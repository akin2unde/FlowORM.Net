# FlowORM.Net

[![NuGet](https://img.shields.io/nuget/v/FlowORM.Net.svg?label=NuGet)](https://www.nuget.org/packages/FlowORM.Net/)


A lightweight, provider-agnostic ORM for .NET 10 with support for SQL Server and MongoDB.

FlowORM.Net provides a consistent repository API across relational and document databases while handling common application concerns such as multi-tenancy, automatic schema synchronization, optimistic concurrency, dynamic queries, transactions, model extensions, indexing, aggregation, soft deletion, and auditing.

## Features

- .NET 10
- SQL Server
- MongoDB
- Provider-independent repository API
- Dependency injection
- Automatic schema synchronization
- Multi-tenancy
- Global/shared models
- CRUD operations
- Batch operations
- Transactions with explicit MongoDB replica-set/sharded-cluster support
- Scoped bootstrap saves without a tenant
- Optimistic concurrency
- Soft and hard delete
- Upsert support
- Dynamic queries
- Runtime-created entities and indexes
- `SelectDynamic`
- Expression-based filtering
- `SearchParam` filtering
- Sorting and paging
- Joins
- Count and Sum aggregation
- Unique constraints
- Single and composite indexes
- Automatic Code generation
- Model extensions / EAV fields
- Audit trail
- Error logging
- Automatic stale-data cleanup
- SQL Server bulk operations
- MongoDB nested-document queries
- MongoDB dictionary filtering
- Full connection-string support
- CancellationToken support
- XML documentation

---

# Installation

Install the core package:

```bash
dotnet add package FlowORM.Net
```

Then install the provider required by your application.

### SQL Server

```bash
dotnet add package FlowORM.Net.SqlServer
```

### MongoDB

```bash
dotnet add package FlowORM.Net.MongoDB
```

---

# Getting Started

## SQL Server

Register FlowORM:

```csharp
builder.Services.AddFlowOrm(options =>
{
    options.Connection.ConnectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");
});

builder.Services.AddFlowOrmSqlServer();
```

Example `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CommerceDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
  }
}
```

---

## MongoDB

```csharp
builder.Services.AddFlowOrm(options =>
{
    options.Connection.ConnectionString =
        builder.Configuration.GetConnectionString("MongoDb");
});

builder.Services.AddFlowOrmMongoDB(options =>
{
    // Leave false for standalone MongoDB.
    options.SupportTransactions = false;
});
```

Example:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://localhost:27017/CommerceDb"
  }
}
```

When `ConnectionString` is supplied, it takes precedence over the individual connection properties.

---

# Connection Configuration

You can provide a complete connection string:

```csharp
options.Connection.ConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");
```

Or configure the connection using the individual connection properties supported by the provider.

Using `ConnectionString` is recommended when your application already manages database connections through normal .NET configuration.

---

# Creating a Model

Models inherit from `DBModel`.

```csharp
public class Product : DBModel
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public ProductStatus Status { get; set; }
}
```

`DBModel` provides the common FlowORM model infrastructure including Code, state tracking, tenant information, concurrency versioning, timestamps, and other ORM metadata.

---

# Repository

Inject `IDataRepository` into your service:

```csharp
public class ProductService
{
    private readonly IDataRepository _repository;

    public ProductService(IDataRepository repository)
    {
        _repository = repository;
    }
}
```

The same repository API works with the configured database provider.

---

# Creating Records

```csharp
var product = new Product
{
    Name = "Sample Product",
    Category = "Electronics",
    Price = 2500,
    DataState = DataState.New
};

await _repository.Save(product);
```

FlowORM automatically generates a Code when one has not been supplied.

---

# Updating Records

Load the record:

```csharp
var product = await _repository.SelectSingle<Product>(
    x => x.Code == productCode);
```

Modify it:

```csharp
product.Price = 3000;
product.DataState = DataState.Changed;

await _repository.Save(product);
```

---

# Batch Save

FlowORM supports batch operations:

```csharp
await _repository.Save(products);
```

The default batch size is 100.

The maximum physical batch size is 500. Larger collections are automatically divided into safe batches.

You can override the batch size when required.

---

# Select

## Expression Query

```csharp
var result = await _repository.Select<Product>(
    x => x.Status == ProductStatus.Active);
```

---

## Paging

```csharp
var result = await _repository.Select<Product>(
    skip: 0,
    limit: 100);
```

You can also use the convenience overload:

```csharp
var result = await _repository.Select<Product>(0, 100);
```

---

## Fetch All

A limit of `0` means return all matching records:

```csharp
var result = await _repository.Select<Product>(
    skip: 0,
    limit: 0);
```

FlowORM may still read the records internally in bounded batches.

---

# SelectSingle

```csharp
var product = await _repository.SelectSingle<Product>(
    x => x.Code == productCode);
