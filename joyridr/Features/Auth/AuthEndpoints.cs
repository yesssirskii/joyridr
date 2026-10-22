namespace Joyridr.Features.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/register", (RegisterRequest request, AuthService auth, CancellationToken ct) =>
                auth.RegisterAsync(request, ct))
            .WithSummary("Create an account as a JoyRider or JoyDriver. Returns tokens, so the user is logged in straight away.");

        group.MapPost("/login", (LoginRequest request, AuthService auth, CancellationToken ct) =>
            auth.LoginAsync(request, ct));

        group.MapPost("/refresh", (RefreshRequest request, AuthService auth, CancellationToken ct) =>
                auth.RefreshAsync(request.RefreshToken, ct))
            .WithSummary("Swap a refresh token for a new token pair. The old refresh token stops working.");

        group.MapPost("/logout", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(request.RefreshToken, ct);
            return Results.NoContent();
        });

        return app;
    }
}
