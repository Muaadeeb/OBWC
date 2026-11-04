namespace OBWC.Api.Controllers;

/// <summary>
/// Photos attached to a scheduled game. A GM may add and change photos only on games they scheduled,
/// and only photos they uploaded. Admin and SuperAdmin can do the same on every game.
/// </summary>
[ApiController]
[Route("api/events/{eventId:guid}/photos")]
public class EventPhotosController(ApplicationDbContext db, FileStorage files) : ClubApiController
{
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<EventPhotoDto>> Upload(
        Guid eventId,
        [FromForm] string caption,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        var session = await db.PlaySessions.SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (session is null)
            return NotFound();
        var denied = DenyUpload(session);
        if (denied is not null)
            return denied;

        var cleanCaption = caption?.Trim() ?? "";
        if (cleanCaption.Length is 0 or > 200)
            return Bad("Enter a caption of 200 characters or fewer.");

        StoredFile stored;
        try
        {
            stored = await files.SaveAsync(file, "events", FileStorage.ImageExtensions, cancellationToken);
        }
        catch (InvalidUploadException ex)
        {
            return Bad(ex.Message);
        }

        var photo = new EventPhoto
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            Caption = cleanCaption,
            RelativePath = stored.RelativePath,
            ContentType = stored.ContentType,
            OwnerUserId = CurrentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.EventPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/files/event-photos/{photo.Id}", Map(photo, canEdit: true));
    }

    [HttpPut("{photoId:guid}")]
    [Authorize]
    public async Task<ActionResult<EventPhotoDto>> Update(
        Guid eventId,
        Guid photoId,
        UpdateEventPhotoRequest request,
        CancellationToken cancellationToken)
    {
        var photo = await FindAsync(eventId, photoId, cancellationToken);
        if (photo is null)
            return NotFound();
        if (!CanChange(photo))
            return Forbidden("You can change only photos you uploaded. Admins can change any game photo.");
        photo.Caption = request.Caption.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(photo, canEdit: true));
    }

    [HttpDelete("{photoId:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid eventId, Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await FindAsync(eventId, photoId, cancellationToken);
        if (photo is null)
            return NotFound();
        if (!CanChange(photo))
            return Forbidden("You can change only photos you uploaded. Admins can change any game photo.");
        db.EventPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        files.Delete(photo.RelativePath);
        return NoContent();
    }

    private ActionResult? DenyUpload(PlaySession session)
    {
        if (IsElevated)
            return null;
        if (!User.IsInRole(RoleNames.Gm) || CurrentUserId != session.CreatedByUserId)
            return Forbidden("A GM can add photos only to games they scheduled. Admins can add photos to any game.");
        return null;
    }

    private bool CanChange(EventPhoto photo) =>
        IsElevated || (!string.IsNullOrEmpty(CurrentUserId) && CurrentUserId == photo.OwnerUserId);

    private Task<EventPhoto?> FindAsync(Guid eventId, Guid photoId, CancellationToken cancellationToken) =>
        db.EventPhotos.SingleOrDefaultAsync(item => item.Id == photoId && item.PlaySessionId == eventId, cancellationToken);

    private static EventPhotoDto Map(EventPhoto photo, bool canEdit) => new()
    {
        Id = photo.Id,
        Caption = photo.Caption,
        Url = $"api/files/event-photos/{photo.Id}",
        CreatedAtUtc = DateTime.SpecifyKind(photo.CreatedAtUtc, DateTimeKind.Utc),
        CanEdit = canEdit
    };
}
