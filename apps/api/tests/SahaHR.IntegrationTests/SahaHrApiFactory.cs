using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SahaHR.IntegrationTests;

/// Boots the real API against a throwaway Postgres container that applies the same db/init
/// scripts as dev (roles + schema + RLS + seed). The app connects as the RLS-bound sahahr_app
/// role, so isolation is exercised exactly as in production.
public sealed class SahaHrApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db;
    private string _appConnection = default!;
    private string _ownerConnection = default!;

    public SahaHrApiFactory()
    {
        var initDir = LocateInitDir();
        _db = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("sahahr_owner")
            .WithPassword("sahahr_dev_pw")
            .WithDatabase("sahahr")
            .WithEnvironment("SAHAHR_APP_PASSWORD", "sahahr_app_pw")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "01_init.sh")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "02_core_schema.sql")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "03_seed_dev.sql")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "04_ats_schema.sql")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "05_seed_ats.sql")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "06_notifications.sql")), "/docker-entrypoint-initdb.d/")
            .WithResourceMapping(new FileInfo(Path.Combine(initDir, "07_leave.sql")), "/docker-entrypoint-initdb.d/")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var host = _db.Hostname;
        var port = _db.GetMappedPublicPort(5432);
        _appConnection = $"Host={host};Port={port};Database=sahahr;Username=sahahr_app;Password=sahahr_app_pw";
        _ownerConnection = $"Host={host};Port={port};Database=sahahr;Username=sahahr_owner;Password=sahahr_dev_pw";

        // Override via environment variables: the env provider outranks appsettings and is in place
        // when Program.cs reads the connection string at registration (before builder.Build()).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _appConnection);
        Environment.SetEnvironmentVariable("ConnectionStrings__Migrator", _ownerConnection);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://test.sahahr.local");
        Environment.SetEnvironmentVariable("Jwt__Audience", "sahahr-api");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-must-be-at-least-32-characters-long!!");

        // Ensure init scripts (schema + seed) finished before any test runs.
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if (await OwnerScalarAsync("SELECT count(*) FROM tenant") >= 1) return;
            }
            catch { /* not ready yet */ }
            await Task.Delay(500);
        }
        throw new TimeoutException("Postgres init (schema + seed) did not complete in time.");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _appConnection,
                ["ConnectionStrings:Migrator"] = _ownerConnection,
                ["Jwt:Issuer"] = "https://test.sahahr.local",
                ["Jwt:Audience"] = "sahahr-api",
                ["Jwt:SigningKey"] = "test-signing-key-must-be-at-least-32-characters-long!!",
            });
        });
    }

    public async Task<long> OwnerScalarAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_ownerConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    /// Execute arbitrary SQL as the owner (RLS-exempt). Used by tests to inject fixtures or
    /// simulate a duplicate outbox delivery.
    public async Task OwnerExecAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_ownerConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// Polls OwnerScalarAsync(sql) until it reaches >= target or the timeout elapses; returns the
    /// last observed value. For asserting on the background outbox dispatcher's async effects.
    public async Task<long> PollScalarAsync(string sql, long target, int timeoutMs = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        long last = 0;
        while (DateTime.UtcNow < deadline)
        {
            last = await OwnerScalarAsync(sql);
            if (last >= target) return last;
            await Task.Delay(500);
        }
        return last;
    }

    /// All "table.column" pairs in the public schema — used by FF-18 to verify EF mappings
    /// resolve to columns that actually exist.
    public async Task<HashSet<string>> OwnerColumnsAsync()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        await using var conn = new NpgsqlConnection(_ownerConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema = 'public'";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            set.Add(reader.GetString(0));
        return set;
    }

    private static string LocateInitDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "db", "init");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"Could not locate db/init from {AppContext.BaseDirectory}");
    }
}
