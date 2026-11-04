namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Policy = PolicyNames.Member)]
public class DocumentsController(ApplicationDbContext db, FileStorage files) : ClubApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClubDocumentDto>>> List([FromQuery] DocumentCategory? category, CancellationToken cancellationToken)
    {
        var query = db.ClubDocuments.AsNoTracking().AsQueryable();
        if (category is not null)
            query = query.Where(document => document.Category == category);
        var documents = await query.OrderByDescending(document => document.CreatedAtUtc).ToListAsync(cancellationToken);
        return Ok(documents.Select(Map).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<ClubDocumentDto>> Upload(
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string category,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        if (!Enum.TryParse<DocumentCategory>(category, ignoreCase: true, out var parsed))
            return Bad("Category must be Constitution or Budget.");
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
            Title = cleanTitle,
            Description = Clean(description),
            FileName = string.IsNullOrWhiteSpace(stored.OriginalFileName) ? "document" : stored.OriginalFileName,
            RelativePath = stored.RelativePath,
            ContentType = stored.ContentType,
            OwnerUserId = CurrentUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ClubDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/documents/{document.Id}", Map(document));
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.ClubDocuments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return NotFound();
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

    private static ClubDocumentDto Map(ClubDocument document) => new()
    {
        Id = document.Id,
        Category = document.Category,
        Title = document.Title,
        Description = document.Description,
        FileName = document.FileName,
        CreatedAtUtc = document.CreatedAtUtc
    };
}