```

---

# SearchParam

`SearchParam` provides dynamic filtering for scenarios where the query is constructed at runtime, such as an API request.

Example:

```csharp
var search = new SearchParam
{
    Filters =
    [
        new SearchFilter
        {
            Field = "Status",
            Operator = SearchOperator.EQ,
            Value = ProductStatus.Active
        }
    ]
};

var result = await _repository.Select<Product>(search);
```

Supported operators include:

- `EQ`
- `NEQ`
- `GT`
- `GTE`
- `LT`
- `LTE`
- `Contains`
- `StartsWith`
- `EndsWith`
- `In`
- `NotIn`
- `IsNull`
- `IsNotNull`
- `Between`
- `NotBetween`

---

# SelectDynamic

`SelectDynamic` allows you to retrieve only selected fields instead of materializing the complete model.

This is useful for reporting, dashboards, grids, APIs, and other projection-heavy workloads.

```csharp
var search = new SearchParam
{
    Fields =
    [
        "Code",
        "Name",
        "Category",
        "Price"
    ]
};

var result = await _repository.SelectDynamic<Product>(
    search,
    skip: 0,
    limit: 100);
```

The result is:

```csharp
PagedResult<dynamic>
```

Only the requested fields are returned.

---

# Sorting

Sorting can be supplied through the query definition.

For example:

```text
Price DESC
Name ASC
```

Multiple sort fields can be used where supported.

---

# Count

FlowORM performs Count directly in the database.

```csharp
var count = await _repository.Count<Product>(
    x => x.Status == ProductStatus.Active);
```

This does not load all matching records into application memory.

---

# Sum

FlowORM supports database-side Sum aggregation.

```csharp
var total = await _repository.Sum<Sale, decimal>(
    x => x.TotalAmount,
    x => x.Status == SaleStatus.Completed);
```

`SearchParam` can also be used when the filter is dynamic.

Aggregation is executed by the database provider rather than loading all matching records into .NET.

---

# Indexes

Use `[Index]` when a property should have a non-unique database index.

```csharp
[Index]
public string Category { get; set; } = string.Empty;
```

Duplicate values are allowed.

Indexes are useful for fields frequently used for:

- filtering
- sorting
- lookups

Do not index every property unnecessarily because indexes have storage and write-performance costs.

---

# Composite Indexes

Multiple properties can participate in the same index.

```csharp
[Index("IX_Status_SoldAt", Order = 1)]
public SaleStatus Status { get; set; }

[Index(
    "IX_Status_SoldAt",
    Order = 2,
    Direction = IndexDirection.Descending)]
public DateTime SoldAt { get; set; }
```

FlowORM creates the equivalent composite/compound index for the configured provider.

---

# Unique Fields

Use `[Unique]` when duplicate values must not be allowed.

```csharp
[Unique]
public string Sku { get; set; } = string.Empty;
```

`[Unique]` is different from `[Index]`.

`[Index]` improves lookup performance while allowing duplicate values.

`[Unique]` enforces uniqueness at the database level.

---

# Multi-Tenancy

FlowORM supports tenant-aware models.

When multi-tenancy is enabled, tenant-scoped queries automatically include the current tenant.

Application code can therefore query normally:

```csharp
var products = await _repository.Select<Product>();
```

while FlowORM applies the tenant restriction automatically.

---

# Tenant-Aware Indexes

For tenant-scoped models, FlowORM automatically includes the tenant in managed indexes where appropriate.

For example:

```csharp
[Index]
public string Category { get; set; } = string.Empty;
```

conceptually becomes:

```text
(Tenant, Category)
```

This matches the queries FlowORM generates:

```text
Tenant = currentTenant AND Category = ...
```

---

# Tenant-Aware Uniqueness

Code uniqueness is tenant-aware.

For a tenant model:

```text
UNIQUE(Tenant, Code)
```

This means:

```text
Tenant A + PRO-001    allowed
Tenant B + PRO-001    allowed
Tenant A + PRO-001    duplicate
```

The same rule applies to `[Unique]` properties.

For example:

```csharp
[Unique]
public string Sku { get; set; } = string.Empty;
```

becomes conceptually:

```text
UNIQUE(Tenant, Sku)
```

so two different tenants may use the same SKU while duplicates within the same tenant are rejected.

---

# Saving Without a Tenant for Bootstrap

Tenant enforcement remains enabled by default. For exceptional bootstrap flows, such as creating the first tenant before an authenticated user or tenant context exists, use `RunWithoutTenant`.

```csharp
await repository.RunWithoutTenant(async repo =>
{
    await transactionManager.Execute(async () =>
    {
        await repo.Save(tenant);

        user.Tenant = tenant.Code;
        await repo.Save(user);
    });
});
```

The permission is scoped to the current asynchronous callback and is restored automatically even when the callback throws. It only relaxes the **missing-tenant validation performed by `Save`**. It does not bypass tenant filtering for `Select`, `Count`, `Sum`, update, delete, or other operations.

The scope is execution-local, so concurrent asynchronous work using the same DI scope does not inherit the permission accidentally. Nested `RunWithoutTenant` scopes are also restored safely.

---

# Global Models

Some data should be shared by every tenant.

Use `[Global]`:

```csharp
[Global]
public class Country : DBModel
{
    public string Name { get; set; } = string.Empty;
}
```

Global models do not require a tenant.

For global models:

```text
Code
```

is globally unique rather than tenant-scoped.

The same applies to `[Unique]` fields.

---

# Optimistic Concurrency

Optimistic concurrency protection is enabled by default.

Every `DBModel` has an ORM-managed Version.

Consider:

```text
Person A loads Product Version 4
Person B loads Product Version 4

