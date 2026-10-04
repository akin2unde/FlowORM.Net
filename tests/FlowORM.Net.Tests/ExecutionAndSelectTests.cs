#pragma warning disable CS1591
using System.Collections.Concurrent;
using System.Dynamic;
using Microsoft.Extensions.DependencyInjection;
using FlowORM.Net.Abstractions;
using FlowORM.Net.Configuration;
using FlowORM.Net.Metadata;
using FlowORM.Net.Models;
using FlowORM.Net.Query;
using FlowORM.Net.Services;

namespace FlowORM.Net.Tests;

public sealed class ExecutionAndSelectTests
{
    [Fact]
    public async Task ExistingSelectStillCounts()
    {
        var store = new ProbeStore(7);
        await using var services = Build(store);
        var result = await services.GetRequiredService<IFlowOrmExecutor>().Run((repo, token) => repo.Select<Customer>(limit: 3, cancellationToken: token));
        Assert.True(result.TotalCalculated);
        Assert.Equal(7, result.TotalRecords);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(1, store.CountCalls);
    }

    [Fact]
    public async Task NoCountSelectPreservesBatchingExtensionsAndDefaults()
    {
        var store = new ProbeStore(700);
        await using var services = Build(store);
        var result = await services.GetRequiredService<IFlowOrmExecutor>().Run((repo, token) =>
            repo.Select<Customer>(includeTotal: false, skip: 10, limit: 600, cancellationToken: token, batch: 1000));
        Assert.False(result.TotalCalculated);
        Assert.Equal(0, result.TotalRecords);
        Assert.Equal(600, result.Data.Count);
        Assert.Equal("0010", result.Data[0].Code);
        Assert.Equal(new[] { 500, 100 }, store.ReadLimits.ToArray());
        Assert.Equal(0, store.CountCalls);
        Assert.Equal(1, store.ExtensionLoads);
        Assert.All(result.Data, row => Assert.Equal(DataState.Unchanged, row.DataState));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    [InlineData(1000)]
    public async Task NoCountFetchAllStopsAtEnd(int count)
    {
        var store = new ProbeStore(count);
        await using var services = Build(store);
        var result = await services.GetRequiredService<IFlowOrmExecutor>().Run((repo, token) =>
            repo.Select<Customer>(includeTotal: false, limit: 0, cancellationToken: token, batch: 500));
        Assert.Equal(count, result.Data.Count);
        Assert.False(result.TotalCalculated);
        Assert.Equal(0, store.CountCalls);
        Assert.All(store.ReadLimits, limit => Assert.InRange(limit, 1, 500));
    }

    [Fact]
    public async Task ExpressionAndProjectionOverloadsAvoidCount()
    {
        var store = new ProbeStore(5);
        await using var services = Build(store);
        var executor = services.GetRequiredService<IFlowOrmExecutor>();
        var expression = await executor.Run((repo, token) => repo.Select<Customer>(row => row.Name == "Name0", includeTotal: false, cancellationToken: token));
        Assert.False(expression.TotalCalculated);
        Assert.Contains(store.LastSearch!.Filters, filter => filter.Field == nameof(Customer.Name));
        var combined = await executor.Run((repo, token) => repo.Select<Customer>(row => row.Name == "Name0", new SearchParam(), includeTotal: false, cancellationToken: token));
        Assert.False(combined.TotalCalculated);
        var projection = await executor.Run((repo, token) => repo.SelectDynamic<Customer>(new SearchParam { Fields = [nameof(Customer.Name)] }, includeTotal: false, cancellationToken: token));
        Assert.False(projection.TotalCalculated);
        Assert.Equal(5, projection.Data.Count);
        Assert.Equal(0, store.CountCalls);
    }

    [Fact]
    public async Task ConcurrentTenantsAreIsolatedAndIdentityFallbackIsPreserved()
    {
        var store = new ProbeStore(0);
        await using var services = Build(store);
        var executor = services.GetRequiredService<IFlowOrmExecutor>();
        Task<Customer?> Read(string tenant) => executor.Run(async (repo, token) =>
        { await Task.Yield(); return await repo.GetByCode<Customer>("A", token); }, tenant);
        var results = await Task.WhenAll(Read("TenantA"), Read("TenantB"));
        Assert.Equal("TenantA", results[0]!.Tenant);
        Assert.Equal("TenantB", results[1]!.Tenant);
        var inherited = await executor.Run((repo, token) => repo.GetByCode<Customer>("A", token));
        Assert.Equal("IdentityTenant", inherited!.Tenant);
        Assert.Equal(3, store.Disposals);
        Assert.Equal(3, store.Instances.Count);
    }

    [Fact]
    public async Task TransactionSharesOneManagerAndCommitsBothSaves()
    {
        var store = new ProbeStore(0);
        await using var services = Build(store);
        await services.GetRequiredService<IFlowOrmExecutor>().Transaction(async (repo, token) =>
        {
            await repo.Save(new Customer { Code = "A" }, token);
            await repo.Save(new Customer { Code = "B" }, token);
        }, "TenantA");
        Assert.Equal(1, store.Begins);
        Assert.Equal(1, store.Commits);
        Assert.Equal(0, store.Rollbacks);
        Assert.Equal(2, store.Committed.Count);
        Assert.All(store.Committed, row => Assert.Equal("TenantA", row.Tenant));
        Assert.Equal(1, store.Disposals);
    }

    [Fact]
    public async Task FailureRollsBackAndDisposesScope()
    {
        var store = new ProbeStore(0);
        await using var services = Build(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IFlowOrmExecutor>().Transaction(async (repo, token) =>
        {
            await repo.Save(new Customer { Code = "A" }, token);
            throw new InvalidOperationException("Expected failure");
        }, "TenantA"));
        Assert.Empty(store.Committed);
        Assert.Equal(1, store.Rollbacks);
        Assert.Equal(1, store.Disposals);
    }

    [Fact]
    public async Task CancellationDoesNotPreventRollback()
    {
        var store = new ProbeStore(0);
        await using var services = Build(store);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<IFlowOrmExecutor>().Transaction(async (repo, token) =>
        {
            await repo.Save(new Customer { Code = "A" }, token);
            cancellation.Cancel();
        }, "TenantA", cancellation.Token));
        Assert.Empty(store.Committed);
        Assert.Equal(1, store.Rollbacks);
        Assert.False(store.RollbackTokenWasCancelled);
        Assert.Equal(1, store.Disposals);
    }

