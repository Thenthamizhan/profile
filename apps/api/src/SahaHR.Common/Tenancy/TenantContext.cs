namespace SahaHR.Common.Tenancy;

/// Per-request tenant + actor + permission set, resolved from the validated JWT (§4.2, §7).
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsAuthenticated { get; }

    /// Dot-namespaced permission check with wildcard support (`payroll.*`, `*`).
    bool Has(string permission);

    void Establish(Guid tenantId, Guid userId, IEnumerable<string> permissions);
}

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlySet<string> Permissions { get; private set; } = new HashSet<string>(StringComparer.Ordinal);
    public bool IsAuthenticated => TenantId is not null && UserId is not null;

    public bool Has(string permission)
    {
        if (Permissions.Contains("*") || Permissions.Contains(permission)) return true;
        var dot = permission.IndexOf('.');
        return dot > 0 && Permissions.Contains(string.Concat(permission.AsSpan(0, dot), ".*"));
    }

    public void Establish(Guid tenantId, Guid userId, IEnumerable<string> permissions)
    {
        TenantId = tenantId;
        UserId = userId;
        Permissions = permissions.ToHashSet(StringComparer.Ordinal);
    }
}
