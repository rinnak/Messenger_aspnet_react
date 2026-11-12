using Messenger.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Messenger.Domain.Chats;

namespace Messenger.Infrastructure.Persistence;

public sealed class MessengerDbContext(DbContextOptions<MessengerDbContext> options)
    : IdentityUserContext<MessengerUser, Guid>(options)
{
    public DbSet<DirectChat> DirectChats => Set<DirectChat>();
    public DbSet<Message> Messages => Set<Message>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<DirectChat>(chat =>
        {
            chat.ToTable("DirectChats", table =>
                table.HasCheckConstraint("CK_DirectChats_UserOrder", "\"UserAId\" < \"UserBId\""));
            chat.HasKey(item => item.Id);
            chat.HasIndex(item => new {item.UserAId, item.UserBId}).IsUnique();
            chat.HasIndex(item => item.UserBId);
            chat.HasOne<MessengerUser>().WithMany().HasForeignKey(item => item.UserAId).OnDelete(DeleteBehavior.Restrict);
            chat.HasOne<MessengerUser>().WithMany().HasForeignKey(item => item.UserBId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MessengerUser>(user =>
        {
            user.ToTable("Users");
            user.Property(account => account.UserName).IsRequired().HasMaxLength(24);
            user.Property(account => account.NormalizedUserName).IsRequired().HasMaxLength(24);
            user.Property(account => account.DisplayName).IsRequired().HasMaxLength(50);
        });

        builder.Entity<Message>(message =>
        {
            message.ToTable("Messages");
            message.HasKey(item => item.Id); //серверный UUID - перв ключ
            message.Property(item => item.Text).HasMaxLength(200).IsRequired();
            message.HasIndex(item => new { item.ChatId, item.Sequence }).IsUnique();
            message.HasIndex(item => new { item.ChatId, item.SenderId, item.ClientMessageId }).IsUnique();
            message.HasOne<DirectChat>().WithMany().HasForeignKey(item => item.ChatId).OnDelete(DeleteBehavior.Restrict);
            message.HasOne<MessengerUser>().WithMany().HasForeignKey(item => item.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }
}
