using Messenger.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Messenger.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Messenger.Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAntiforgery antiforgery,
    RegisterAccount registerAccount,
    UserManager<MessengerUser> userManager,
    SignInManager<MessengerUser> signInManager,
    IAccountStore accounts) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken() //Выдача токена гостю
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        
        return Ok(new {requestToken = tokens.RequestToken});
        //клиент положит этот тоект в заголовок POST
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegistrationRequest request,
        [FromHeader(Name = "X-CSRF-TOKEN"), Required] string csrfToken)
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
    
    public sealed record LoginRequest(
        [Required, StringLength(24)] string Username,
        [Required, StringLength(128)] string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromHeader(Name = "X-CSRF-TOKEN"), Required] string csrfToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return Problem(statusCode: 400, title: "Не прошла проверка CSRF",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_csrf_token" });
        }
        
        var user = await userManager.FindByNameAsync(request.Username);

        if (user is null)
        {
            return Unauthorized(new {code = "invalid_credentals", message = "Неверный логин или пароль"});
        }
        
        //Добавляет Ser-Cookie если вход разрешен
        var signIn = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (!signIn.Succeeded)
        {
            return Unauthorized(new { code = "invalid_credentials", message = "Неверный логин или пароль" });
        }
        
        return Ok(new AccountProfile(user.Id, user.UserName!, user.DisplayName));
    }

    [Authorize]
    [HttpGet("me")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)] //запрет кеширования
    public async Task<IActionResult> GetCurrentAccount(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userManager.GetUserId(User), out var userId))
        {
            return Unauthorized();
        }

        var profile = await accounts.FindProfileByIdAsync(userId, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromHeader(Name = "X-CSRF-TOKEN"), Required]
        string csrfToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext)) //проверка токена
        {
            return Problem(
                statusCode: 400,
                title: "Не прошла проверка CSRF",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_csrf_token" }
            );
        }
        
        await signInManager.SignOutAsync(); //Identity сам удаляет куки входа
        
        return NoContent();
    }

    private ObjectResult RegistrationProblem(int status, string code, string title)
    {
        return Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?>{ ["code"] = code});
    }
}