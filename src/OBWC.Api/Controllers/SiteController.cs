namespace OBWC.Api.Controllers;

[ApiController]
public class SiteController(ApplicationDbContext db, IClubMailer mail, IHostEnvironment environment) : ClubApiController
{
    [HttpGet("api/settings/public")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicInfoDto>> PublicInfo(CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken);
        return Ok(MapInfo(settings));
    }

    [HttpPut("api/settings/public")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<PublicInfoDto>> UpdatePublicInfo(UpdatePublicInfoRequest request, CancellationToken cancellationToken)
    {
        await UpsertAsync(SettingKeys.ContactEmail, request.ContactEmail.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.AdultDues, request.AdultDues.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.StudentDues, request.StudentDues.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.StudentDuesNote, request.StudentDuesNote.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.FriendDues, request.FriendDues.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.OfficerTerm, request.OfficerTerm.Trim(), cancellationToken);
        await UpsertAsync(SettingKeys.ElectionMeeting, request.ElectionMeeting.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapInfo(await SettingsAsync(cancellationToken)));
    }

    [HttpGet("api/content/{key}")]
    [AllowAnonymous]
    public async Task<ActionResult<ContentPageDto>> GetContent(string key, CancellationToken cancellationToken)
    {
        if (!ContentKeys.IsKnown(key))
            return NotFound();
        var page = await db.ContentPages.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        return page is null ? NotFound() : Ok(MapPage(page));
    }

    [HttpPut("api/content/{key}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<ContentPageDto>> UpdateContent(string key, SaveContentRequest request, CancellationToken cancellationToken)
    {
        if (!ContentKeys.IsKnown(key))
            return NotFound();
        var page = await db.ContentPages.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (page is null)
        {
            page = new ContentPage { Key = key };
            db.ContentPages.Add(page);
        }

        page.Title = request.Title.Trim();
        page.Body = request.Body.Trim();
        page.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapPage(page));
    }

    [HttpGet("api/officers")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<OfficerDto>>> Officers(CancellationToken cancellationToken) =>
        Ok(await db.Officers.AsNoTracking().OrderBy(officer => officer.SortOrder).ThenBy(officer => officer.RoleTitle)
            .Select(officer => new OfficerDto
            {
                Id = officer.Id,
                RoleTitle = officer.RoleTitle,
                DisplayName = officer.DisplayName,
                TermLabel = officer.TermLabel,
                SortOrder = officer.SortOrder
            })
            .ToListAsync(cancellationToken));

    [HttpPost("api/officers")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<OfficerDto>> CreateOfficer(SaveOfficerRequest request, CancellationToken cancellationToken)
    {
        var officer = new Officer { Id = Guid.NewGuid() };
        Apply(officer, request);
        db.Officers.Add(officer);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/officers/{officer.Id}", MapOfficer(officer));
    }

    [HttpPut("api/officers/{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<OfficerDto>> UpdateOfficer(Guid id, SaveOfficerRequest request, CancellationToken cancellationToken)
    {
        var officer = await db.Officers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (officer is null)
            return NotFound();
        Apply(officer, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapOfficer(officer));
    }

    [HttpDelete("api/officers/{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> DeleteOfficer(Guid id, CancellationToken cancellationToken)
    {
        var officer = await db.Officers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (officer is null)
            return NotFound();
        db.Officers.Remove(officer);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/contact")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> Contact(ContactRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
            return Accepted(new MessageResponse { Message = "Thanks. Your note is on its way." });

        var settings = await SettingsAsync(cancellationToken);
        var contactEmail = settings.GetValueOrDefault(SettingKeys.ContactEmail) ?? ClubContentContact.Email;
        if (!mail.IsConfigured && !environment.IsDevelopment())
        {
            return Problem(
                detail: $"The site cannot send mail yet. Write to {contactEmail} directly.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email is not configured");
        }

        var body = $"From: {request.Name.Trim()} <{request.Email.Trim()}>\n\n{request.Message.Trim()}";
        try
        {
            await mail.SendAsync(contactEmail, "OBWC website contact", body, request.Name.Trim(), request.Email.Trim(), cancellationToken);
        }
        catch (MailNotConfiguredException)
        {
            return Problem(
                detail: $"The site cannot send mail yet. Write to {contactEmail} directly.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email is not configured");
        }

        var message = mail.IsConfigured
            ? "Thanks. Your note was sent to the club."
            : $"Thanks. Mail is not configured on this server, so the note was saved for the developer. You can also write {contactEmail} directly.";
        return Accepted(new MessageResponse { Message = message });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok" });

    private async Task<Dictionary<string, string>> SettingsAsync(CancellationToken cancellationToken) =>
        await db.SiteSettings.AsNoTracking().ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await db.SiteSettings.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (setting is null)
            db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
        else
            setting.Value = value;
    }

    private static PublicInfoDto MapInfo(IReadOnlyDictionary<string, string> settings) => new()
    {
        ContactEmail = settings.GetValueOrDefault(SettingKeys.ContactEmail) ?? ClubContentContact.Email,
        AdultDues = settings.GetValueOrDefault(SettingKeys.AdultDues) ?? "",
        StudentDues = settings.GetValueOrDefault(SettingKeys.StudentDues) ?? "",
        StudentDuesNote = settings.GetValueOrDefault(SettingKeys.StudentDuesNote) ?? "",
        FriendDues = settings.GetValueOrDefault(SettingKeys.FriendDues) ?? "",
        OfficerTerm = settings.GetValueOrDefault(SettingKeys.OfficerTerm) ?? "",
        ElectionMeeting = settings.GetValueOrDefault(SettingKeys.ElectionMeeting) ?? "",
        ClubToday = ClubClock.Today()
    };

    private static ContentPageDto MapPage(ContentPage page) => new()
    {
        Key = page.Key,
        Title = page.Title,
        Body = page.Body,
        UpdatedAtUtc = page.UpdatedAtUtc
    };

    private static void Apply(Officer officer, SaveOfficerRequest request)
    {
        officer.RoleTitle = request.RoleTitle.Trim();
        officer.DisplayName = request.DisplayName.Trim();
        officer.TermLabel = Clean(request.TermLabel);
        officer.SortOrder = request.SortOrder;
    }

    private static OfficerDto MapOfficer(Officer officer) => new()
    {
        Id = officer.Id,
        RoleTitle = officer.RoleTitle,
        DisplayName = officer.DisplayName,
        TermLabel = officer.TermLabel,
        SortOrder = officer.SortOrder
    };
}
