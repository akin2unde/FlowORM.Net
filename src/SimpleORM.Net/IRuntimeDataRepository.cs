#pragma warning disable CS1591
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Abstractions;

/// <summary>Dynamic counterpart of IDataRepository for runtime-created entities.</summary>
public interface IRuntimeDataRepository
{
    Task<PagedResult<dynamic>> Select(string entity, SearchParam? search = null, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null);
    /// <summary>Selects runtime records with optional total-count calculation. False leaves the total unknown.</summary>
    Task<PagedResult<dynamic>> Select(string entity, SearchParam? search, bool includeTotal, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null);
    /// <summary>Selects runtime records without query filters and with optional total-count calculation.</summary>
    Task<PagedResult<dynamic>> Select(string entity, bool includeTotal, int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null);
    Task<dynamic?> SelectSingle(string entity, SearchParam? search = null, CancellationToken cancellationToken = default);
    Task<dynamic> Save(string entity, IDictionary<string, object?> data, CancellationToken cancellationToken = default, bool upsert = false);
    Task<List<dynamic>> Save(string entity, IEnumerable<IDictionary<string, object?>> data, CancellationToken cancellationToken = default, int? batch = null, bool upsert = false);
    Task Update(string entity, string code, IDictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task<long> Update(string entity, SearchParam search, IDictionary<string, object?> data, CancellationToken cancellationToken = default);
    Task Delete(string entity, string code, CancellationToken cancellationToken = default);
    Task<long> Delete(string entity, SearchParam search, CancellationToken cancellationToken = default);
    Task<long> Count(string entity, SearchParam? search = null, CancellationToken cancellationToken = default);
    Task<TValue> Sum<TValue>(string entity, string field, SearchParam? search = null, CancellationToken cancellationToken = default);
}
