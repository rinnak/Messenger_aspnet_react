using System.Data;
using System.Xml.Xsl;
using Messenger.Application.Chats;
using Messenger.Domain.Chats;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Chats;

public sealed class MessageStore(MessengerDbContext db) : IMessageStore
{
    public async Task<SendMessageResult> SaveAsync(Guid chatId, Guid senderId, Guid clientMessageId, string text,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var lockedChats = await db.DirectChats.FromSqlInterpolated($"""
                                                                    select * from "DirectChats"
                                                                    where "Id" = {chatId} and ("UserAId" = {senderId} or "UserBId" = {senderId})
                                                                    for UPDATE 
                                                                    """).AsTracking().ToListAsync(cancellationToken);

        var chat = lockedChats.SingleOrDefault();
        if (chat is null)
        {
            return new SendMessageResult(null, false, "chat_not_found");
        }

        // поиск сохраненной отправки
        var previous = await db.Messages.AsNoTracking().SingleOrDefaultAsync(message =>
            message.ChatId == chatId && message.SenderId == senderId &&
            message.ClientMessageId == clientMessageId, cancellationToken);

        //обработка повтора отправки
        if (previous is not null)
        {
            return previous.Text == text
                ? new SendMessageResult(ToView(previous), false) // при совпадении возвр прежнее соо
                : new SendMessageResult(null, false, "message_id_conflict");
        }
        
        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddTicks(-(now.Ticks % 10));
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = senderId,
            ClientMessageId = clientMessageId,
            Sequence = chat.RecordMessage(createdAt),
            Text = text,
            CreatedAt = createdAt
        };
        
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SendMessageResult(ToView(message), true);
    }

    public async Task<MessagePage?> ReadHistoryAsync(Guid chatId, Guid userId, int limit,
        int? beforeSeq, int? afterSeq, CancellationToken cancellationToken)
    {
        var canRead = await db.DirectChats.AnyAsync(chat => chat.Id == chatId &&
                                                            (chat.UserAId == userId || chat.UserBId == userId), cancellationToken);
        if (!canRead)
        {
            return null;
        }
        
        var query = db.Messages.AsNoTracking().Where(message => message.ChatId == chatId);

        //если запрошены более ранние сообщения
        if (beforeSeq is int before)
        {
            query = query.Where(message => message.Sequence < before);
        }

        //если запрошены более поздние сообщения
        if (afterSeq is int after)
        {
            query = query.Where(message => message.Sequence > after);
        }
        
        var ordered = afterSeq.HasValue
            ? query.OrderBy(message => message.Sequence)
            : query.OrderByDescending(message => message.Sequence);
        
        var page = await ordered.Take(limit + 1).ToListAsync(cancellationToken);

        int? nextSeq = page.Count > limit ? page[limit - 1].Sequence : null;

        if (nextSeq.HasValue)
        {
            page.RemoveAt(limit);
        }

        if (!afterSeq.HasValue) //база вернула записи от новых к старым
        {
            page.Reverse(); //делаем от старого к новому
        }

        return new MessagePage(page.Select(ToView).ToList(), nextSeq);
    }
    
    private static MessageView ToView(Message message) =>
        new(message.Id, message.ChatId, message.SenderId, message.ClientMessageId,
            message.Sequence, message.Text, message.CreatedAt);
}
