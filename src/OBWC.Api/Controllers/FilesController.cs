namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(ApplicationDbContext db, FileStorage files) : ClubApiController
{
    [HttpGet("gallery/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Gallery(Guid id, CancellationToken cancellationToken)
    {
        var photo = await db.GalleryPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (photo is null)
            return NotFound();
        return File(files.Open(photo.RelativePath), photo.ContentType);
    }

    [HttpGet("venues/{id:guid}/{side}")]
    [AllowAnonymous]
    public async Task<IActionResult> Venue(Guid id, string side, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (venue is null)
            return NotFound();

        var (path, contentType) = side.ToLowerInvariant() switch
        {
            "inside" => (venue.InsidePhotoPath, venue.InsideContentType),
            "outside" => (venue.OutsidePhotoPath, venue.OutsideContentType),
            _ => (null, null)
        };
        if (path is null)
            return NotFound();
        return File(files.Open(path), contentType ?? "application/octet-stream");
    }

    [HttpGet("officers/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Officer(Guid id, CancellationToken cancellationToken)
    {
        var officer = await db.Officers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (officer?.PhotoPath is null)
            return NotFound();
        return File(files.Open(officer.PhotoPath), officer.PhotoContentType ?? "application/octet-stream");
    }

    [HttpGet("event-photos/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> EventPhoto(Guid id, CancellationToken cancellationToken)
    {
        var photo = await db.EventPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (photo is null)
            return NotFound();
        return File(files.Open(photo.RelativePath), photo.ContentType);
    }
}
