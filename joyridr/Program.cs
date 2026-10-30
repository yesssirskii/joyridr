using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Joyridr.Common;
using Joyridr.Controllers;
using Joyridr.Data;
using Joyridr.Interfaces;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Joyridr.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
    options.AddPolicy(AuthController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// ---- API plumbing ----
// Enums go over the wire as camelCase strings ("joyRider", "awaitingPayment"), which map
// straight onto Swift enums with String raw values. Set for both MVC and the OpenAPI generator.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "JOYRIDR API";
        document.Info.Description = "Log in via /api/auth/login, then click Authorize and paste the accessToken.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };
        return Task.CompletedTask;
    });
    // Only endpoints that need a token get the lock icon in Swagger/Scalar.
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
        return Task.CompletedTask;
    });
});

// ---- App services ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
builder.Services.AddSingleton<IPushSender, LoggingPushSender>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<CarService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RideService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ChatService>();
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
    app.UseSwaggerUI(options =>  // and the classic Swagger UI at /swagger, same OpenAPI document
    {
        options.SwaggerEndpoint("/openapi/v1.json", "JOYRIDR API v1");
        options.DocumentTitle = "JOYRIDR API";
        options.EnablePersistAuthorization(); // keep the pasted token across page reloads
    });
}

app.MapControllers();

app.Run();

public partial class Program;
