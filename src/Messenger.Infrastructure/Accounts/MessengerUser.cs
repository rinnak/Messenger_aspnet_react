using Microsoft.AspNetCore.Identity;

namespace Messenger.Infrastructure.Accounts;

public sealed class MessengerUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
