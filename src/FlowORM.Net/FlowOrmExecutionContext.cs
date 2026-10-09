using System.Threading;
using FlowORM.Net.Abstractions;

namespace FlowORM.Net.Services;

/// <summary>Scoped execution state used by FlowORM's executor and repository.</summary>
/// <remarks>Execution flags are async-local so parallel operations in the same DI scope do not leak state into one another.</remarks>
public sealed class FlowOrmExecutionContext
{
    private readonly AsyncLocal<int> _saveWithoutTenantDepth = new();

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
