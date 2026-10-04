#pragma warning disable CS1591
using System.Globalization;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Models;
using SimpleORM.Net.Query;

namespace SimpleORM.Net.Services;

/// <summary>Repository for entities defined and created at runtime.</summary>
public sealed class RuntimeDataRepository(
    IRuntimeDatabaseProvider provider,
    IDBTransactionManager transactions) : IRuntimeDataRepository
{
    public Task<PagedResult<dynamic>> Select(string entity, SearchParam? search = null, int skip = 0, int limit = 100,
        CancellationToken cancellationToken = default, int? batch = null) =>
        Select(entity, search, true, skip, limit, cancellationToken, batch);

    public Task<PagedResult<dynamic>> Select(string entity, bool includeTotal, int skip = 0, int limit = 100,
        CancellationToken cancellationToken = default, int? batch = null) =>
        Select(entity, null, includeTotal, skip, limit, cancellationToken, batch);

    public async Task<PagedResult<dynamic>> Select(string entity, SearchParam? search, bool includeTotal,
        int skip = 0, int limit = 100, CancellationToken cancellationToken = default, int? batch = null)
    {
        ValidateEntity(entity);
        if (skip < 0 || limit < 0) throw new ArgumentOutOfRangeException();
        cancellationToken.ThrowIfCancellationRequested();
        var query = search?.Clone() ?? new SearchParam();
        var total = includeTotal ? await provider.Count(entity, query, cancellationToken) : 0;
        // Preserve the existing counted runtime read path. No-count reads use finite physical batches.
        if (includeTotal)
        {
            var selected = await provider.Select(entity, query, skip, limit, cancellationToken);
            return new PagedResult<dynamic> { Data = selected.ToList(), TotalRecords = total,
                TotalCalculated = true, Skipped = skip, Limit = limit };
        }
        var rows = new List<dynamic>();
        var size = BatchResolver.Resolve(batch, 100);
        var position = skip;
        long remaining = limit == 0 ? long.MaxValue : limit;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var take = (int)Math.Min(size, remaining);
            var page = await provider.Select(entity, query, position, take, cancellationToken);
            if (page.Count == 0) break;
            rows.AddRange(page);
            position = checked(position + page.Count);
            remaining -= page.Count;
            if (page.Count < take) break;
        }
        return new PagedResult<dynamic> { Data = rows, TotalRecords = 0, TotalCalculated = false,
            Skipped = skip, Limit = limit };
    }

    public Task<dynamic?> SelectSingle(
        string entity,
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        return provider.SelectSingle(entity, search?.Clone() ?? new SearchParam(), cancellationToken);
    }

    public async Task<dynamic> Save(
        string entity,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default,
        bool upsert = false)
    {
        var result = await Save(entity, [data], cancellationToken, null, upsert);
        return result[0];
    }

    public async Task<List<dynamic>> Save(
        string entity,
        IEnumerable<IDictionary<string, object?>> data,
        CancellationToken cancellationToken = default,
        int? batch = null,
        bool upsert = false)
    {
        ValidateEntity(entity);

        var list = data.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var size = Math.Min(batch ?? 100, 500);
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batch));
        }

        return await transactions.Execute(async () =>
        {
            var result = new List<dynamic>();

            for (var index = 0; index < list.Count; index += size)
            {
                var chunk = list.Skip(index).Take(size).ToList();
                var saved = await provider.Save(
                    entity,
                    chunk,
                    upsert,
                    transactions.Current!,
                    cancellationToken);

                result.AddRange(saved);
            }

            return result;
        }, cancellationToken);
    }

    public Task Update(
        string entity,
        string code,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return transactions.Execute(
            () => provider.Update(entity, code, data, transactions.Current!, cancellationToken),
            cancellationToken);
    }

    public Task<long> Update(
        string entity,
        SearchParam search,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        ArgumentNullException.ThrowIfNull(search);
        EnsureBulkSearch(search, "update");

        return transactions.Execute(
            () => provider.Update(entity, search.Clone(), data, transactions.Current!, cancellationToken),
            cancellationToken);
    }

    public Task Delete(
        string entity,
        string code,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return transactions.Execute(
            () => provider.Delete(entity, code, transactions.Current!, cancellationToken),
            cancellationToken);
    }

    public Task<long> Delete(
        string entity,
        SearchParam search,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        ArgumentNullException.ThrowIfNull(search);
        EnsureBulkSearch(search, "delete");

        return transactions.Execute(
            () => provider.Delete(entity, search.Clone(), transactions.Current!, cancellationToken),
            cancellationToken);
    }

    public Task<long> Count(
        string entity,
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        return provider.Count(entity, search?.Clone() ?? new SearchParam(), cancellationToken);
    }

    public async Task<TValue> Sum<TValue>(
        string entity,
        string field,
        SearchParam? search = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEntity(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        var value = await provider.Sum(
            entity,
            field,
            search?.Clone() ?? new SearchParam(),
            cancellationToken);

        if (value is null || value is DBNull)
        {
            return default!;
        }

        return (TValue)Convert.ChangeType(
            value,
            Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue),
            CultureInfo.InvariantCulture);
    }

    private static void EnsureBulkSearch(SearchParam search, string operation)
    {
        if (search.Filters.Count == 0)
        {
            throw new InvalidOperationException(
                $"Runtime bulk {operation} requires at least one filter. " +
                "This prevents accidentally changing every row in the entity.");
        }
    }

    private static void ValidateEntity(string entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
    }
}