Person A saves
Database becomes Version 5

Person B attempts to save Version 4
```

FlowORM detects that the record changed after Person B loaded it and throws:

```csharp
DBConcurrencyException
```

This prevents silent lost updates.

No additional read-before-update query is required. The version comparison is performed as part of the database update.

---

# Disabling Concurrency for a Model

Some models may intentionally use last-write-wins behavior.

Use:

```csharp
[DisableConcurrencyCheck]
public class AuditLog : DBModel
{
}
```

The Version remains available, but concurrent modifications are not rejected for that model.

---

# Upsert

FlowORM supports Upsert for providers that implement it.

Upsert means:

> Update the record if it exists; otherwise insert it.

Example:

```csharp
var product = new Product
{
    Code = "PRO-001",
    Name = "Updated Product",
    Price = 5000,

    DataState = DataState.Changed,
    Upsert = true
};

await _repository.Save(product);
```

This is useful for synchronization and import scenarios where the caller may not know whether the record already exists.

## Upsert and Concurrency

Upsert is a last-write-wins operation and therefore conflicts with normal optimistic concurrency semantics.

If a model uses concurrency protection, FlowORM does not silently bypass the Version check.

Use Upsert only where last-write-wins behavior is intentional.

MongoDB currently provides the primary Upsert implementation.

---

# Soft Delete

Models can use soft-delete behavior so that deleting a record does not physically remove it from the database.

Soft-deleted records are automatically excluded from normal queries.

---

# Hard Delete

Models configured for hard deletion are physically removed from the database.

Concurrency protection also applies to delete operations unless explicitly disabled for the model.

---

# Transactions

FlowORM reuses an existing transaction when one is active, so nested repository saves participate in the outer transaction and only the outer operation commits or rolls back.

```csharp
await transactionManager.Execute(async () =>
{
    await _repository.Save(customer);
    await _repository.Save(order);
    await _repository.Save(payment);
});
```

## MongoDB transaction support

MongoDB transactions require a replica set or supported sharded cluster. FlowORM keeps standalone MongoDB safe by disabling MongoDB transactions by default.

For a standalone deployment, no extra configuration is required:

```csharp
builder.Services.AddFlowOrmMongoDB();
```

`IDBTransactionManager.Execute` still runs the callback, but no MongoDB transaction is started, so rollback semantics are not available. This keeps existing grouped-save code usable on standalone MongoDB without pretending that the operations are atomic.

When the MongoDB deployment supports transactions, enable them explicitly:

```csharp
builder.Services.AddFlowOrmMongoDB(options =>
{
    options.SupportTransactions = true;
});
```

When `SupportTransactions` is `true`, FlowORM validates the MongoDB topology **once during application startup** using the configured singleton `IMongoClient`. A replica set or sharded cluster passes validation. A standalone server causes startup to fail with a clear configuration error. The check is not repeated for every transaction.

FlowORM's typed provider, runtime provider, validation service, and transaction sessions all reuse the same singleton `IMongoClient`.

---

# Automatic Schema Synchronization

FlowORM can synchronize the database schema with model metadata when the application starts.

Depending on the provider and configuration, synchronization can handle changes such as:

- tables/collections
- columns
- column types
- keys
- unique constraints
- indexes

MongoDB schema synchronization focuses primarily on indexes because MongoDB documents do not require relational table schemas.

Potentially destructive schema changes are controlled separately and should be enabled deliberately.

---

# Model Extensions

FlowORM supports extending models without adding physical properties to the original model.

An extendable model can store dynamic extension values through:

```csharp
model.Extended
```

Extension definitions can describe properties such as:

- FieldCode
- FieldName
- DataType
- Required
- Size
- DefaultValue

Extension values are loaded and saved with the owning model.

This is useful for applications that allow customers or administrators to define additional fields at runtime.

---

# MongoDB Nested Documents

MongoDB supports querying nested document properties.

For example:

```csharp
public class Customer : DBModel
{
    public Address Address { get; set; } = new();
}
```

can be queried using an expression:

```csharp
var customers = await _repository.Select<Customer>(
    x => x.Address.City == "Lagos");
