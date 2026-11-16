using Messenger.Application.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Messenger.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;

namespace Messenger.Infrastructure.Accounts;

public sealed class IdentityAccountStore(
    UserManager<MessengerUser> userManager,
    MessengerDbContext db,
    IMemoryCache cache) : IAccountStore
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

    public Task<List<AccountProfile>> SearchAsync(string query,
        Guid currentUserId, CancellationToken cancellationToken)
    {
        var escaped = query.Replace("!", "!!").Replace("%", "_%").Replace("_", "!_");
        var pattern = $"{escaped}";
        
        return db.Users.AsNoTracking()
            .Where(user => user.Id != currentUserId && user.UserName != null)
            .Where(user => EF.Functions.ILike(user.UserName!, pattern, "!") ||
                           EF.Functions.ILike(user.DisplayName, pattern, "!"))
            .OrderBy(user => user.NormalizedUserName)
            .Select(user => new AccountProfile(user.Id, user.UserName!, user.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountProfile?> FindByUsernameAsync(string username)
    {
        var user = await userManager.FindByNameAsync(username);

        if (user is null)
        {
            return null;
        }
        
        return new AccountProfile(user.Id, user.UserName!, user.DisplayName);
    }

    public async Task<AccountProfile?> FindProfileByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = (nameof(AccountProfile), userId);

        if (cache.TryGetValue<AccountProfile>(cacheKey, out var cachedProfile) &&
            cachedProfile is not null)
        {
            return cachedProfile;
        }

        var profile = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AccountProfile(user.Id, user.UserName!, user.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is not null)
        {
            cache.Set(cacheKey, profile, TimeSpan.FromMinutes(5));
        }
        return profile;
    }
}
