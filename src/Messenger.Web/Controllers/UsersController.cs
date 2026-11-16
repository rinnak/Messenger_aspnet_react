using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Messenger.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Web.Controllers;


    [ApiController]
    [Authorize]
    [Route("api/users")]
    public sealed class UsersController(IAccountStore accounts) : ControllerBase
    {
        [HttpGet("search")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Search(
            [FromQuery, Required, StringLength(50)]
            string query,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }
            var items = await accounts.SearchAsync(query.Trim(), userId, cancellationToken);
            return Ok(new { items });
        }
        
        
        [HttpGet("lookup")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> FindByUsername(
            [FromQuery, Required, RegularExpression("^[A-Za-z0-9_]{3,24}$")]
            string username)
        {
            var profile = await accounts.FindByUsernameAsync(username);

            if (profile is null)
            {
                return Problem(
                    statusCode: 404,
                    title: "Пользователь с таким логином не найден",
                    extensions: new Dictionary<string, object?> { ["code"] = "user_not_found" }
                );
            }
            return Ok(profile);
        }
    }
    