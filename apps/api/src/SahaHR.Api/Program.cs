using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SahaHR.Common.Auditing;
using SahaHR.Common.Eventing;
using SahaHR.Common.Modules;
using SahaHR.Common.Persistence;
using SahaHR.Common.Tenancy;
using SahaHR.Modules.Identity;
using SahaHR.Modules.People;
using SahaHR.Modules.Recruitment;
using SahaHR.Modules.Notifications;
using SahaHR.Modules.Leave;

var builder = WebApplication.CreateBuilder(args);

// Bind the platform-provided port (Render/Heroku/Cloud Run set $PORT) on all interfaces.
// Locally PORT is unset, so the default ASPNETCORE_URLS / launch profile applies unchanged.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --- modules (bounded contexts as in-process modules) ---
IModule[] modules = [new IdentityModule(), new PeopleModule(), new RecruitmentModule(), new NotificationsModule(), new LeaveModule()];
builder.Services.AddSingleton(new ModuleAssemblies(modules.Select(m => m.GetType().Assembly)));

// --- tenancy + persistence ---
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantConnectionInterceptor>();     // session GUC — direct endpoint
builder.Services.AddScoped<TenantTransactionInterceptor>();    // transaction-local GUC — pooler-safe

var appConnection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
builder.Services.AddDbContext<SahaHrDbContext>((sp, options) =>
{
    options.UseNpgsql(appConnection);
    options.UseSnakeCaseNamingConvention();
    options.AddInterceptors(
        sp.GetRequiredService<TenantConnectionInterceptor>(),
        sp.GetRequiredService<TenantTransactionInterceptor>());
});

// owner-role data source for RLS-exempt background work (outbox relay, dev-token lookup)
var ownerConnection = builder.Configuration.GetConnectionString("Migrator")
    ?? throw new InvalidOperationException("ConnectionStrings:Migrator is required.");
builder.Services.AddSingleton(new OwnerDataSource(NpgsqlDataSource.Create(ownerConnection)));

// --- platform services ---
builder.Services.AddScoped<IEventBus, OutboxEventBus>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddHostedService<OutboxDispatcher>();

// --- authentication / authorization ---
// Two modes, chosen by config:
//   * Jwt:Authority set   -> production OIDC. Access tokens are validated against the IdP's JWKS
//     (asymmetric). Fine-grained perms are resolved from our RBAC tables by the Identity module's
//     PermissionClaimsTransformation (the IdP token carries identity only). See docs/AUTH.md.
//   * Jwt:Authority unset -> dev/test. HS256 tokens minted by /v1/dev/token with a shared key.
var jwt = builder.Configuration.GetSection("Jwt");
var authority = jwt["Authority"];
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;       // keep "sub", "tenant_id", "perm" verbatim
        // HTTPS metadata is only relaxed outside production (dev/test over http).
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;       // OIDC discovery + JWKS fetched from the IdP
            options.Audience = jwt["Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = jwt["Audience"],
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                NameClaimType = "sub",
            };
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwt["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
                ValidateLifetime = true,
            };
        }
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();

foreach (var module in modules)
    module.Register(builder.Services, builder.Configuration);

var app = builder.Build();

app.MapOpenApi();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
foreach (var module in modules)
    module.MapEndpoints(app);

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
