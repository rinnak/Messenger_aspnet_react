using System.Text.RegularExpressions;

namespace Messenger.Application.Accounts;

public sealed record RegistrationRequest(string? Username, string? DisplayName, string? Password);
public sealed record AccountProfile(Guid Id, string Username, string DisplayName);
public sealed record RegistrationResult(AccountProfile? User, string? ErrorCode);

public interface IAccountStore
{
    Task<RegistrationResult> CreateAsync(string username, string displayName, string password);
    Task<AccountProfile?> FindByUsernameAsync(string username);
    Task<AccountProfile?> FindProfileByIdAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class RegisterAccount(IAccountStore accounts)
{
    
    public Task<RegistrationResult> RegisterAsync(RegistrationRequest request)
    {
        var username = request.Username ?? "";
        var displayName = request.DisplayName?.Trim() ?? "";
        var password = request.Password ?? "";

        if (!Regex.IsMatch(username, @"\A[A-Za-z0-9_]{3,24}\z"))
        {
            return Task.FromResult(new RegistrationResult(null, "invalid_username"));
        }

        if (displayName.Length is < 1 or > 50)
        {
            return Task.FromResult(new RegistrationResult(null, "invalid_display_name"));
        }

        if (password.Length is < 8 or > 128)
        {
            return Task.FromResult(new RegistrationResult(null, "invalid_password"));
        }
        
        return accounts.CreateAsync(username, displayName, password); //данные проверены передаем в бд
    }
}