```

Deeper paths are also supported:

```csharp
x => x.Address.Country.Code == "NG"
```

These are translated to MongoDB dotted document paths.

---

# MongoDB Dictionary Queries

MongoDB models can contain dynamic dictionary data:

```csharp
public Dictionary<string, object?> Attributes { get; set; } = [];
```

For example, a document might contain:

```json
{
  "Attributes": {
    "Color": "Black",
    "Storage": 256
  }
}
```

You can query dictionary fields using expressions:

```csharp
var products = await _repository.Select<Product>(
    x => x.Attributes["Color"] == "Black");
```

Numeric comparisons are also supported:

```csharp
var products = await _repository.Select<Product>(
    x => (int)x.Attributes["Storage"] >= 256);
```

And conditions can be combined:

```csharp
var products = await _repository.Select<Product>(
    x =>
        x.Attributes["Color"] == "Black" &&
        (int)x.Attributes["Storage"] >= 256);
```

---

# MongoDB Dynamic Nested Queries

The same nested fields can be supplied dynamically through `SearchParam`.

For example:

```json
{
  "filters": [
    {
      "field": "Attributes.Color",
      "operator": "EQ",
      "value": "Black"
    },
    {
      "field": "Attributes.Storage",
      "operator": "GTE",
      "value": 256
    }
  ]
}
```

MongoDB translates these to dotted document paths.

Nested fields can also participate in:

- filtering
- sorting
- `SelectDynamic`
- Sum aggregation

This allows both strongly typed application queries and runtime-generated API queries to use the same document structure.

---

# MongoDB SelectDynamic

Nested fields can be projected without returning the complete document.

For example:

```csharp
var search = new SearchParam
{
    Fields =
    [
        "Code",
        "Name",
        "Attributes.Color",
        "Attributes.Storage"
    ]
};

var result = await _repository.SelectDynamic<Product>(
    search);
```

Only the requested fields are projected by MongoDB.

---

# SQL Server Bulk Operations

FlowORM's SQL Server provider uses bulk and set-based operations for high-volume writes.

Insert operations use `SqlBulkCopy`.

Batch updates use staging tables and set-based updates rather than issuing one SQL command for every model.

This also integrates with optimistic concurrency without requiring a separate read-before-write operation.

---

# Automatic Code Generation

`DBModel` supports automatic Code generation.

Models can override the prefix:

```csharp
public class Product : DBModel
{
    public override string GetPrefix()
    {
        return "PRO";
    }
}
```

Generated Codes use randomized characters rather than sequential database counters.

---

# Audit Trail

FlowORM can maintain audit information for models where auditing is enabled.

Audit functionality is optional and can be configured according to application requirements.

---

# Error Logging

FlowORM can persist application/ORM errors to the configured database when database error logging is enabled.

Error logging is optional.

Retention can also be configured so old error records are automatically removed.

---

# Automatic Stale-Data Cleanup

Models can define automatic retention policies.

For example:

```csharp
[AutoDelete(60, "0 0 1 * *")]
public class TemporaryLog : DBModel
{
}
```

This allows stale records to be periodically removed according to model-specific retention rules.

---

# CancellationToken

Async repository operations support `CancellationToken`.

Example:

```csharp
await _repository.Select<Product>(
    x => x.Status == ProductStatus.Active,
    cancellationToken);
```

This allows HTTP request cancellation and application shutdown signals to propagate to database operations.

---

# Provider Independence

Application code depends on:

```csharp
IDataRepository
```

rather than directly depending on SQL Server or MongoDB.

For example:

```csharp
await _repository.Select<Product>(
    x => x.Status == ProductStatus.Active);
