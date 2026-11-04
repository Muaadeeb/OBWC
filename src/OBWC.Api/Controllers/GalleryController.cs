namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/gallery")]
public class GalleryController(
    ApplicationDbContext db,
    FileStorage files,
    IOptions<UploadOptions> uploadOptions,
    IAuthorizationService authorization) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<GalleryListDto>> List(CancellationToken cancellationToken)
    {
        var photos = await db.GalleryPhotos.AsNoTracking()
            .OrderByDescending(photo => photo.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return Ok(new GalleryListDto
        {
            Count = photos.Count,
            MaxPhotos = MaxPhotos,
            Photos = photos.Select(Map).ToArray()
        });
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<GalleryPhotoDto>> Upload(
        [FromForm] string caption,
        [FromForm] string publicEventName,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        var cleanCaption = caption?.Trim() ?? "";
        var cleanEvent = publicEventName?.Trim() ?? "";
        if (cleanCaption.Length is 0 or > 200)
            return Bad("Enter a caption of 200 characters or fewer.");
        if (cleanEvent.Length is 0 or > 200)
            return Bad("Name the public event (game day, Nuke-Con, NADBAG, ScroogeCon, or another club event).");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var count = await db.GalleryPhotos.CountAsync(cancellationToken);
        if (count >= MaxPhotos)
            return ConflictProblem($"The gallery holds at most {MaxPhotos} photos of OBWC games at public events.");

        StoredFile stored;
        try
        {
            stored = await files.SaveAsync(file, "gallery", FileStorage.ImageExtensions, cancellationToken);
        }
        catch (InvalidUploadException ex)
        {
            return Bad(ex.Message);
        }

        var photo = new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = cleanCaption,
            PublicEventName = cleanEvent,
            RelativePath = stored.RelativePath,
            ContentType = stored.ContentType,
            OwnerUserId = CurrentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.GalleryPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/gallery/{photo.Id}", Map(photo));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var photo = await db.GalleryPhotos.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (photo is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, photo);
        if (deny is not null)
            return deny;
        db.GalleryPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(photo.RelativePath);
        return NoContent();
    }

    private int MaxPhotos => uploadOptions.Value.MaxGalleryPhotos <= 0
        ? GalleryLimits.MaxPhotos
        : uploadOptions.Value.MaxGalleryPhotos;

    private GalleryPhotoDto Map(GalleryPhoto photo) => new()
    {
        Id = photo.Id,
        Caption = photo.Caption,
        PublicEventName = photo.PublicEventName,
        Url = $"api/files/gallery/{photo.Id}",
        CreatedAtUtc = photo.CreatedAtUtc,
        CanEdit = IsElevated || CurrentUserId == photo.OwnerUserId
    };
}
