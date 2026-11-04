namespace OBWC.Api.Controllers;

/// <summary>
/// Development-only view of confirmation and reset messages when SMTP is not configured.
/// </summary>
[ApiController]
[Route("api/dev/mail")]
public class DevMailController(EmailOutbox outbox, IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<DevMailMessageDto>> List()
    {
        if (!environment.IsDevelopment())
            return NotFound();

        return Ok(outbox.Snapshot().Select(email => new DevMailMessageDto
        {
            To = email.To,
            Subject = email.Subject,
            Body = email.Body,
            Link = email.Link
        }).ToArray());
    }
}
