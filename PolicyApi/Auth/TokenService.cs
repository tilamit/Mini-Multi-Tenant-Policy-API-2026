using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PolicyApi.Contracts;

namespace PolicyApi.Auth;

/// <summary>
/// Issues signed JWTs carrying the tenant id in a custom <c>tid</c> claim. The tenant-scoped API
/// trusts that claim (validated by the JWT signature) as the tenant identity, replacing the
/// spoofable header used earlier.
/// </summary>
public sealed class TokenService
{
    public const string TenantClaimType = "tid";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenResponse Create(int tenantId, string? subject)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [TenantClaimType] = tenantId,
                [JwtRegisteredClaimNames.Sub] = subject ?? $"tenant-{tenantId}",
            },
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResponse(token, "Bearer", _options.AccessTokenMinutes * 60);
    }
}
