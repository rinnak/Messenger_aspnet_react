using Messenger.Application.Chats;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Web.Hubs;

public sealed class ChatNotifier(
    IHubContext<ChatHub> hub,
    IDirectChatStore chats,
    ILogger<ChatNotifier> logger)
{
    public async Task NotifyMessageCreatedAsync(MessageView message)
    {
        //ограничение времени поиска и отправки события
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            var chat = await chats.FindChatForUserAsync(message.SenderId, message.ChatId, timeout.Token);
            if (chat is null)
            {
                return;
            }

            var recipients = new[]
            {
                message.SenderId.ToString(),
                chat.Peer.Id.ToString()
            };

            //отправки события во все активные подключения двух акков
            await hub.Clients.Users(recipients).SendAsync("MessageCreated", message, timeout.Token);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не уделось отправить MessageCreated для {MessageId}. Тип ошибки: {ErrorType}",
                message.Id, e.GetType().Name);
        }
    }

    public async Task NotifyChatCreatedAsync(Guid userId, ChatSummary chat)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            var recipients = new[] { userId.ToString(), chat.Peer.Id.ToString() };
            await hub.Clients.Users(recipients)
                .SendAsync("ChatCreated", new { chatId = chat.Id }, timeout.Token);
        }
        catch (Exception e)
        {
            logger.LogWarning("Не удалось отправить ChatCreated для {ChatId}. Тип ошибки: {ErrorType}",
                chat.Id, e.GetType().Name);
        }
    }
}