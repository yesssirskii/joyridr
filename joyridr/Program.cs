using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Admin;
using Joyridr.Features.Auth;
using Joyridr.Features.Cars;
using Joyridr.Features.Drivers;
using Joyridr.Features.Notifications;
using Joyridr.Features.Payments;
using Joyridr.Features.Rides;
using Joyridr.Features.Schedule;
using Joyridr.Features.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Database ----
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(config.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

// ---- Options ----
builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RideOptions>(config.GetSection(RideOptions.SectionName));
var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be set and at least 32 characters long.");

// ---- Auth ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub" and "role" as they are in the token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = TokenService.GetSigningKey(jwt),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Rider, p => p.RequireRole(nameof(UserRole.JoyRider)))
    .AddPolicy(Policies.Driver, p => p.RequireRole(nameof(UserRole.JoyDriver)))
    .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRole.Admin)));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitsPerMinute = config.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// ---- API plumbing ----
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums go over the wire as camelCase strings ("joyRider", "awaitingPayment"), which map
    // straight onto Swift enums with String raw values.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));

// ---- App services ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
builder.Services.AddSingleton<IPushSender, LoggingPushSender>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RideService>();
if (config.GetValue("BackgroundJobs:Enabled", true))
    builder.Services.AddHostedService<RideExpiryWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (config.GetValue<bool>("Database:MigrateOnStartup"))
        await db.Database.MigrateAsync();
    if (config.GetValue<bool>("Database:SeedDemoData"))
        await DevSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles(); // serves wwwroot/uploads
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive API docs at /scalar
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapDriverEndpoints();
app.MapScheduleEndpoints();
app.MapCarEndpoints();
app.MapRideEndpoints();
app.MapNotificationEndpoints();
app.MapAdminEndpoints();

app.Run();

public partial class Program;
