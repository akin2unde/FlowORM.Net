#pragma warning disable CS1591
using System.Globalization;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Query;
using SimpleORM.Net.Models;
namespace SimpleORM.Net.Services;
/// <summary>Repository for entities defined and created at runtime.</summary>
public sealed class RuntimeDataRepository(IRuntimeDatabaseProvider provider, IDBTransactionManager transactions) : IRuntimeDataRepository
{
    public async Task<PagedResult<dynamic>> Select(string entity, SearchParam? search = null, int skip = 0, int limit = 100, CancellationToken ct = default, int? batch = null)
    {
        ValidateEntity(entity);
        if (skip < 0 || limit < 0) throw new ArgumentOutOfRangeException();
        var q = search?.Clone() ?? new SearchParam();
        var total = await provider.Count(entity, q, ct);
        var rows = await provider.Select(entity, q, skip, limit, ct);
        return new PagedResult<dynamic> { Data = rows.ToList(), TotalRecords = total, Skipped = skip, Limit = limit };
    }
    public Task<dynamic?> SelectSingle(string entity, SearchParam? search = null, CancellationToken ct = default)
    {
        ValidateEntity(entity);
        return provider.SelectSingle(entity, search?.Clone() ?? new SearchParam(), ct);
    }
    public async Task<dynamic> Save(string entity, IDictionary<string, object?> data, CancellationToken ct = default) => (await Save(entity, [data], ct))[0];
    public async Task<List<dynamic>> Save(string entity, IEnumerable<IDictionary<string, object?>> data, CancellationToken ct = default, int? batch = null)
    {
        ValidateEntity(entity);
        var list = data.ToList();
        if (list.Count == 0) return [];
        var size = Math.Min(batch ?? 100, 500);
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(batch));
        return await transactions.Execute(async () => { var result = new List<dynamic>(); for (var i=0;i<list.Count;i+=size) result.AddRange(await provider.Save(entity, list.Skip(i).Take(size).ToList(), transactions.Current!, ct)); return result; }, ct);
    }
    public Task Update(string entity, string code, IDictionary<string, object?> data, CancellationToken ct = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return transactions.Execute(() => provider.Update(entity, code, data, transactions.Current!, ct), ct);
    }
    public Task Delete(string entity, string code, CancellationToken ct = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return transactions.Execute(() => provider.Delete(entity, code, transactions.Current!, ct), ct);
    }
    public Task<long> Count(string entity, SearchParam? search = null, CancellationToken ct = default)
    {
        ValidateEntity(entity);
        return provider.Count(entity, search?.Clone() ?? new SearchParam(), ct);
    }
    public async Task<TValue> Sum<TValue>(string entity, string field, SearchParam? search = null, CancellationToken ct = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        var value = await provider.Sum(entity, field, search?.Clone() ?? new SearchParam(), ct);
        if (value is null || value is DBNull) return default!;
        return (TValue)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue), CultureInfo.InvariantCulture);
    }
    private static void ValidateEntity(string entity) => ArgumentException.ThrowIfNullOrWhiteSpace(entity);
}
