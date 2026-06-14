using System.Net;
using System.Net.Sockets;
using System.Text;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
using CafeErezBetting.API.Hubs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.BackgroundServices;
using CafeErezBetting.Infrastructure.Data;
using CafeErezBetting.API.Services;
using CafeErezBetting.Infrastructure.Services;
using CafeErezBetting.Infrastructure.Services.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Redis ───────────────────────────────────────────────────────────────────
// AbortOnConnectFail=false: don't crash on startup when Redis is unavailable
// (required for CI jobs: Swagger gen + EF migrations run without Redis)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddStackExchangeRedisCache(options =>
    options.ConfigurationOptions = redisOptions);

// ─── HttpClient (for scraper) ────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("telesport", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    c.DefaultRequestHeaders.Add("Referer", "https://www.telesport.co.il/");
    c.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en;q=0.8");
    c.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
    c.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // m.telesport.co.il (mobile API) resolves to IPv6 which Docker bridge networks cannot route.
    // Force IPv4 and try all returned A records (some CDN edges may refuse HTTPS on certain IPs).
    ConnectCallback = async (ctx, ct) =>
    {
        var ipv4 = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
        if (ipv4.Length == 0)
            throw new InvalidOperationException($"No IPv4 address for {ctx.DnsEndPoint.Host}");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(ipv4, ctx.DnsEndPoint.Port, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }
});
builder.Services.AddHttpClient("livegames", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36");
    c.DefaultRequestHeaders.Add("Referer", "https://m.livegames.co.il/");
    c.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en;q=0.8");
    c.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
    c.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // m.livegames.co.il resolves to IPv6 which Docker bridge networks cannot route.
    // Force IPv4 and try all returned A records (some CDN edges may refuse HTTPS on certain IPs).
    ConnectCallback = async (ctx, ct) =>
    {
        var ipv4 = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
        if (ipv4.Length == 0)
            throw new InvalidOperationException($"No IPv4 address for {ctx.DnsEndPoint.Host}");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(ipv4, ctx.DnsEndPoint.Port, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }
});
builder.Services.AddHttpClient("winner", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    c.DefaultRequestHeaders.Add("Referer", "https://www.winner.co.il/");
    c.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en;q=0.8");
    c.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
    c.Timeout = TimeSpan.FromSeconds(15);
});

// ─── Domain services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IWinnerSyncService, WinnerScraperService>();
builder.Services.AddScoped<LivegamesApiClient>();
builder.Services.AddScoped<ITotoSyncService, TotoSyncService>();
builder.Services.AddScoped<TotoTelesportApiClient>();
builder.Services.AddScoped<TotoWinnerApiClient>();
builder.Services.AddScoped<IFormsService, FormsService>();
builder.Services.AddScoped<IMatchNotificationService, SignalRNotificationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<IProductService, ProductService>();

// ─── Background service ──────────────────────────────────────────────────────
builder.Services.AddHostedService<WinnerSyncHostedService>();
builder.Services.AddHostedService<TotoSyncHostedService>();

// ─── JWT Auth ────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = false,
            ValidateAudience = false,
            ClockSkew        = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path  = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.SecurityToken.Id;
                if (!string.IsNullOrEmpty(jti))
                {
                    var cache = ctx.HttpContext.RequestServices
                        .GetRequiredService<IDistributedCache>();
                    var blacklisted = await cache.GetStringAsync($"jwt:bl:{jti}");
                    if (blacklisted is not null)
                        ctx.Fail("Token revoked");
                }
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// ─── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── CORS ────────────────────────────────────────────────────────────────────
var allowedOrigins = (builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ─── Controllers + Swagger ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CafeErezBetting API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header, Name = "Authorization",
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }});
});

var app = builder.Build();

// ─── Middleware ───────────────────────────────────────────────────────────────
app.UseSerilogRequestLogging();

var swaggerOnly = bool.Parse(Environment.GetEnvironmentVariable("SWAGGER_ONLY") ?? "false");

if (app.Environment.IsDevelopment() || swaggerOnly)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CafeErezBetting API v1"));
}

if (app.Environment.IsDevelopment() && !swaggerOnly)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Seed default admin if none exist
        if (!await db.AdminUsers.AnyAsync())
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
                DisplayName = "מנהל",
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration/seeding skipped (no DB connection available)");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── Health check ────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// ─── SignalR hubs ─────────────────────────────────────────────────────────────
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHub<MatchesHub>("/hubs/matches");

app.Run();
