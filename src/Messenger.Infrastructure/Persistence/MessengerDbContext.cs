using Messenger.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Persistence;

public sealed class MessengerDbContext(DbContextOptions<MessengerDbContext> options)
    : IdentityUserContext<MessengerUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MessengerUser>(user =>
        {
            user.ToTable("Users");
            user.Property(account => account.UserName).IsRequired().HasMaxLength(24);
            user.Property(account => account.NormalizedUserName).IsRequired().HasMaxLength(24);
            user.Property(account => account.DisplayName).IsRequired().HasMaxLength(50);
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }
}
