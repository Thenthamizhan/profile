using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SahaHR.Common.Persistence;

namespace SahaHR.IntegrationTests;

/// FF-18 — Schema/model mapping integrity (AOM §6). Asserts that EVERY column the EF model maps
/// resolves to a column that actually exists in the database. Catches the class of bug where the
/// snake_case naming convention mis-derives a column name (e.g. ResumeS3Key -> "resume_s3key" when
/// the column is "resume_s3_key") — a real defect this suite caught once by accident; now it is
/// guarded for every entity at once.
[Collection(ApiCollection.Name)]
public sealed class SchemaMappingTests
{
    private readonly SahaHrApiFactory _factory;
    public SchemaMappingTests(SahaHrApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Every_mapped_column_exists_in_the_database()
    {
        var dbColumns = await _factory.OwnerColumnsAsync();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SahaHrDbContext>();

        var missing = new List<string>();
        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue; // keyless / non-table types

            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName(storeObject);
                if (column is null) continue;
                if (!dbColumns.Contains($"{table}.{column}"))
                    missing.Add($"{entity.ClrType.Name}.{prop.Name} -> {table}.{column} (no such column)");
            }
        }

        Assert.True(missing.Count == 0,
            "EF properties mapped to columns that do not exist:\n  " + string.Join("\n  ", missing));
    }
}
