using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Messenger.Application.Chats;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Messenger.Web.Hubs; 

namespace Messenger.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/chats/{chatId:guid}/messages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MessagesController(
    IAntiforgery antiforgery, 
    SendMessage sendMessage, 
    IMessageStore messages,
    MessageNotifier notifier) : ControllerBase
{
    public sealed record SendMessageRequest(Guid ClientMessageId, string? Text);

    [HttpPost]
    public async Task<IActionResult> Send(
        Guid chatId, 
        [FromBody] SendMessageRequest request,
        [FromHeader(Name = "X-CSRF-TOKEN"), Required]
        string csrfToken,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return MessageProblem(400, "invalid_csrf_token", "Не прошла проверка CSRF");
        }
        
        var result = await sendMessage.SendAsync(chatId, userId, request.ClientMessageId,
            request.Text, cancellationToken);

        if (result.Created && result.Message is { } createdMessage)
        {
            await notifier.NotifyMessageCreatedAsync(createdMessage);
        }

        return result.ErrorCode switch
        {
            "invalid_message" => MessageProblem(400, "invalid_message",
                "Укажи ID отправки и текст от 1 до 2000 символов"),
            "chat_not_found" => MessageProblem(404, "chat_not_found", "Диалог не найден"),
            "message_id_conflict" => MessageProblem(409, "message_id_conflict",
                "Этот ID отправки уже использован для другого текста"),
            _ => result.Created ? StatusCode(201, result.Message) : Ok(result.Message)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        Guid chatId,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 100)] int limit = 30,
        [FromQuery, Range(1, int.MaxValue)] int? beforeSeq = null, //размер страницы и граница старых сообщений
        [FromQuery, Range(0, int.MaxValue)] int? afterSeq = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (beforeSeq.HasValue && afterSeq.HasValue)
        {
            return MessageProblem(400, "invalid_cursor", "Укажи только один курсор: beforeSeq или afterSeq");
        }
        
        var page = await messages.ReadHistoryAsync(chatId, userId, limit, beforeSeq, afterSeq, cancellationToken);

        return page is null ? MessageProblem(404, "chat_not_found", "Диалог не найден") : Ok(page);
    }

    private ObjectResult MessageProblem(int status, string code, string title) =>
        Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}