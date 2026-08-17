namespace PolicyApi.Auth;

/// <summary>
/// JWT settings bound from the "Jwt" configuration section. The signing key lives in appsettings
/// only as a development convenience; in production it would come from a secret store (and issuance
/// would be handled by a real identity provider, not this app).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 60;
}
