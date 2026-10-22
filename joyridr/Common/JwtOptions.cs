namespace Joyridr.Common;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "joyridr";
    public string Audience { get; set; } = "joyridr-ios";

    /// <summary>At least 32 characters. Never commit the production key; set it via user-secrets or an environment variable (Jwt__SigningKey).</summary>
    public string SigningKey { get; set; } = null!;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
