using Messenger.Application.Accounts;
using Messenger.Infrastructure.Accounts;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Messenger.Application.Chats;
using Messenger.Infrastructure.Chats;
using Messenger.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

var localDocker = builder.Environment.IsEnvironment("LocalDocker");
var cookiePolicy = localDocker ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

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
builder.Services.AddScoped<ChatNotifier>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = localDocker ? "Messenger.LocalDocker.Auth" : "Messenger.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookiePolicy;
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
    options.Cookie.SecurePolicy = cookiePolicy;
    if (localDocker)
    {
        options.Cookie.Name = "Messenger.LocalDocker.Csrf";
    }
});

builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache(); 
var app = builder.Build();

if (localDocker)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment() || localDocker)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (localDocker)
{
    app.UseStaticFiles();
}
else
{
    app.UseHttpsRedirection();
}

var webSocketOptions = new WebSocketOptions
{
    AllowedOrigins =
    {
        app.Configuration["SignalR:AllowedOrigin"] ??
        "https://localhost:7178"
    }
};

if (app.Environment.IsDevelopment())
{
    webSocketOptions.AllowedOrigins.Add("https://localhost:5173");
}

app.UseWebSockets(webSocketOptions);
app.UseAuthentication(); //читаем куки входа для опр текущ польз
app.UseAuthorization(); //может ли опр польз вызв выбр маршрут (если сессия истекла)

app.MapGet("/health", () => Results.Ok(new { service = "Messenger" }));
app.MapControllers();

app.MapHub<ChatHub>("/hubs/chat", options =>
{
    options.CloseOnAuthenticationExpiration = true;
});

if (localDocker)
{
    const string indexFile = "client/index.html";
    app.MapFallbackToFile("/", indexFile);
    app.MapFallbackToFile("/login", indexFile);
    app.MapFallbackToFile("/register", indexFile);
    app.MapFallbackToFile("/chats", indexFile);
    app.MapFallbackToFile("/chats/{chatId:guid}", indexFile);
}
else
{
    app.MapGet("/", () => Results.Ok(new { service = "Messenger" }));
}
app.Run();
