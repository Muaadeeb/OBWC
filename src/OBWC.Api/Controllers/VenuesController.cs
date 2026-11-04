namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/venues")]
public class VenuesController(ApplicationDbContext db, FileStorage files) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<VenueDto>>> List(CancellationToken cancellationToken) =>
        Ok((await db.Venues.AsNoTracking().OrderBy(venue => venue.SortOrder).ThenBy(venue => venue.Name).ToListAsync(cancellationToken))
            .Select(Map)
            .ToArray());

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<VenueDto>> Create(SaveVenueRequest request, CancellationToken cancellationToken)
    {
        var venue = new Venue { Id = Guid.NewGuid() };
        Apply(venue, request);
        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/venues/{venue.Id}", Map(venue));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<VenueDto>> Update(Guid id, SaveVenueRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (venue is null)
            return NotFound();
        Apply(venue, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(venue));
    }

    [HttpPost("{id:guid}/photos")]
    [Authorize(Policy = PolicyNames.Admin)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<VenueDto>> UploadPhotos(Guid id, IFormFile? inside, IFormFile? outside, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (venue is null)
            return NotFound();
        if (inside is null && outside is null)
            return Bad("Choose an inside photo, an outside photo, or both.");

        try
        {
            if (inside is not null)
            {
                var stored = await files.SaveAsync(inside, "venues", FileStorage.ImageExtensions, cancellationToken);
                files.Delete(venue.InsidePhotoPath);
                venue.InsidePhotoPath = stored.RelativePath;
                venue.InsideContentType = stored.ContentType;
            }

            if (outside is not null)
            {
                var stored = await files.SaveAsync(outside, "venues", FileStorage.ImageExtensions, cancellationToken);
                files.Delete(venue.OutsidePhotoPath);
                venue.OutsidePhotoPath = stored.RelativePath;
                venue.OutsideContentType = stored.ContentType;
            }
        }
        catch (InvalidUploadException ex)
        {
            return Bad(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(venue));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (venue is null)
            return NotFound();
        db.Venues.Remove(venue);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("This venue is still used by a scheduled game.");
        }

        files.Delete(venue.InsidePhotoPath);
        files.Delete(venue.OutsidePhotoPath);
        return NoContent();
    }

    private static void Apply(Venue venue, SaveVenueRequest request)
    {
        venue.Name = request.Name.Trim();
        venue.Address = Clean(request.Address);
        venue.Description = Clean(request.Description);
        venue.SortOrder = request.SortOrder;
    }

    private static VenueDto Map(Venue venue) => new()
    {
        Id = venue.Id,
        Name = venue.Name,
        Address = venue.Address,
        Description = venue.Description,
        HasInsidePhoto = venue.InsidePhotoPath is not null,
        HasOutsidePhoto = venue.OutsidePhotoPath is not null,
        InsidePhotoUrl = venue.InsidePhotoPath is null ? null : $"api/files/venues/{venue.Id}/inside",
        OutsidePhotoUrl = venue.OutsidePhotoPath is null ? null : $"api/files/venues/{venue.Id}/outside",
        SortOrder = venue.SortOrder
    };
}
