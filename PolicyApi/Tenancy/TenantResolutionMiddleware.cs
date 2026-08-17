using Microsoft.EntityFrameworkCore;
using PolicyApi.Auth;
using PolicyApi.Data;

namespace PolicyApi.Tenancy;

/// <summary>
/// Layer 1 of the isolation strategy: resolve tenant context at the edge. The tenant identity is
/// read from the validated JWT <c>tid</c> claim (populated on <see cref="HttpContext.User"/> by the
/// authentication middleware), then a scoped <see cref="ITenantContext"/> is populated from it. This
/// is the production-shaped approach the earlier README anticipated: the identity comes from a
/// signed token claim, not a spoofable client header.
///
/// This is middleware rather than a minimal-API endpoint filter because an endpoint filter runs only
/// after the handler's arguments are bound, by which point the scoped <see cref="PolicyDbContext"/>
/// (whose constructor captures the tenant id) would already have been built and thrown its
/// fail-closed guard. Middleware runs before endpoint argument binding, so the tenant is resolved
/// first. It must be registered after UseAuthentication so the claims are available.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var path = context.Request.Path;

        // Non-API paths and the anonymous token endpoint have no tenant to resolve. The token
        // endpoint resolves its own context (it queries the unfiltered Tenants set to validate the
        // requested tenant), so it must be skipped here.
        if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/auth"))
        {
            await _next(context);
            return;
        }

        // Protected endpoints are already guaranteed authenticated by RequireAuthorization; the token
        // must also carry a usable tenant claim.
        var claim = context.User.FindFirst(TokenService.TenantClaimType)?.Value;
        if (!int.TryParse(claim, out var tenantId))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Token is missing a valid tenant claim.");
            return;
        }

        tenantContext.Resolve(tenantId);

        // Defence in depth: a token can outlive its tenant, so confirm the tenant still exists. The
        // Tenants set is not tenant-filtered, so this check is valid.
        var db = context.RequestServices.GetRequiredService<PolicyDbContext>();
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == tenantId, context.RequestAborted);
        if (!tenantExists)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unknown tenant.");
            return;
        }

        await _next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title) =>
        Results.Problem(statusCode: statusCode, title: title).ExecuteAsync(context);
}
