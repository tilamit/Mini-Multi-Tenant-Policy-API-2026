using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PolicyApi.Auth;
using PolicyApi.Contracts;
using PolicyApi.Data;
using PolicyApi.Services;
using PolicyApi.Tenancy;
using PolicyApi.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PolicyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PolicyDb")));

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddSingleton<IClock, SystemClock>();

// --- Authentication / authorization ---------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep short claim names (tid, sub) instead of the default long URI mapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// --- Swagger, with a Bearer "Authorize" button ----------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mini Multi-Tenant Policy API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/auth/token (no 'Bearer ' prefix needed).",
    });

    // Microsoft.OpenApi v2: the requirement references the definition by id via the document.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>(),
    });
});

var app = builder.Build();

SeedDatabase(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Layer 1: resolve the tenant from the validated JWT claim, before any endpoint (and therefore any
// PolicyDbContext) is constructed. Registered after UseAuthentication so User claims are available.
app.UseMiddleware<TenantResolutionMiddleware>();

// Every /api endpoint requires a valid bearer token, except the token endpoint below.
var api = app.MapGroup("/api").RequireAuthorization();

// Token endpoint - the only anonymous endpoint, since you need it to obtain a token.
// SECURITY CAVEAT: this mints a token for any existing tenant with no credential check. It stands in
// for a real identity provider (which would verify credentials) purely so the API is testable here.
api.MapPost("/auth/token",
    async (TokenRequest request, ITenantContext tenantContext, TokenService tokens, HttpContext http, CancellationToken ct) =>
    {
        // Resolve the requested tenant so the (unfiltered) existence check can run without tripping
        // the fail-closed guard, then confirm the tenant exists before issuing a token.
        tenantContext.Resolve(request.TenantId);
        var db = http.RequestServices.GetRequiredService<PolicyDbContext>();
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == request.TenantId, ct);
        if (!tenantExists)
            return Results.NotFound($"Tenant {request.TenantId} does not exist.");

        return Results.Ok(tokens.Create(request.TenantId, request.Subject));
    })
    .AllowAnonymous();

// Customer's policy, tenant-scoped. Cross-tenant customer or no policy both return 404 (see README
// for the 404-vs-403 enumeration-leak reasoning).
api.MapGet("/customers/{customerId:int}/policy",
    async (int customerId, PolicyService service, CancellationToken ct) =>
    {
        var policy = await service.GetPolicyForCustomerAsync(customerId, ct);
        return policy is null ? Results.NotFound() : Results.Ok(policy);
    });

// Policies expiring within N days (default 30), tenant-scoped. Negative or absurd values are 400.
api.MapGet("/policies/expiring",
    async (int? withinDays, PolicyService service, CancellationToken ct) =>
    {
        var days = withinDays ?? 30;
        if (days < 0)
            return Results.BadRequest("'withinDays' must be zero or positive.");
        if (days > PolicyService.MaxWithinDays)
            return Results.BadRequest($"'withinDays' must be at most {PolicyService.MaxWithinDays}.");

        var policies = await service.GetExpiringPoliciesAsync(days, ct);
        return Results.Ok(policies);
    });

// Bonus: deterministic lookup - returns the stored expiration date verbatim or 404, never inferred.
api.MapGet("/policies/{policyNumber}/expiration-date",
    async (string policyNumber, PolicyService service, CancellationToken ct) =>
    {
        var result = await service.GetExpirationDateAsync(policyNumber, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    });

app.Run();

static void SeedDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    // Bootstrap scope: there is no HTTP request, so resolve a system sentinel tenant id. Only DDL
    // (EnsureCreated) and inserts run here, neither of which is affected by the tenant query
    // filters, so the sentinel is never used to read another tenant's data.
    services.GetRequiredService<ITenantContext>().Resolve(tenantId: 0);

    var db = services.GetRequiredService<PolicyDbContext>();
    db.Database.EnsureCreated();
    DataSeeder.Seed(db);
}

// Exposed so the test project's WebApplicationFactory can reference the entry point if desired.
public partial class Program;
