using Messenger.Application.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAntiforgery antiforgery, RegisterAccount registerAccount) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken() //Выдача токена гостю
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        
        return Ok(new {requestToken = tokens.RequestToken});
        //клиент положит этот тоект в заголовок POST
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext)) 
        {
            return RegistrationProblem(400, "invalid_csrf_token", "Не прошла проверка CSRF");
        }
        
        var result = await registerAccount.RegisterAsync(request); //регистрация в Application

        if (result.User is not null)
        {
            return StatusCode(201, result.User);
        }

        if (result.ErrorCode == "username_taken")
        {
            return RegistrationProblem(409, "username_taken", "Этот логин уже занят");
        }
        
        return RegistrationProblem(409, result.ErrorCode ?? "registration_rejected", "Проверь данные регистрации");
    }

    private ObjectResult RegistrationProblem(int status, string code, string title)
    {
        return Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?>{ ["code"] = code});
    }
}