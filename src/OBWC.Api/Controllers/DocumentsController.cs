namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(ApplicationDbContext db, FileStorage files) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ClubDocumentDto>>> List([FromQuery] DocumentCategory? category, CancellationToken cancellationToken)
    {
        var query = db.ClubDocuments.AsNoTracking().AsQueryable();
        if (category is not null)
            query = query.Where(document => document.Category == category);
        if (!CanReadMemberDocuments)
            query = query.Where(document => document.Visibility == DocumentVisibility.Public);
        var documents = await query.OrderBy(document => document.Category).ThenBy(document => document.Title).ToListAsync(cancellationToken);
        return Ok(documents.Select(Map).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<ClubDocumentDto>> Upload(
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string category,
        [FromForm] string? visibility,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        if (!Enum.TryParse<DocumentCategory>(category, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return Bad("Category must be Constitution, Budget, or Minutes.");
        var parsedVisibility = DocumentVisibility.MembersOnly;
        if (!string.IsNullOrWhiteSpace(visibility))
        {
            if (!Enum.TryParse<DocumentVisibility>(visibility, ignoreCase: true, out parsedVisibility) || !Enum.IsDefined(parsedVisibility))
                return Bad("Visibility must be Public or MembersOnly.");
        }

        var cleanTitle = title?.Trim() ?? "";
        if (cleanTitle.Length is 0 or > 200)
            return Bad("Enter a title of 200 characters or fewer.");

        StoredFile stored;
        try
        {
            stored = await files.SaveAsync(file, "documents", FileStorage.DocumentExtensions, cancellationToken);
        }
        catch (InvalidUploadException ex)
        {
            return Bad(ex.Message);
        }

        var document = new ClubDocument
        {
            Id = Guid.NewGuid(),
            Category = parsed,
            Visibility = parsedVisibility,
            Title = cleanTitle,
            Description = Clean(description),
            FileName = string.IsNullOrWhiteSpace(stored.OriginalFileName) ? "document.pdf" : stored.OriginalFileName,
            RelativePath = stored.RelativePath,
            ContentType = stored.ContentType,
            OwnerUserId = CurrentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ClubDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/documents/{document.Id}", Map(document));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<ClubDocumentDto>> Rename(Guid id, RenameDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await db.ClubDocuments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return NotFound();
        document.Title = request.Title.Trim();
        if (request.Description is not null)
            document.Description = Clean(request.Description);
        if (request.Category is DocumentCategory category && Enum.IsDefined(category))
            document.Category = category;
        if (request.Visibility is DocumentVisibility visibility && Enum.IsDefined(visibility))
            document.Visibility = visibility;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(document));
    }

    [HttpGet("{id:guid}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.ClubDocuments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return NotFound();
        if (document.Visibility == DocumentVisibility.MembersOnly && !CanReadMemberDocuments)
            return CurrentUserId is null
                ? Unauthorized()
                : Forbidden("This document is for members.");
        var stream = files.Open(document.RelativePath);
        return File(stream, document.ContentType, document.FileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.ClubDocuments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return NotFound();
        db.ClubDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(document.RelativePath);
        return NoContent();
    }

    private bool CanReadMemberDocuments =>
        IsElevated || User.IsInRole(RoleNames.Member) || User.IsInRole(RoleNames.Gm);

    private static ClubDocumentDto Map(ClubDocument document) => new()
    {
        Id = document.Id,
        Category = document.Category,
        Visibility = document.Visibility,
        Title = document.Title,
        Description = document.Description,
        FileName = document.FileName,
        CreatedAtUtc = document.CreatedAtUtc
    };
}