```

remains the same regardless of the configured provider.

Some provider-specific capabilities, particularly document-oriented MongoDB features, naturally have no SQL Server equivalent.

FlowORM throws an explicit unsupported-operation error rather than silently generating incorrect queries when a feature cannot be represented by the active provider.

---

# Performance

FlowORM is designed to avoid unnecessary database round trips.

Key performance features include:

- SQL Server `SqlBulkCopy`
- set-based SQL updates
- configurable batching
- maximum physical batch size of 500
- MongoDB bulk operations
- database-side filtering
- database-side Count and Sum
- database-side dynamic projection
- indexes
- optimistic concurrency without read-before-update
- transaction reuse

Actual performance depends on database configuration, indexes, network latency, model complexity, hardware, and workload.

---

# Sample Project

The repository includes a sample project demonstrating the major FlowORM features.

The sample covers areas such as:

- dependency injection
- SQL/Mongo configuration
- CRUD
- paging
- filtering
- `SearchParam`
- `SelectDynamic`
- Count
- Sum
- transactions
- model extensions
- indexing
- multi-tenancy

Use the sample project alongside this README when integrating FlowORM into a new application.

---

# Documentation

Additional documentation is available in the `docs` directory.

This includes more detailed information about architecture and advanced features such as indexing, aggregation, MongoDB document queries, performance, concurrency, and model extensions.

---

# NuGet

Install the core package:

```bash
dotnet add package FlowORM.Net
```

SQL Server:

```bash
dotnet add package FlowORM.Net.SqlServer
```

MongoDB:

```bash
dotnet add package FlowORM.Net.MongoDB
```

Package versions and release history are available through NuGet and the repository releases.

---

# Runtime Entities

FlowORM can create and query entities at runtime without generating CLR classes and without changing `IDataRepository`.

Runtime entities use the separate `IRuntimeDataRepository` and `ISchemaManager` APIs while sharing FlowORM concepts such as `SearchParam`, paging, tenant scoping, transactions, Code generation, soft delete, concurrency versioning, Count and Sum.

## Create a Runtime Entity

```csharp
var entity = new RuntimeEntityDefinition
{
    Name = "CustomProduct",
    CodePrefix = "CPR",
    Fields =
    [
        new() { Name = "Name", DataType = RuntimeDataType.String, Size = 150, Required = true },
        new() { Name = "Category", DataType = RuntimeDataType.String, Size = 50 },
        new() { Name = "Price", DataType = RuntimeDataType.Decimal, Required = true }
    ],
    Indexes =
    [
        new RuntimeIndexDefinition
        {
            Name = "IX_Category_Price",
            Fields =
            [
                new("Category"),
                new("Price", IndexDirection.Descending)
            ]
        }
    ]
};

await schemaManager.CreateEntity(entity);
```

SQL Server creates a physical table. MongoDB creates a collection and physical MongoDB indexes. Runtime definitions are persisted by the provider so they survive application restarts.

For tenant-scoped runtime entities, managed indexes are automatically tenant-prefixed. A unique `Sku` index therefore becomes logically `(Tenant, Sku)`. Set `Global = true` for shared runtime entities.

## Add and Remove Runtime Indexes

```csharp
await schemaManager.CreateIndex(
    "CustomProduct",
    new RuntimeIndexDefinition
    {
        Name = "UX_Sku",
        Unique = true,
        Fields = [new("Sku")]
    });

await schemaManager.DropIndex("CustomProduct", "UX_Sku");
```

MongoDB runtime indexes support ascending/descending keys, compound indexes, uniqueness, and tenant-aware keys just like model-defined indexes.

## Runtime Fields and Entity Removal

```csharp
await schemaManager.AddField(
    "CustomProduct",
    new RuntimeFieldDefinition
    {
        Name = "Description",
        DataType = RuntimeDataType.String,
        Size = 500
    });
```

Destructive operations must be explicitly enabled:

```csharp
await schemaManager.DropField("CustomProduct", "Description", allowDestructiveChange: true);
await schemaManager.DropEntity("CustomProduct", allowDestructiveChange: true);
```

## Save Runtime Data

```csharp
await runtimeRepository.Save(
    "CustomProduct",
    new Dictionary<string, object?>
    {
        ["Name"] = "Samsung S26",
        ["Category"] = "Phone",
        ["Price"] = 950000m
    });
```

Batch save uses the same bounded-batch approach as the normal repository:

```csharp
await runtimeRepository.Save("CustomProduct", products, cancellationToken, batch: 500);
```

## Search Runtime Data

Runtime entities use the existing `SearchParam` query model:

```csharp
var search = new SearchParam
{
    Filters =
    [
        new SearchFilter { Field = "Category", Operator = SearchOperator.EQ, Value = "Phone" },
        new SearchFilter { Field = "Price", Operator = SearchOperator.GTE, Value = 500000m }
    ],
    OrderBy =
    [
        new SearchOrder { Field = "Price", Descending = true }
    ]
};

var result = await runtimeRepository.Select(
    "CustomProduct",
    search,
    skip: 0,
    limit: 100);
```

The result is `PagedResult<dynamic>`.

Runtime entities also support:

```csharp
var item = await runtimeRepository.SelectSingle("CustomProduct", search);
var count = await runtimeRepository.Count("CustomProduct", search);
var total = await runtimeRepository.Sum<decimal>("CustomProduct", "Price", search);
await runtimeRepository.Update("CustomProduct", code, values);
await runtimeRepository.Delete("CustomProduct", code);
```

`IDataRepository` remains unchanged; use it for compile-time `DBModel` types and use `IRuntimeDataRepository` only for runtime-defined entities.

## Runtime Upsert

Runtime entities support the same explicit upsert intent as typed models without storing `Upsert` as business data:

```csharp
await runtimeRepository.Save(
    "Order",
    order,
    cancellationToken,
    upsert: true);
```

Batch upsert is also supported:

```csharp
await runtimeRepository.Save(
    "Order",
    orders,
    cancellationToken,
    batch: 500,
    upsert: true);
