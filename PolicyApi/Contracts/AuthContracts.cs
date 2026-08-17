namespace PolicyApi.Contracts;

/// <summary>Request to mint a token for a tenant. See the token endpoint for the security caveat.</summary>
public record TokenRequest(int TenantId, string? Subject);

public record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);
