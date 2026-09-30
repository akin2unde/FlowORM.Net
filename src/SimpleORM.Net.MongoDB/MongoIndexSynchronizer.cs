using MongoDB.Bson;
using MongoDB.Driver;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Attributes;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;

namespace SimpleORM.Net.MongoDB;

/// <summary>
/// Synchronizes only SimpleORM-managed MongoDB indexes.
/// MongoDB document fields themselves do not require relational-style migrations.
/// </summary>
public sealed class MongoIndexSynchronizer : IDBSchemaSynchronizer
{
    private const string ManagedIndexPrefix = "SORM_";

    private readonly MongoDatabaseProvider _provider;
    private readonly IDBMetadataProvider _metadata;
    private readonly SimpleOrmOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoIndexSynchronizer"/> class.
    /// </summary>
    public MongoIndexSynchronizer(
        MongoDatabaseProvider provider,
        IDBMetadataProvider metadata,
        SimpleOrmOptions options)
    {
        _provider = provider;
        _metadata = metadata;
        _options = options;
    }

    /// <inheritdoc />
    public async Task Synchronize(
        CancellationToken cancellationToken = default)
    {
        if (!_options.AutoMigration)
        {
            return;
        }

        foreach (var model in _metadata.GetRegisteredModels())
        {
            var collection = _provider.Database.GetCollection<BsonDocument>(
                model.TableName);

            var desiredIndexes = BuildDesiredIndexes(model);
            var existingIndexes = await ReadManagedIndexNames(
                collection,
                cancellationToken);

            foreach (var desired in desiredIndexes)
            {
                if (existingIndexes.Contains(
                        desired.Name,
                        StringComparer.Ordinal))
                {
                    continue;
                }

                var keys = Builders<BsonDocument>.IndexKeys.Combine(
                    desired.Columns.Select(column => column.Direction == IndexDirection.Descending
                        ? Builders<BsonDocument>.IndexKeys.Descending(column.Name)
                        : Builders<BsonDocument>.IndexKeys.Ascending(column.Name)));

                var index = new CreateIndexModel<BsonDocument>(
                    keys,
                    new CreateIndexOptions
                    {
                        Name = desired.Name,
                        Unique = desired.Unique
                    });

                await collection.Indexes.CreateOneAsync(
                    index,
                    cancellationToken: cancellationToken);
            }

            foreach (var existing in existingIndexes)
            {
                if (desiredIndexes.Any(
                        desired => desired.Name.Equals(
                            existing,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                await collection.Indexes.DropOneAsync(
                    existing,
                    cancellationToken);
            }
        }
    }

    private sealed record DesiredIndex(string Name, bool Unique, IReadOnlyList<(string Name, IndexDirection Direction)> Columns);

    private static IReadOnlyList<DesiredIndex> BuildDesiredIndexes(DBModelMetadata model)
    {
        var indexes = new List<DesiredIndex>();
        var tenantPrefix = model.TenantScoped && model.TenantColumn is not null
            ? new[] { (model.TenantColumn.ColumnName, IndexDirection.Ascending) }
            : Array.Empty<(string, IndexDirection)>();
        var tenantName = model.TenantScoped && model.TenantColumn is not null
            ? $"_{NormalizeName(model.TenantColumn.ColumnName)}"
            : string.Empty;

        // Code is unique inside a tenant; global models keep globally unique Code.
        indexes.Add(new DesiredIndex(
            $"{ManagedIndexPrefix}UQ_{NormalizeName(model.TableName)}{tenantName}_Code",
            true,
            tenantPrefix.Concat(new[] { (model.CodeColumn.ColumnName, IndexDirection.Ascending) }).DistinctBy(item => item.Item1, StringComparer.OrdinalIgnoreCase).ToArray()));

        foreach (var column in model.PersistedColumns.Where(column => column.Unique && !column.IsCode).Where(column => string.IsNullOrWhiteSpace(column.UniqueGroup)))
        {
            indexes.Add(new DesiredIndex(
                $"{ManagedIndexPrefix}UQ_{NormalizeName(model.TableName)}{tenantName}_{NormalizeName(column.ColumnName)}",
                true,
                tenantPrefix.Concat(new[] { (column.ColumnName, IndexDirection.Ascending) }).DistinctBy(item => item.Item1, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        foreach (var group in model.PersistedColumns.Where(column => column.Unique && !column.IsCode).Where(column => !string.IsNullOrWhiteSpace(column.UniqueGroup)).GroupBy(column => column.UniqueGroup!, StringComparer.OrdinalIgnoreCase))
        {
            indexes.Add(new DesiredIndex(
                $"{ManagedIndexPrefix}UQ_{NormalizeName(model.TableName)}{tenantName}_{NormalizeName(group.Key)}",
                true,
                tenantPrefix.Concat(group.Select(column => (column.ColumnName, IndexDirection.Ascending))).DistinctBy(item => item.Item1, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        var indexed = model.PersistedColumns.SelectMany(column =>
            column.Property.GetCustomAttributes(typeof(IndexAttribute), true).Cast<IndexAttribute>().Select(attribute => (column, attribute))).ToArray();

        foreach (var item in indexed.Where(item => string.IsNullOrWhiteSpace(item.attribute.Name)))
        {
            indexes.Add(new DesiredIndex(
                $"{ManagedIndexPrefix}IX_{NormalizeName(model.TableName)}{tenantName}_{NormalizeName(item.column.ColumnName)}",
                false,
                tenantPrefix.Concat(new[] { (item.column.ColumnName, item.attribute.Direction) }).DistinctBy(value => value.Item1, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        foreach (var group in indexed.Where(item => !string.IsNullOrWhiteSpace(item.attribute.Name)).GroupBy(item => item.attribute.Name!, StringComparer.OrdinalIgnoreCase))
        {
            indexes.Add(new DesiredIndex(
                $"{ManagedIndexPrefix}IX_{NormalizeName(model.TableName)}{tenantName}_{NormalizeName(group.Key)}",
                false,
                tenantPrefix.Concat(group.OrderBy(item => item.attribute.Order).Select(item => (item.column.ColumnName, item.attribute.Direction))).DistinctBy(value => value.Item1, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        return indexes;
    }

    private static async Task<IReadOnlyList<string>> ReadManagedIndexNames(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(
            cancellationToken);

        var indexes = await cursor.ToListAsync(
            cancellationToken);

        return indexes
            .Where(index => index.Contains("name"))
            .Select(index => index["name"].AsString)
            .Where(name => name.StartsWith(
                ManagedIndexPrefix,
                StringComparison.Ordinal))
            .ToArray();
    }

    private static string NormalizeName(string value)
    {
        return new string(
            value.Select(
                    character => char.IsLetterOrDigit(character) || character == '_'
                        ? character
                        : '_')
                .ToArray());
    }
}