```

Upsert uses last-write-wins semantics. A runtime entity with optimistic concurrency enabled rejects upsert; set `ConcurrencyEnabled = false` on an entity only when that behavior is intentional.

## Runtime JSON Dotted Paths

A field declared as `RuntimeDataType.Json` can be queried through a dotted path:

```csharp
var search = new SearchParam
{
    Filters =
    [
        new SearchFilter
        {
            Field = "Details.Lines.Product",
            Operator = SearchOperator.EQ,
            Value = "PRO-001"
        }
    ]
};
```

FlowORM validates `Details` as a defined JSON runtime field and passes the remaining path to the provider. MongoDB supports native dotted document/array paths. SQL Server translates scalar JSON paths through its JSON functions; array-element matching that requires `OPENJSON` remains provider-specific.

## Runtime Bulk Update and Delete

Update all runtime records matching a `SearchParam` in one provider-side operation:

```csharp
var affected = await runtimeRepository.Update(
    "PrismRecord_SalesByCustomer",
    search,
    new Dictionary<string, object?>
    {
        ["CustomerName"] = "Ada Okafor"
    },
    cancellationToken);
```

Delete all matching records:

```csharp
var affected = await runtimeRepository.Delete(
    "PrismRecord_SalesByCustomer",
    search,
    cancellationToken);
```

MongoDB uses `UpdateMany` / `DeleteMany` (or a bulk soft-delete update). SQL Server uses one set-based `UPDATE` / `DELETE`. For safety, bulk update and delete reject a `SearchParam` with no filters.

## Database Name Validation

FlowORM validates the target database before opening provider connections.

- With individual connection settings, set `Connection.DatabaseName`.
- With a MongoDB connection string, either set `Connection.DatabaseName` or include the database in the connection string.
- With a SQL Server connection string, include `Initial Catalog` / `Database`.

A missing database name now produces a clear `InvalidOperationException` instead of failing later during provider initialization or the first database operation.

# Select with Optional Total Calculation

Existing `Select` calls still calculate the total. Use this for UI pagination such as “Page 1 of 20”:

```csharp
var result = await repository.Select<Customer>(search, limit: 50, cancellationToken: ct);
var rows = result.Data;
var total = result.TotalRecords; // Calculated; TotalCalculated is true.
```

The overload with `includeTotal: false` avoids the matching-record Count query:

```csharp
var result = await repository.Select<Customer>(
    search,
    includeTotal: false,
    limit: 500,
    cancellationToken: ct);

var rows = result.Data;
// result.TotalCalculated == false
// result.TotalRecords == 0 is a placeholder, not proof there are zero matches.
```

Use `TotalCalculated` before displaying or otherwise interpreting `TotalRecords`. `Data.Count` is the number returned in this page, not the number of matching records. Reading only `.Data` from a counted Select does not avoid Count: it has already run.

| Use case | Calculate total? |
|---|---|
| UI page numbers and total records | Yes; normal Select |
| Exact progress percentage | Calculate once where practical, then read later pages without counting |
| Worker processing successive batches until no rows remain | No |
| Export processing successive batches | No, unless a displayed total is needed |
| Prism pre-compile rebuild | No count per page; mirror summary can provide estimated progress |

All existing Select signatures remain. New overloads cover SearchParam, expressions, expressions plus SearchParam, dynamic projections and runtime entities. There is no separate SelectData API:

```csharp
await repository.Select<Customer>(includeTotal: false, limit: 500, cancellationToken: ct);
await repository.Select<Customer>(c => c.Name == "Ada", includeTotal: false, cancellationToken: ct);
await repository.Select<Customer>(c => c.Name == "Ada", search, includeTotal: false, cancellationToken: ct);
await repository.SelectDynamic<Customer>(
    new SearchParam { Fields = [nameof(Customer.Name)] }, includeTotal: false, cancellationToken: ct);
await runtimeRepository.Select("Customer", search, includeTotal: false, limit: 500, cancellationToken: ct);
```

Typed no-count reads still normalize search values, enforce provider tenant/soft-delete rules, load extensions and apply return defaults. Logical limits and the existing physical batch cap of 500 remain. `limit: 0` still fetches all remaining rows: no-count reads stop at an empty/short batch. Fetch-all returns a materialized collection; physical batching does not bound the memory of the complete result. Use positive limits for large workers and exports.

For a cursor-based worker, use the same filter and ordering on each page. Here the cursor is the last Code read, not a last-updated marker:

```csharp
string? after = null;
while (true)
{
    var query = new SearchParam
    {
        OrderBy = [new SearchOrder { Field = nameof(Customer.Code) }]
    };
    if (after is not null)
        query.Filters.Add(new SearchFilter
        {
            Field = nameof(Customer.Code), Operator = SearchOperator.GT, Value = after
        });

    var page = await repository.Select<Customer>(query, includeTotal: false,
        limit: 500, cancellationToken: ct);
    if (page.Data.Count == 0) break;

    await Process(page.Data, ct); // Your application processing method.
    after = page.Data[^1].Code;
}
```

A cursor scan is not a change feed or a point-in-time snapshot. Concurrent inserts/updates behind the cursor need a later reconciliation run or durable pending work. For progress, calculate a count once or use an appropriate summary rather than counting the remaining records on every page.

# Scoped Run and Transaction

`AddFlowOrm` now registers `IFlowOrmExecutor` automatically. Inject it into a worker or service:

```csharp
using FlowORM.Net.Abstractions;
using FlowORM.Net.Models;
using FlowORM.Net.Query;

