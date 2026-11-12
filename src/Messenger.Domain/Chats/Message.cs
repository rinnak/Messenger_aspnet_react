namespace Messenger.Domain.Chats;

public class Message
{
    public Guid Id { get; init; }
    public Guid ChatId { get; init; }
    public Guid SenderId { get; init; }
    public Guid ClientMessageId { get; init; }
    public int Sequence { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}