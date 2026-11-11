using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Messenger.Application.Chats;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/chats")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatController(IAntiforgery antiforgery, OpenDirectChat openChat, IDirectChatStore chats) : ControllerBase
{
    public sealed record OpenChatRequest(Guid PeerUserId); //данные от клиента

    //Открытие личного диалога
    [HttpPost("direct")]
    public async Task<IActionResult> OpenDirect(
        [FromBody] OpenChatRequest request,
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
            return ChatProblem(400, "invalid_csrf_token", "Не прошла проверка CSRF");
        }
        
        var result = await openChat.OpenChatAsync(userId, request.PeerUserId, cancellationToken);

        if (result.ErrorCode == "invalid_peer")
        {
            return ChatProblem(404, "invalid_peer", "Выбери другого пользователя");
        }

        if (result.Chat is null)
        {
            return ChatProblem(404, "user_not_found", "Собеседник не найден");
        }
        
        return result.Created
            ? CreatedAtAction(nameof(GetChat), new {chatId = result.Chat.Id }, result.Chat) 
            : Ok(result.Chat);
    }

    [HttpGet]
    public async Task<IActionResult> ListChats(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }
        
        var items = await chats.ListChatsAsync(userId, cancellationToken);
        return Ok(new {items});
    }

    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> GetChat(Guid chatId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }
        
        var chat = await chats.FindChatForUserAsync(userId, chatId, cancellationToken);

        if (chat is null)
        {
            return ChatProblem(404, "chat_not_found", "Диалог не найден");
        }
        
        return Ok(chat);
    }

    private ObjectResult ChatProblem(int status, string code, string title)
    {
        return Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?>{ ["code"] = code });
    }
}