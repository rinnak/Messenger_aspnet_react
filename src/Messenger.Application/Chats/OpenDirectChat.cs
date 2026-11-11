using Messenger.Application.Accounts;

namespace Messenger.Application.Chats;

public sealed record ChatSummary(Guid Id, AccountProfile Peer, DateTimeOffset CreatedAt, DateTimeOffset? LastMessageAt);

public sealed record OpenChatResult(ChatSummary? Chat, bool Created, string? ErrorCode = null);

public interface IDirectChatStore
{
    Task<OpenChatResult> FindOrCreateChatAsync(Guid userId, Guid peerId, CancellationToken cancellationToken);
    Task<List<ChatSummary>> ListChatsAsync(Guid userId, CancellationToken cancellationToken);
    Task<ChatSummary?> FindChatForUserAsync(Guid userId, Guid chatId, CancellationToken cancellationToken);
}

public sealed class OpenDirectChat(IDirectChatStore chats)
{
    public Task<OpenChatResult> OpenChatAsync(Guid userId, Guid peerId, CancellationToken cancellationToken)
    {
        if (peerId == Guid.Empty || peerId == userId)
        {
            return Task.FromResult(new OpenChatResult(null, false, "invalid_peer"));
        }
        
        return chats.FindOrCreateChatAsync(userId, peerId, cancellationToken);
    }
}