public sealed class CustomerWorker(IFlowOrmExecutor orm)
{
    public Task<PagedResult<Customer>> ReadPage(SearchParam search, string tenant, CancellationToken ct) =>
        orm.Run((repo, token) => repo.Select<Customer>(search,
            includeTotal: false, limit: 500, cancellationToken: token),
            tenant: tenant, cancellationToken: ct);
}
```

Run creates one DI scope, sets an optional tenant override, resolves IDataRepository, awaits the callback and asynchronously disposes the scope. It uses the registered service container; it does not create another database or container per call. MongoDB typed/runtime providers continue sharing the registered singleton MongoClient and its connection pools. SQL Server continues using its provider connection lifecycle and SQL connection pooling.

The executor is singleton-safe; each call owns its repository and execution context. Multiple operations inside one callback use the same scoped repository:

```csharp
var customer = await orm.Run(
    (repo, token) => repo.GetByCode<Customer>("CUS-001", token),
    tenant: "Tenant1", cancellationToken: ct);
```

Run does not start an outer transaction. Individual repository writes keep their existing transaction behavior. Use Transaction when several writes must commit together:

```csharp
await orm.Transaction(async (repo, token) =>
{
    await repo.Save(customer, token);
    await repo.Save(inventory, token); // A different model type, same transaction.
}, tenant: "Tenant1", cancellationToken: ct);
```

Transaction resolves the repository and IDBTransactionManager from the same scope. Existing nested repository saves reuse the active transaction. The callback does not need one model type or return type to group writes. Result-returning overloads are available for both Run and Transaction. Exceptions propagate; scope disposal happens on success, failure and cancellation. Rollback uses a non-cancelled token so cancellation does not prevent cleanup.

| Situation | Recommended access |
|---|---|
| Normal API request service | Inject IDataRepository; keep the existing request scope |
| Hosted worker/scheduled process | Inject IFlowOrmExecutor; Run creates the operation scope |
| Several atomic writes | Transaction, or the existing scoped IDBTransactionManager |
| Reading the next batch | Run with Select(includeTotal: false) |

Tenant and lifetime rules:

- An explicit non-empty tenant overrides the tenant only for that operation. Concurrent calls have independent scoped contexts.
- Omitted/null tenant means use the existing JWT/custom ITenantProvider. The executor does not replace that provider or change the HTTP user's identity.
- A background process without an ambient identity must supply a tenant when tenant-scoped multi-tenancy requires it. Global-model behavior remains unchanged.
- Keep your existing user provider for audit identity. This API overrides the tenant, not the user.
- Calls use the ORM registration in their own service container. This does not add named-database registration; Prism can resolve its executor from its existing private container.
- Nested executor calls create new scopes and independent transactions. Use the callback's repository for all work that must participate in the same transaction, and await that work sequentially.
- Return materialized results rather than repositories or deferred operations whose scope has already ended.

`FlowOrmExecutionContext` is scoped. The built-in repository, extension service and both typed/runtime providers wrap the configured tenant provider through this context. Custom providers/services that resolve tenants directly can participate without changing ITenantProvider:

```csharp
// In a custom provider/service constructor:
_operationTenant = executionContext.Wrap(configuredTenantProvider);
// Later:
var tenant = _operationTenant.GetTenant();
```

The sample's custom extension service is updated accordingly. See `ScopedExecutionExamples.cs` for registered examples of paged reads and mixed-model transactions; these examples do not run automatically at startup.

One existing transaction limitation remains: repository Save updates supplied model state/version before an outer transaction commits. If that outer transaction later rolls back, those in-memory objects are not automatically restored. Reload/reconstruct them or explicitly restore their persistence state before retrying. Database writes still roll back atomically.

## Validation of this update

Added tests for default counted reads, no-count physical batching/fetch-all, expressions and projections, runtime reads, tenant isolation/fallback, scoped tenant-less bootstrap saves, asynchronous scope disposal, shared transactions, rollback and cancellation. The editing environment lacks the .NET SDK and database servers: executable tests and provider integration must run in CI. Local source syntax, XML, JSON and archive checks do not substitute for compilation or database integration.

---

# Contributing

Contributions, bug reports, feature requests, tests, and documentation improvements are welcome.

When contributing:

1. Keep provider-independent behavior in the core package.
2. Keep database-specific behavior in the appropriate provider.
3. Add tests for new functionality.
4. Update documentation when public behavior changes.
5. Preserve backward compatibility where practical.

---

# License

See the repository license for licensing information.

---

# Author

**Akintunde Morakinyo**


### Audit trail behavior

Set `options.AuditTrail.Enabled = true` to persist `DBAuditTrail` entries to the `__DBAuditTrail` table/collection when typed models are inserted, updated or deleted. Audit entries are written through the same database provider and transaction as the original write. `[DisableAudit]` excludes a model, while `[DoNotAudit]` excludes individual properties from the JSON snapshot. `IncludeNewValues` controls snapshots for inserts and updates. **Historical `OldData` is not reconstructed by this implementation**, and hard deletes record the action without a new-value snapshot. Ensure the audit table is included in schema migration before the first audited write, especially if automatic migration is disabled.


## Scoped tenant operations

FlowORM provides narrowly scoped tenant overrides. These scopes restore the previous
state after the callback completes, including when it throws. They are async-flow-local.

```csharp
// Bootstrap: only Save may omit the tenant.
await repository.RunSaveWithoutTenant(async db =>
{
    await db.Save(tenant);
});

