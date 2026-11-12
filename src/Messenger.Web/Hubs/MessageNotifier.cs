using Messenger.Application.Chats;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Web.Hubs;

public sealed class MessageNotifier(
    IHubContext<ChatHub> hub,
    IDirectChatStore chats,
    ILogger<MessageNotifier> logger)
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
}