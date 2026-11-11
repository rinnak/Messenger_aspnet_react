using Messenger.Application.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Messenger.Infrastructure.Accounts;

public sealed class IdentityAccountStore(UserManager<MessengerUser> userManager) : IAccountStore
{
    public async Task<RegistrationResult> CreateAsync(string username, string displayName, string password)
    {
        var user = new MessengerUser { UserName = username, DisplayName = displayName }; //объект акка в памяти

        try
        {
            // Identity проверяет пароль, вычисляет хеш и сохраняет акк
            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                return new RegistrationResult(new AccountProfile(user.Id, username, displayName), null);
            }

            if (result.Errors.Any(error => error.Code ==
                                           nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return new RegistrationResult(null, "username_taken");
            }

            return new RegistrationResult(null, "registration_rejected");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                  {
                                                      SqlState: PostgresErrorCodes.UniqueViolation,
                                                      ConstraintName: "UserNameIndex"
                                                  })
        {
            return new RegistrationResult(null, "username_taken");
            //cлучай с параллельным запросом
        }
    }

    public async Task<AccountProfile?> FindByUsernameAsync(string username)
    {
        var user = await userManager.FindByNameAsync(username);

        if (user is null)
        {
            return null;
        }
        
        return new AccountProfile(user.Id, user.UserName, user.DisplayName);
    }
}
