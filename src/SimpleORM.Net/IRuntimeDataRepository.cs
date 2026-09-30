#pragma warning disable CS1591
using SimpleORM.Net.Query;
using SimpleORM.Net.Models;

namespace SimpleORM.Net.Abstractions;

/// <summary>Dynamic counterpart of IDataRepository for runtime-created entities.</summary>
public interface IRuntimeDataRepository
{
    Task<PagedResult<dynamic>> Select(string entity, SearchParam? search = null, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null);
    Task<dynamic?> SelectSingle(string entity, SearchParam? search = null, CancellationToken cancellationToken = default);
    Task<dynamic> Save(string entity, IDictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task<List<dynamic>> Save(string entity, IEnumerable<IDictionary<string, object?>> data, CancellationToken cancellationToken = default, int? batch = null);
    Task Update(string entity, string code, IDictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task Delete(string entity, string code, CancellationToken cancellationToken = default);
    Task<long> Count(string entity, SearchParam? search = null, CancellationToken cancellationToken = default);
    Task<TValue> Sum<TValue>(string entity, string field, SearchParam? search = null, CancellationToken cancellationToken = default);
}
