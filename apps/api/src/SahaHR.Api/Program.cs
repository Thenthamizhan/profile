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
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;       // keep "sub", "tenant_id", "perm" verbatim
        options.RequireHttpsMetadata = false;   // dev; real IdP serves HTTPS metadata
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
    });
builder.Services.AddAuthorization();
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
