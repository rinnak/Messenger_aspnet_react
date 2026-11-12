namespace Messenger.Application.Chats;

public sealed record MessageView(Guid Id, Guid ChatId, Guid SenderId, Guid ClientMessageId,
    int Sequence, string Text, DateTimeOffset CreatedAt);
public sealed record MessagePage(IReadOnlyList<MessageView> Items, int? NexSeq);
public sealed record SendMessageResult(MessageView? Message, bool Created, string? ErrorCode = null);

public interface IMessageStore
{
    Task<SendMessageResult> SaveAsync(Guid chatId, Guid senderId, Guid clientMessageId, string text, CancellationToken cancellationToken);
    Task<MessagePage?> ReadHistoryAsync(Guid chatId, Guid userId, int limit,
        int? beforeSeq, int? afterSeq, CancellationToken cancellationToken);
}

public sealed class SendMessage(IMessageStore messages)
{
    public Task<SendMessageResult> SendAsync(Guid chatId, Guid senderId, Guid clientMessageId,
        string? text, CancellationToken cancellationToken)
    {
        var normalizedText = text?.Trim() ?? "";

        if (clientMessageId == Guid.Empty || normalizedText.Length is < 1 or > 2000)
        {
            return Task.FromResult(new SendMessageResult(null, false, "invalid_message"));
        }
        
        return messages.SaveAsync(chatId, senderId, clientMessageId, normalizedText, cancellationToken);
    }
}