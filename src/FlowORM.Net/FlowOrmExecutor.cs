using Microsoft.Extensions.DependencyInjection;
using FlowORM.Net.Abstractions;

namespace FlowORM.Net.Services;

/// <summary>Creates and disposes one repository scope per operation without building another container.</summary>
/// <param name="scopeFactory">Scope factory from the registered ORM service container.</param>
public sealed class FlowOrmExecutor(IServiceScopeFactory scopeFactory) : IFlowOrmExecutor
{
    /// <inheritdoc />
    public Task<TResult> Run<TResult>(Func<IDataRepository, CancellationToken, Task<TResult>> work,
        string? tenant = null, CancellationToken cancellationToken = default) =>
        Execute(work, tenant, false, cancellationToken);

    /// <inheritdoc />
    public Task Run(Func<IDataRepository, CancellationToken, Task> work,
        string? tenant = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Run(async (repo, token) => { await work(repo, token); return true; }, tenant, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> Transaction<TResult>(Func<IDataRepository, CancellationToken, Task<TResult>> work,
        string? tenant = null, CancellationToken cancellationToken = default) =>
        Execute(work, tenant, true, cancellationToken);

    /// <inheritdoc />
    public Task Transaction(Func<IDataRepository, CancellationToken, Task> work,
        string? tenant = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Transaction(async (repo, token) => { await work(repo, token); return true; }, tenant, cancellationToken);
    }

    private async Task<TResult> Execute<TResult>(Func<IDataRepository, CancellationToken, Task<TResult>> work,
        string? tenant, bool transactional, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (tenant is not null && string.IsNullOrWhiteSpace(tenant))
            throw new ArgumentException("Tenant override cannot be empty.", nameof(tenant));
        token.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<FlowOrmExecutionContext>().TenantOverride = tenant;
        var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();
        if (transactional)
        {
            var manager = scope.ServiceProvider.GetRequiredService<IDBTransactionManager>();
            return await manager.Execute(() => work(repository, token), token);
        }
        var result = await work(repository, token);
        token.ThrowIfCancellationRequested();
        return result;
    }
}
