using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Sample.Api;

/// <summary>Examples usable from workers or other services without capturing a request-scoped repository.</summary>
/// <param name="executor">The singleton executor registered by AddSimpleOrm.</param>
public sealed class ScopedExecutionExamples(ISimpleOrmExecutor executor)
{
    /// <summary>Reads a page without counting all matching customers.</summary>
    /// <param name="search">Filters and ordering.</param>
    /// <param name="tenant">Tenant to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A materialized page whose total is unknown.</returns>
    public Task<PagedResult<Customer>> ReadPage(SearchParam search, string tenant,
        CancellationToken cancellationToken = default) =>
        executor.Run((repo, token) => repo.Select<Customer>(search, includeTotal: false, limit: 500,
            cancellationToken: token), tenant, cancellationToken);

    /// <summary>Saves different model types in the same scoped transaction.</summary>
    /// <param name="customer">Customer to save.</param>
    /// <param name="inventory">Inventory to save.</param>
    /// <param name="tenant">Tenant to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SaveTogether(Customer customer, Inventory inventory, string tenant,
        CancellationToken cancellationToken = default) =>
        executor.Transaction(async (repo, token) =>
        {
            await repo.Save(customer, token);
            await repo.Save(inventory, token);
        }, tenant, cancellationToken);
}
