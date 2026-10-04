using FlowORM.Net.Abstractions;

namespace FlowORM.Net.Services;

/// <summary>Scoped tenant override used by FlowORM's executor and built-in providers.</summary>
/// <remarks>The executor sets this context before resolving its repository. It is not shared between scopes.</remarks>
public sealed class FlowOrmExecutionContext
{
    internal string? TenantOverride { get; set; }

    /// <summary>Wraps the configured tenant provider, falling back to it when no override is set.</summary>
    /// <param name="provider">The application's existing tenant provider.</param>
    /// <returns>A tenant provider observing only this execution scope.</returns>
    public ITenantProvider Wrap(ITenantProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new ScopedTenantProvider(this, provider);
    }

    private sealed class ScopedTenantProvider(FlowOrmExecutionContext context, ITenantProvider fallback) : ITenantProvider
    {
        public string? GetTenant() => context.TenantOverride ?? fallback.GetTenant();
    }
}
