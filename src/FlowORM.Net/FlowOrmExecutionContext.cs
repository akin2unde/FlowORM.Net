using System.Threading;
using FlowORM.Net.Abstractions;

namespace FlowORM.Net.Services;

/// <summary>Scoped execution state used by FlowORM's executor and repository.</summary>
/// <remarks>Execution flags are async-local so parallel operations in the same DI scope do not leak state into one another.</remarks>
public sealed class FlowOrmExecutionContext
{
    private readonly AsyncLocal<int> _saveWithoutTenantDepth = new();
    private readonly AsyncLocal<ReadScopeState?> _readScope = new();

    /// <summary>True while an administrative read scope is active.</summary>
    public bool HasReadScope => _readScope.Value is not null;

    /// <summary>True when reading records from all tenants.</summary>
    public bool ReadAcrossTenants => _readScope.Value?.AcrossTenants == true;

    /// <summary>Tenant selected for administrative reads, if any.</summary>
    public string? ReadTenant => _readScope.Value?.Tenant;

    internal IDisposable BeginReadForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return BeginRead(new ReadScopeState(false, tenant));
    }

    internal IDisposable BeginReadAcrossTenants() => BeginRead(new ReadScopeState(true, null));

    private IDisposable BeginRead(ReadScopeState next)
    {
        var previous = _readScope.Value;
        _readScope.Value = next;
        return new ReadScopeHandle(this, previous);
    }

    private sealed record ReadScopeState(bool AcrossTenants, string? Tenant);

    private sealed class ReadScopeHandle(FlowOrmExecutionContext owner, ReadScopeState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._readScope.Value = previous;
        }
    }


    internal string? TenantOverride { get; set; }

    /// <summary>Gets whether the current asynchronous execution may save a tenant-scoped model without a tenant.</summary>
    public bool CanSaveWithoutTenant => _saveWithoutTenantDepth.Value > 0;

    /// <summary>Enters a scope that permits Save to persist a tenant-scoped model when no tenant can be resolved.</summary>
    /// <returns>A scope that restores normal tenant validation when disposed.</returns>
    internal IDisposable BeginSaveWithoutTenant()
    {
        _saveWithoutTenantDepth.Value++;
        return new SaveWithoutTenantScope(this);
    }

    /// <summary>Wraps the configured tenant provider, falling back to it when no override is set.</summary>
    /// <param name="provider">The application's existing tenant provider.</param>
    /// <returns>A tenant provider observing only this execution scope.</returns>
    public ITenantProvider Wrap(ITenantProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new ScopedTenantProvider(this, provider);
    }

    private sealed class SaveWithoutTenantScope(FlowOrmExecutionContext context) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            context._saveWithoutTenantDepth.Value = Math.Max(0, context._saveWithoutTenantDepth.Value - 1);
        }
    }

    private sealed class ScopedTenantProvider(FlowOrmExecutionContext context, ITenantProvider fallback) : ITenantProvider
    {
        public string? GetTenant() => context.TenantOverride ?? fallback.GetTenant();
    }
}
