using Messenger.Application.Accounts;
using Messenger.Application.Chats;
using Messenger.Domain.Chats;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Chats;

public sealed class DirectChatStore(MessengerDbContext db) : IDirectChatStore
{
    // создаем или читаем диалог для пары
    public async Task<OpenChatResult> FindOrCreateChatAsync(Guid userId, Guid peerId, CancellationToken cancellationToken)
    {
        var chatId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                                      INSERT INTO "DirectChats" ("Id", "UserAId", "UserBId", "CreatedAt", "LastSequence")
                                                                      SELECT {chatId}, LEAST(a."Id", b."Id"), GREATEST(a."Id", b."Id"),
                                                                             {createdAt}, 0
                                                                             FROM "Users" AS a CROSS JOIN "Users" AS b  
                                                                             WHERE a."Id" = {userId} AND b."Id" = {peerId}
                                                                             ON CONFLICT ("UserAId", "UserBId") DO NOTHING
                                                                      """, cancellationToken);
        var pair = db.DirectChats.Where(chat => 
            (chat.UserAId == userId && chat.UserBId == peerId) ||
            (chat.UserBId == userId && chat.UserAId == peerId));
        
        //чтение диалога и публ профиля по диалогу пары
        var chat = await ProjectChats(pair, userId).SingleOrDefaultAsync(cancellationToken);

        return new OpenChatResult(chat, inserted == 1, chat is null ? "user_not_found" : null);
    }

    // чтение личных диалогов пользователя
    public Task<List<ChatSummary>> ListChatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var chats = db.DirectChats.Where(chat => chat.UserAId == userId || chat.UserBId == userId)
            .OrderByDescending(chat => chat.LastMessageAt ?? chat.CreatedAt)
            .ThenBy(chat => chat.Id);
        return ProjectChats(chats, userId).ToListAsync(cancellationToken);
    }

    public Task<ChatSummary?> FindChatForUserAsync(Guid userId, Guid chatId, CancellationToken cancellationToken)
    {
        var chats = db.DirectChats.Where(chat => chat.Id == chatId &&
                                           (chat.UserAId == userId || chat.UserBId == userId));
        return ProjectChats(chats, userId).SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<ChatSummary> ProjectChats(IQueryable<DirectChat> chats, Guid userId)
    {
        return from chat in chats
            join peer in db.Users on (chat.UserAId == userId ? chat.UserBId : chat.UserAId) equals peer.Id
            select new ChatSummary(chat.Id, new AccountProfile(peer.Id, peer.UserName!, peer.DisplayName),
                chat.CreatedAt, chat.LastMessageAt);
    }
}
