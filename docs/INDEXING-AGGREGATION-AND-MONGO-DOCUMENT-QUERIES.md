# Indexing, aggregation and MongoDB document queries

This document describes the indexing, tenant-aware uniqueness, `Sum`, MongoDB nested/dictionary querying, `Upsert`, and full-connection-string behavior added to FlowORM.Net.

## Index and Unique

`[Index]` is a performance hint and permits duplicate values. `[Unique]` is a database constraint and rejects duplicate values. Both are synchronized by the provider's existing managed-index migration mechanism.

Single-column index:

```csharp
[Index]
public string Category { get; set; } = string.Empty;
```

Composite index:

```csharp
[Index("IX_Category_Created", Order = 1)]
public string Category { get; set; } = string.Empty;

[Index("IX_Category_Created", Order = 2, Direction = IndexDirection.Descending)]
public DateTime CreatedAt { get; set; }
```

Tenant-scoped models automatically prefix managed indexes with `Tenant`. Unique Code is `(Tenant, Code)` and `[Unique] Email` becomes `(Tenant, Email)`. `[Global]` models omit Tenant.

For SQL Server, new tenant-scoped tables use `(Tenant, Code)` as their primary key. Existing primary keys are only replaced when destructive migrations are enabled.

## Sum

`Count` was already part of `IDataRepository` and is unchanged. `Sum` adds server-side numeric aggregation:

```csharp
var value = await repository.Sum<Order, decimal>(
    order => order.Total,
    order => order.Status == OrderStatus.Completed,
    cancellationToken: ct);
```

The filter can also come from `SearchParam`. Tenant and soft-delete scopes continue to be applied by the provider.

## MongoDB nested fields and dictionaries

MongoDB accepts dotted document paths in `SearchParam`, for example `Attributes.Color` and `Address.City`. The same paths can be produced from expressions:

```csharp
x => x.Address.City == "Lagos"
x => x.Attributes["Color"] == "Black"
x => (int)x.Attributes["Storage"] >= 256
```

The shared expression resolver converts CLR member access and dictionary indexers to dotted paths. MongoDB supports those paths in filters, sort definitions, `SelectDynamic` projections and `Sum` fields. SQL Server rejects dotted document paths rather than interpreting them as arbitrary SQL identifiers.

## Upsert

`DBModel.Upsert` is currently implemented by the MongoDB provider for changed models. It sets `ReplaceOneModel.IsUpsert`, producing replace-if-found / insert-if-missing behavior. Because optimistic concurrency protects against last-write-wins updates, upsert is rejected when concurrency checking is enabled for that model. Use `[DisableConcurrencyCheck]` only where last-write-wins upsert is intentional.

## Full ConnectionString

`DatabaseConnectionOptions.ConnectionString` accepts a complete provider connection string. When non-empty it takes precedence over the individual Host/Port/DatabaseName/Username/Password/UseSsl/Options properties. SQL Server, SQL schema synchronization and MongoDB all honor the same precedence rule.
