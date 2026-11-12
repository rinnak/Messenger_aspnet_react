using Messenger.Application.Accounts;
using Messenger.Infrastructure.Accounts;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Messenger.Application.Chats;
using Messenger.Infrastructure.Chats;
using Messenger.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Messenger")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Messenger for PostgreSQL.");

builder.Services.AddScoped<IDirectChatStore, DirectChatStore>();
builder.Services.AddScoped<OpenDirectChat>();
builder.Services.AddScoped<IMessageStore, MessageStore>();
builder.Services.AddScoped<SendMessage>();
builder.Services.AddDbContext<MessengerDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<MessengerUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<MessengerDbContext>()
    .AddSignInManager();
builder.Services.AddSignalR();
builder.Services.AddScoped<MessageNotifier>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Messenger.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAccountStore, IdentityAccountStore>();
builder.Services.AddScoped<RegisterAccount>();
builder.Services.AddControllers();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddSwaggerGen();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();
app.UseWebSockets(new WebSocketOptions
{
    AllowedOrigins = { app.Configuration["SignalR:AllowedOrigin"] ?? "https://localhost:7178" }
});
app.UseAuthentication(); //читаем куки входа для опр текущ польз
app.UseAuthorization(); //может ли опр польз вызв выбр маршрут (если сессия истекла)

app.MapGet("/", () => Results.Ok(new { service = "Messenger" }));
app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat", options =>
{
    options.CloseOnAuthenticationExpiration = true;
});

app.Run();
