namespace Messenger.Domain.Chats;

public class DirectChat
{
    public Guid Id { get; init; }
    public Guid UserAId { get; init; }
    public Guid UserBId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int LastSequence { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }
    public int RecordMessage(DateTimeOffset createdAt)
    {
        LastSequence = checked(LastSequence + 1);
        LastMessageAt = createdAt;
        
        return LastSequence;
    }
}