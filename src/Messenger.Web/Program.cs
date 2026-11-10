using Messenger.Application.Accounts;
using Messenger.Infrastructure.Accounts;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Messenger")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Messenger for PostgreSQL.");

builder.Services.AddDbContext<MessengerDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<MessengerUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<MessengerDbContext>();

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { service = "Messenger" }));
app.MapControllers();

app.Run();
