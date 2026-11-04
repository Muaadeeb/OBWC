namespace OBWC.Shared.Dtos;

public record DevMailMessageDto
{
    public string To { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public string? Link { get; init; }
}
