namespace PolicyApi.Contracts;

// Purpose-built response DTOs, never EF entities. This prevents accidental over-exposure of
// navigation properties or internal fields. Note that TenantId never appears in any response.

public record PolicyResponse(
    int PolicyId,
    int CustomerId,
    string PolicyNumber,
    DateOnly ExpirationDate,
    decimal PremiumAmount);

public record ExpiringPolicyResponse(
    int PolicyId,
    int CustomerId,
    string PolicyNumber,
    DateOnly ExpirationDate);

public record ExpirationDateResponse(
    string PolicyNumber,
    DateOnly ExpirationDate);