    [Fact]
    public async Task PreCancelledRunDoesNotCreateScope()
    {
        var store = new ProbeStore(0);
        await using var services = Build(store);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<IFlowOrmExecutor>().Run(
            (repo, token) => repo.Select<Customer>(includeTotal: false, cancellationToken: token), cancellationToken: cancellation.Token));
        Assert.Empty(store.Instances);
    }

    [Fact]
    public async Task RuntimeNoCountReadKeepsFetchAllAndBatchCap()
    {
        var store = new ProbeStore(600);
        await using var services = Build(store);
        await using var scope = services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRuntimeDataRepository>();
        var result = await repo.Select("Customer", includeTotal: false, skip: 10, limit: 0, batch: 1000);
        Assert.False(result.TotalCalculated);
        Assert.Equal(590, result.Data.Count);
        Assert.Equal(0, store.CountCalls);
        Assert.Equal(new[] { 500, 500 }, store.ReadLimits.ToArray());
    }

    private static ServiceProvider Build(ProbeStore store)
    {
        var services = new ServiceCollection();
        services.AddFlowOrm(options => { options.Connection.ConnectionString = "mongodb://localhost/test"; options.MultiTenancy.Enabled = true; }, typeof(Customer).Assembly);
        services.AddFlowOrmDefaultIdentityProviders();
        services.AddSingleton(store);
        services.AddScoped<ITenantProvider>(_ => new IdentityTenant());
        services.AddScoped<IDatabaseProvider, ProbeProvider>();
        services.AddScoped<IRuntimeDatabaseProvider>(_ => new ProbeRuntimeProvider(store));
        services.AddScoped<IExtensionService>(_ => new ProbeExtensions(store));
        services.AddScoped<IAuditService>(_ => new ProbeAudit());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    private sealed class IdentityTenant : ITenantProvider { public string? GetTenant() => "IdentityTenant"; }
    private sealed class ProbeStore(int count)
    {
        public List<Customer> Rows { get; } = Enumerable.Range(0, count).Select(i => new Customer { Code = i.ToString("D4"), Name = "Name" + i }).ToList();
        public ConcurrentQueue<int> ReadLimits { get; } = new();
        public ConcurrentBag<ProbeProvider> Instances { get; } = [];
        public List<DBModel> Committed { get; } = [];
        public SearchParam? LastSearch;
        public int CountCalls, ExtensionLoads, Begins, Commits, Rollbacks, Disposals;
        public bool RollbackTokenWasCancelled;
    }
    private sealed class ProbeProvider : IDatabaseProvider, IAsyncDisposable
    {
        private readonly ProbeStore store;
        private readonly ITenantProvider tenant;
        public ProbeProvider(ProbeStore store, ITenantProvider tenant, FlowOrmExecutionContext context)
        { this.store = store; this.tenant = context.Wrap(tenant); store.Instances.Add(this); }
        public Task<long> Count<T>(SearchParam search, CancellationToken cancellationToken = default) where T : DBModel
        { Interlocked.Increment(ref store.CountCalls); return Task.FromResult((long)store.Rows.Count); }
        public Task<IReadOnlyList<T>> Select<T>(SearchParam search, int skip, int limit, CancellationToken cancellationToken = default) where T : DBModel
        { store.LastSearch = search; store.ReadLimits.Enqueue(limit); return Task.FromResult<IReadOnlyList<T>>(store.Rows.Skip(skip).Take(limit).Cast<T>().ToArray()); }
        public Task<IReadOnlyList<dynamic>> SelectDynamic<T>(SearchParam search, int skip, int limit, CancellationToken cancellationToken = default) where T : DBModel
        { return Task.FromResult<IReadOnlyList<dynamic>>(store.Rows.Skip(skip).Take(limit).Select(row => { IDictionary<string, object?> value = new ExpandoObject(); value["Name"] = row.Name; return (dynamic)value; }).ToArray()); }
        public Task<T?> GetByCode<T>(string code, CancellationToken cancellationToken = default) where T : DBModel => Task.FromResult<T?>((T)(DBModel)new Customer { Code = code, Tenant = tenant.GetTenant() });
        public Task<IDBTransaction> BeginTransaction(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref store.Begins); return Task.FromResult<IDBTransaction>(new ProbeTransaction(store)); }
        public Task Insert<T>(IReadOnlyList<T> models, IDBTransaction transaction, CancellationToken cancellationToken = default) where T : DBModel
        { ((ProbeTransaction)transaction).Pending.AddRange(models); return Task.CompletedTask; }
        public Task Update<T>(IReadOnlyList<T> models, IDBTransaction transaction, CancellationToken cancellationToken = default) where T : DBModel => throw new NotSupportedException();
        public Task Delete<T>(IReadOnlyList<T> models, bool hardDelete, IDBTransaction transaction, CancellationToken cancellationToken = default) where T : DBModel => throw new NotSupportedException();
        public Task<T?> SelectSingle<T>(SearchParam search, CancellationToken cancellationToken = default) where T : DBModel => throw new NotSupportedException();
        public Task<object?> Sum<T>(string field, SearchParam search, CancellationToken cancellationToken = default) where T : DBModel => throw new NotSupportedException();
        public Task<long> DeleteStale(DBModelMetadata metadata, DateTime olderThanUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GenerateDebugQuery<T>(SearchParam search, int skip = 0, int limit = 100) where T : DBModel => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Interlocked.Increment(ref store.Disposals); return ValueTask.CompletedTask; }
    }
    private sealed class ProbeTransaction(ProbeStore store) : IDBTransaction
    {
        public List<DBModel> Pending { get; } = [];
        public Task Commit(CancellationToken cancellationToken = default)
        { store.Committed.AddRange(Pending); Interlocked.Increment(ref store.Commits); return Task.CompletedTask; }
        public Task Rollback(CancellationToken cancellationToken = default)
        { store.RollbackTokenWasCancelled = cancellationToken.IsCancellationRequested; Pending.Clear(); Interlocked.Increment(ref store.Rollbacks); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class ProbeExtensions(ProbeStore store) : IExtensionService
    {
        public Task Load<T>(IReadOnlyList<T> models, CancellationToken cancellationToken = default) where T : DBModel
        { Interlocked.Increment(ref store.ExtensionLoads); return Task.CompletedTask; }
        public Task Save<T>(IReadOnlyList<T> models, IDBTransaction transaction, CancellationToken cancellationToken = default) where T : DBModel => Task.CompletedTask;
    }
    private sealed class ProbeAudit : IAuditService
    { public Task Write<T>(string action, IReadOnlyList<T> models, IDBTransaction transaction, CancellationToken cancellationToken = default) where T : DBModel => Task.CompletedTask; }
    private sealed class ProbeRuntimeProvider(ProbeStore store) : IRuntimeDatabaseProvider
    {
        public Task<IReadOnlyList<dynamic>> Select(string entity, SearchParam search, int skip, int limit, CancellationToken cancellationToken = default)
        { store.ReadLimits.Enqueue(limit); return Task.FromResult<IReadOnlyList<dynamic>>(store.Rows.Skip(skip).Take(limit == 0 ? int.MaxValue : limit).Select(row => (dynamic)new Dictionary<string, object?> { ["Code"] = row.Code }).ToArray()); }
        public Task<long> Count(string entity, SearchParam search, CancellationToken cancellationToken = default)
        { store.CountCalls++; return Task.FromResult((long)store.Rows.Count); }
        public Task<dynamic?> SelectSingle(string entity, SearchParam search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> Sum(string entity, string field, SearchParam search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<dynamic>> Save(string entity, IReadOnlyList<IDictionary<string, object?>> data, bool upsert, IDBTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Update(string entity, string code, IDictionary<string, object?> data, IDBTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> Update(string entity, SearchParam search, IDictionary<string, object?> data, IDBTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Delete(string entity, string code, IDBTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> Delete(string entity, SearchParam search, IDBTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

}
