#pragma warning disable CS1591
using SimpleORM.Net.Query;
namespace SimpleORM.Net.Abstractions;
/// <summary>Low-level provider contract for runtime entities.</summary>
public interface IRuntimeDatabaseProvider
{
    Task<IReadOnlyList<dynamic>> Select(string entity, SearchParam search, int skip, int limit, CancellationToken cancellationToken = default);
    Task<dynamic?> SelectSingle(string entity, SearchParam search, CancellationToken cancellationToken = default);
    Task<long> Count(string entity, SearchParam search, CancellationToken cancellationToken = default);
    Task<object?> Sum(string entity, string field, SearchParam search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<dynamic>> Save(string entity, IReadOnlyList<IDictionary<string, object?>> data, IDBTransaction transaction, CancellationToken cancellationToken = default);
    Task Update(string entity, string code, IDictionary<string, object?> data, IDBTransaction transaction, CancellationToken cancellationToken = default);
    Task Delete(string entity, string code, IDBTransaction transaction, CancellationToken cancellationToken = default);
}