// Privileged read of one tenant (authorization is your application's responsibility).
var invoices = await repository.RunReadForTenant(
    "TEN-001",
    async db => await db.Select<Invoice>());

// Privileged read of every tenant.
var allInvoices = await repository.RunReadAcrossTenants(
    async db => await db.Select<Invoice>());
```

`RunWithoutTenant` remains available as a compatibility alias for the save-only
scope. Read scopes affect normal typed repository reads (`Select`, `SelectSingle`,
`Count`, and `Sum`), not saves. Attempting to save inside a read scope throws.
Never expose the administrative read scopes to untrusted callers: FlowORM does
not infer a super-admin role or authorize these methods automatically.

**Limitations:** Runtime-entity queries and SQL/Mongo join paths are not yet
fully covered by the new administrative read scopes. MongoDB refuses a
cross-tenant joined read rather than risk joining different tenants incorrectly.
For now, use these scopes for ordinary typed model queries without joins.


## Code identity and automatic indexing

Every `DBModel` inherits a non-virtual `Code` identity property. Do not redeclare it with `new`: model registration rejects hidden `Code` properties to prevent ambiguous provider mappings. To customize generated codes, use the existing class-level `[DBCode(Prefix = "CUS", Length = 12)]` attribute (or the global `CodeGeneration` options). These settings control generation, not the identity column's mapping.

**No `[Index]` or `[Unique]` annotation is required on `Code`.** SQL Server creates/maintains the primary key on `Code` for global models, or `(Tenant, Code)` for tenant-scoped models; MongoDB manages an equivalent unique index. The compound key allows the same code to exist in different tenants, while preventing duplicates within one tenant. The SQL primary key already provides an index, so adding another index on the same identity columns would be redundant.

```csharp
[DBCode(Prefix = "CUS", Length = 12)]
public sealed class Customer : DBModel
{
    public string Name { get; set; } = string.Empty;
}
```

Use `[Index]` for additional query fields (for example, a frequently searched customer name or an invoice's `CustomerCode`). Before applying unique constraints to existing databases, resolve any pre-existing duplicate identity values.


## Reference-aware search (SQL Server preview)

A one-level relationship can be declared on a reference code:

```csharp
public class Invoice : DBModel
{
    [Reference(typeof(Customer), nameof(Customer.Code),
        SearchFields = [nameof(Customer.Name), nameof(Customer.Email)])]
    public string? CustomerCode { get; set; }
}

var results = await repository.Select<Invoice>(new SearchParam
{
    Search = "Ada",
    SearchReferences = true
});
```

`SearchReferences` defaults to false, so normal reads do not perform extra reference lookups.
SQL Server uses a correlated `EXISTS` search, preserving invoices with missing customer codes
when the invoice's own fields match. Tenant-scoped models match on both reference code
and tenant, even under `RunReadAcrossTenants`. Reference matches do not hydrate the
related model. Only one level is supported.

**Current limitation:** MongoDB reference-aware search is not implemented in this preview;
requests are rejected explicitly. This preview supports reference text searches but not
reference-field filter operators. Do not publish as a cross-provider feature yet.


### MongoDB reference-aware searching

MongoDB uses an opt-in `$lookup` pipeline for one-level `[Reference]` search. `SearchReferences = true` searches configured reference fields, while `SearchFields = ["Customer.Name"]` explicitly requests that field. Normal searches use `Find` without lookup. A null, empty, or unmatched `CustomerCode` does not remove an invoice that matches its own fields. Multi-tenant lookups match both the referenced code and tenant when both models are tenant-scoped, including `RunReadAcrossTenants`. The pipeline is shared by `Select`, `SelectSingle`, and `Count`; joined results are not hydrated into model properties. Administrative scopes must be authorized by the calling application.
