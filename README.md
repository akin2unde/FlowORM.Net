# SimpleORM.Net
[![Build](https://github.com/akin2unde/SimpleORM.Net/actions/workflows/build.yml/badge.svg)](https://github.com/akin2unde/SimpleORM.Net/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SimpleORM.Net.svg)](https://www.nuget.org/packages/SimpleORM.Net)
[![Downloads](https://img.shields.io/nuget/dt/SimpleORM.Net.svg)](https://www.nuget.org/packages/SimpleORM.Net)
[![License](https://img.shields.io/github/license/akin2unde/SimpleORM.Net)](LICENSE)

A lightweight, provider-based ORM for .NET 10 focused on a small repository API, model conventions, batching, transactions, extensions, multi-tenancy, auditing and provider-specific schema management.

**Author:** Akintunde Morakinyo  
**Repository:** `akin2unde/SimpleORM.Net`

## Packages

- `SimpleORM.Net` — core repository, metadata, query model and transaction abstractions.
- `SimpleORM.Net.SqlServer` — SQL Server provider and schema synchronization.
- `SimpleORM.Net.MongoDB` — MongoDB provider and index synchronization.
- `SimpleORM.Net.AspNetCore` — ASP.NET Core tenant/user integration, error middleware and payload encryption.
- `SimpleORM.Net.Http` — typed HTTP request wrapper.


## Quick start

```csharp
builder.Services.AddSimpleOrm(
    options =>
    {
        options.Database = DatabaseType.SqlServer;
        options.Connection.Host = "localhost";
        options.Connection.Port = 1433;
        options.Connection.Database = "CommerceDb";
        options.Connection.Username = "sa";
        options.Connection.Password = configuration["SimpleOrm:Password"];
        options.DefaultStringLength = 50;
        options.CodeGeneration.Length = 10;
        options.Batch.Save = 100;
        options.Batch.Select = 100;
        options.Concurrency.Enabled = true; // default
        options.AutoMigration = true;
    },
    typeof(Product).Assembly);

// Provider registration intentionally remains explicit.
builder.Services.AddSimpleOrmSqlServer();
builder.Services.AddSimpleOrmAspNetCore();
```

For MongoDB, set `options.Database = DatabaseType.MongoDb` and call `AddSimpleOrmMongoDB()`.

## Models and code generation

Every persisted model inherits `DBModel`. A table/collection is inferred automatically; `[DBTable]` is only needed to override its database name.

```csharp
public sealed class Product : DBModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public override string GetPrefix() => "PRD";
}
```

Every model instance can generate a code:

```csharp
var product = new Product();
var code = product.GenerateCode();       // PRD-XXXXXXXXXX
var shortCode = product.GenerateCode(6); // PRD-XXXXXX
```

When `Code` is empty during an insert, the repository calls the model's `GenerateCode` using the configured/model metadata length. `Code` is unique by convention.

## Optimistic concurrency

SimpleORM protects updates and deletes from lost updates by default. Every `DBModel` has a `Version` managed by the ORM. The database mutation matches the version originally loaded and increments it atomically when the write succeeds. No separate pre-read is added to the normal write path.

```csharp
var product = await repository.GetByCode<Product>("PRD-001", ct);
product!.Price = 120;
product.DataState = DataState.Changed;
await repository.Save(product, ct);
```

If another request changed the same record after it was loaded, `Save` throws `DBConcurrencyException`. For detached/API update models, round-trip the `Version` value returned by the read; omitting or changing it can correctly produce a conflict once the stored record has advanced. With the optional ASP.NET Core error middleware enabled, this exception is returned as HTTP `409 Conflict`.

Concurrency protection is enabled globally by default and can be configured explicitly:

```csharp
options.Concurrency.Enabled = true;
```

Models that intentionally use last-write-wins behavior can opt out:

```csharp
[DisableConcurrencyCheck]
public sealed class TelemetryLog : DBModel
{
    public string Message { get; set; } = string.Empty;
}
```

SQL Server keeps bulk performance by checking `Version` in the existing staging-table join. MongoDB includes `Version` in each bulk-write filter. SQL auto-migration seeds existing rows with version `1`; older MongoDB documents without a version are treated as version `1` on their first protected mutation.

## Repository API

Inject one repository for all models:

```csharp
public sealed class ProductService(IDataRepository repository)
{
    public Task<Product?> Get(string code, CancellationToken ct) =>
        repository.GetByCode<Product>(code, ct);
}
```

### Select

```csharp
var page = await repository.Select<Product>(
    skip: 0,
    limit: 100,
    cancellationToken: ct,
    batch: 100);

var active = await repository.Select<Product>(
    x => x.Active,
    skip: 0,
    limit: 100,
    cancellationToken: ct);
```

`limit: 0` means fetch all matching records. Physical batches are capped internally at 500.

For joins, selected fields, ordering and richer filtering, pass `SearchParam` or combine it with an expression.

### SelectSingle

```csharp
var product = await repository.SelectSingle<Product>(
    x => x.Code == code,
    ct);
```

### Search

Strings are searchable by convention unless `[NotSearchable]` is applied.

```csharp
var result = await repository.Search<Product>(
    "milk",
    cancellationToken: ct);
```

### Count

```csharp
long total = await repository.Count<Product>(cancellationToken: ct);
long active = await repository.Count<Product>(x => x.Active, cancellationToken: ct);
```

### Save and DataState

One `Save` API handles insert, update and delete through `DataState`.

```csharp
product.DataState = DataState.New;
await repository.Save(product, ct);

product.Price = 2500;
product.DataState = DataState.Changed;
await repository.Save(product, ct);

product.DataState = DataState.Removed;
await repository.Save(product, ct);
```

Models use soft delete by default. Apply `[HardDelete]` to models that must be physically deleted.

Batch save places `CancellationToken` before the optional batch parameter:

```csharp
await repository.Save(products, ct, batch: 200);
```

## Transactions

Normal saves manage their transaction automatically. Use `IDBTransactionManager.Execute` when several repository operations must commit or roll back together.

```csharp
return await transactionManager.Execute<IReadOnlyList<Product>>(
    async () =>
    {
        var savedProducts = await repository.Save(products, ct);

        if (inventories.Count > 0)
        {
            await repository.Save(inventories, ct);
        }

        return savedProducts;
    },
    ct);
```

Nested repository calls reuse the current scoped transaction; they do not independently commit it.

## SearchParam, joins and selected fields

`SearchParam` supports filters, ordering, join type and selected fields. Expression filters can be used alone or merged with a `SearchParam`.

```csharp
var result = await repository.Select<Order>(
    x => x.Total > 1000,
    searchParam,
    skip: 0,
    limit: 100,
    cancellationToken: ct);
```

## Debug queries

Generate a provider-specific query with values embedded for debugging only:

```csharp
var query = repository.GenerateDebugQuery<Product>(searchParam);
```

The generated text is for inspection and is never used as the execution path.

## Extensions

Apply `[Extendable]` to models that support dynamic extension definitions. `DBModel.Extended` contains loaded extension values. Definitions describe field code/name, data type, required state, size and default value.

`options.Extensions.RequirePublish = false` makes saved definitions immediately available. Set it to `true` to require explicit publishing.

The sample API contains end-to-end definition, publishing, loading and saving examples.

## Ignored properties

Use `[Ignore]` for model properties that belong to runtime/application state but must never be persisted:

```csharp
public sealed class Country : DBModel
{
    public string Name { get; set; } = string.Empty;

    [Ignore]
    public string? DisplayLabel { get; set; }
}
```

Ignored properties are excluded from SQL Server schema generation, selects, inserts, updates, filters, joins, and ordering. MongoDB also omits ignored members from BSON persistence. Existing ignored SQL columns are only physically removed when destructive migrations are enabled.

## Multi-tenancy

Enable tenant filtering globally:

```csharp
options.MultiTenancy.Enabled = true;
options.MultiTenancy.JwtClaim = "tenant";
```

ASP.NET Core can resolve the tenant from the configured JWT claim. Tenant behavior remains optional.

Use `[Global]` for shared models that must not require or persist a tenant even when application multi-tenancy is enabled:

```csharp
[Global]
public sealed class Country : DBModel
{
    public string Name { get; set; } = string.Empty;
}
```

Normal models remain tenant scoped. Global models skip tenant filters on read/update/delete and the inherited `Tenant` property is excluded from persistence. Typical uses include tenant records themselves and shared reference data such as countries.

## Audit and error logging

Audit trails are opt-in globally and can be disabled per model. Error logging middleware is also optional and can persist useful failure context such as request URL and payload information where available.

Stale error logs can be physically removed on a UTC cron schedule:

```csharp
options.ErrorLog.Enabled = true;
options.ErrorLog.AutoDeleteEnabled = true;
options.ErrorLog.RetentionDays = 60;
options.ErrorLog.CleanupCron = "0 0 1 */3 *"; // every quarter
```

The cleanup above runs every three months and deletes error-log records whose `CreatedAt` is older than 60 days. A monthly schedule can use `0 0 1 * *`. Cleanup bypasses tenant scoping because retention is system-level maintenance.

Any model can opt into the same retention mechanism:

```csharp
[AutoDelete(60, "0 0 1 * *")]
public sealed class TemporaryImport : DBModel
{
}
```


## Enum and string conventions

```csharp
options.DefaultStringLength = 50;
options.EnumStorage = EnumStorage.String;
```

Individual model attributes can override supported conventions. Password/sensitive return values can use `[DefaultOnReturn]` so their values are reset after materialization.

## Schema management

SQL Server auto-migration synchronizes supported table, column, key and index changes and records migration failures. MongoDB intentionally avoids relational-style migrations and synchronizes indexes instead.

## Raw provider queries

`IDBQuery` is available for advanced provider-specific direct queries and supports dynamic or typed result shapes. Prefer `IDataRepository` for normal application CRUD.

## Bulk-write performance

SQL Server batch inserts use `SqlBulkCopy`. Batch updates and deletes stage rows into a temporary table and apply one set-based statement per SimpleORM batch, reducing per-row database round-trips. MongoDB continues to use native bulk writes. For performance comparisons, use the same batch size for both providers; `500` is the maximum SimpleORM physical batch size.

See `docs/PERFORMANCE-AND-CORRECTNESS-2026-09-04.md` for the changes made after the 1,000,000-record Docker benchmark and the next-run guidance.

## Sample project

`samples/SimpleORM.Net.Sample.Api` demonstrates:

- Controller → application service → `IDataRepository`
- expression and `SearchParam` selects
- `SelectSingle`, `GetByCode`, search and count
- single and batch saves
- `DataState`
- transactions across multiple model types
- extensions and publishing
- debug query generation
- SQL Server/MongoDB provider selection
- ASP.NET Core integration

## Build and test

```bash
dotnet restore SimpleORM.Net.slnx
dotnet build SimpleORM.Net.slnx -c Release
dotnet test SimpleORM.Net.slnx -c Release
```

XML documentation and warnings-as-errors are enabled repository-wide.

## NuGet publishing

GitHub Actions includes:

- `.github/workflows/build.yml` — restore, build and test pushes/PRs.
- `.github/workflows/nuget.yml` — build, test, pack and publish tags matching `v*`.

Create a GitHub Actions secret named `NUGET_API_KEY`, then push a version tag such as `v0.1.0` to run the publishing workflow.

## Roadmap

Future phases are intended to add more providers and provider-neutral data movement between database types without changing application models or the repository CRUD API.

## License

MIT. See `LICENSE`.

## Full connection strings

`Connection.ConnectionString` can be used when the application already owns a complete provider connection string. When it is supplied, the SQL Server and MongoDB providers use it directly instead of rebuilding a connection from `Host`, `Port`, `DatabaseName`, `Username`, `Password`, `UseSsl` and `Options`.

```csharp
builder.Services.AddSimpleOrm(options =>
{
    options.Database = DatabaseType.SqlServer;
    options.Connection.ConnectionString = configuration.GetConnectionString("CommerceDb");
});
```

MongoDB works the same way:

```csharp
options.Database = DatabaseType.MongoDb;
options.Connection.ConnectionString =
    "mongodb://user:password@localhost:27017/commerce";
```

Use either the full `ConnectionString` or the individual connection properties. A non-empty `ConnectionString` takes precedence.

## Indexes and tenant-aware uniqueness

Use `[Index]` for query/sort performance where duplicate values are allowed. `[Unique]` remains the constraint for values that must not repeat.

```csharp
[Index]
public CustomerStatus Status { get; set; }

[Unique]
public string Email { get; set; } = string.Empty;
```

Named indexes create composite indexes. `Order` controls the key order and `Direction` controls ascending/descending index keys:

```csharp
[Index("IX_Status_Created", Order = 1)]
public CustomerStatus Status { get; set; }

[Index("IX_Status_Created", Order = 2, Direction = IndexDirection.Descending)]
public DateTime CreatedAt { get; set; }
```

For tenant-scoped models SimpleORM automatically prefixes managed indexes/unique constraints with `Tenant`, because normal queries are tenant-scoped. `Code` is unique per tenant (`Tenant + Code`). Existing `[Unique]` fields are also unique per tenant (`Tenant + Email`, for example). `[Global]` models have no tenant prefix, so their `Code` and `[Unique]` values remain globally unique.

On SQL Server, new tenant tables use `(Tenant, Code)` as the primary key. Changing an existing Code-only primary key is destructive and therefore requires `options.Migrations.AllowDestructiveChanges = true`.

## Sum aggregation

`Count` remains available as before. `Sum` performs the aggregation inside the database rather than loading matching records into memory.

```csharp
var total = await repository.Sum<Sale, decimal>(
    sale => sale.TotalAmount,
    sale => sale.Status == SaleStatus.Completed,
    cancellationToken: ct);
```

A `SearchParam` can also be supplied to the `Sum` overload. SQL Server generates `SUM(...)` over the filtered query; MongoDB uses `$match` and `$group/$sum`.

## MongoDB nested and dictionary queries

MongoDB supports dotted document paths through both `SearchParam` and strongly typed expressions. This is useful for nested objects and dictionaries.

```csharp
var blackProducts = await repository.Select<Product>(
    product => product.Attributes["Color"] == "Black",
    cancellationToken: ct);

var lagosCustomers = await repository.Select<Customer>(
    customer => customer.Address.City == "Lagos",
    cancellationToken: ct);
```

Dictionary keys used by expression translation must resolve to a string value. The expression resolver translates the examples above to `Attributes.Color` and `Address.City`.

Dynamic/API filters can use the same paths:

```json
{
  "filters": [
    { "field": "Attributes.Color", "operator": "EQ", "value": "Black" },
    { "field": "Attributes.Storage", "operator": "GTE", "value": 256 }
  ],
  "orderBy": [
    { "field": "Attributes.Storage", "descending": true }
  ],
  "fields": ["Code", "Name", "Attributes.Color", "Attributes.Storage"]
}
```

MongoDB supports these nested paths for filtering, ordering, `SelectDynamic`, and `Sum`. SQL Server deliberately rejects document/dictionary dotted paths unless represented through the ORM's relational join facilities.

## Upsert

`DBModel.Upsert` requests insert-or-replace behavior when a MongoDB model is saved as `DataState.Changed`:

```csharp
product.DataState = DataState.Changed;
product.Upsert = true;
await repository.Save(product, ct);
```

If the matching MongoDB document exists it is replaced; if it does not exist MongoDB inserts it. `Upsert` is an instruction property and is not persisted.

Optimistic concurrency and last-write-wins upsert have conflicting semantics, so MongoDB rejects `Upsert = true` while concurrency protection is enabled for the model. Use normal `DataState.New` inserts where possible. If last-write-wins upsert is intentionally required, opt that model out with `[DisableConcurrencyCheck]`.

`Upsert` is currently a MongoDB provider feature; SQL Server does not currently translate `DBModel.Upsert` into a MERGE/upsert operation.
