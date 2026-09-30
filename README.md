# SimpleORM.Net

[![NuGet](https://img.shields.io/nuget/v/SimpleORM.Net.svg?label=NuGet)](https://www.nuget.org/packages/SimpleORM.Net/)


A lightweight, provider-agnostic ORM for .NET 10 with support for SQL Server and MongoDB.

SimpleORM.Net provides a consistent repository API across relational and document databases while handling common application concerns such as multi-tenancy, automatic schema synchronization, optimistic concurrency, dynamic queries, transactions, model extensions, indexing, aggregation, soft deletion, and auditing.

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
- Transactions
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
dotnet add package SimpleORM.Net
```

Then install the provider required by your application.

### SQL Server

```bash
dotnet add package SimpleORM.Net.SqlServer
```

### MongoDB

```bash
dotnet add package SimpleORM.Net.MongoDB
```

---

# Getting Started

## SQL Server

Register SimpleORM:

```csharp
builder.Services.AddSimpleOrm(options =>
{
    options.Connection.ConnectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");
});

builder.Services.AddSimpleOrmSqlServer();
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
builder.Services.AddSimpleOrm(options =>
{
    options.Connection.ConnectionString =
        builder.Configuration.GetConnectionString("MongoDb");
});

builder.Services.AddSimpleOrmMongoDB();
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

`DBModel` provides the common SimpleORM model infrastructure including Code, state tracking, tenant information, concurrency versioning, timestamps, and other ORM metadata.

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

SimpleORM automatically generates a Code when one has not been supplied.

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

SimpleORM supports batch operations:

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

SimpleORM may still read the records internally in bounded batches.

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

SimpleORM performs Count directly in the database.

```csharp
var count = await _repository.Count<Product>(
    x => x.Status == ProductStatus.Active);
```

This does not load all matching records into application memory.

---

# Sum

SimpleORM supports database-side Sum aggregation.

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

SimpleORM creates the equivalent composite/compound index for the configured provider.

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

SimpleORM supports tenant-aware models.

When multi-tenancy is enabled, tenant-scoped queries automatically include the current tenant.

Application code can therefore query normally:

```csharp
var products = await _repository.Select<Product>();
```

while SimpleORM applies the tenant restriction automatically.

---

# Tenant-Aware Indexes

For tenant-scoped models, SimpleORM automatically includes the tenant in managed indexes where appropriate.

For example:

```csharp
[Index]
public string Category { get; set; } = string.Empty;
```

conceptually becomes:

```text
(Tenant, Category)
```

This matches the queries SimpleORM generates:

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

SimpleORM detects that the record changed after Person B loaded it and throws:

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

SimpleORM supports Upsert for providers that implement it.

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

If a model uses concurrency protection, SimpleORM does not silently bypass the Version check.

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

SimpleORM supports transactions.

Operations automatically reuse an existing SimpleORM transaction when one is active.

This allows multiple repository operations to participate in the same transaction without each operation independently committing.

Example:

```csharp
await transactionManager.Execute(async () =>
{
    await _repository.Save(customer);
    await _repository.Save(order);
    await _repository.Save(payment);
});
```

The transaction is committed only when the outer transaction completes successfully.

---

# Automatic Schema Synchronization

SimpleORM can synchronize the database schema with model metadata when the application starts.

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

SimpleORM supports extending models without adding physical properties to the original model.

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

SimpleORM's SQL Server provider uses bulk and set-based operations for high-volume writes.

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

SimpleORM can maintain audit information for models where auditing is enabled.

Audit functionality is optional and can be configured according to application requirements.

---

# Error Logging

SimpleORM can persist application/ORM errors to the configured database when database error logging is enabled.

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

SimpleORM throws an explicit unsupported-operation error rather than silently generating incorrect queries when a feature cannot be represented by the active provider.

---

# Performance

SimpleORM is designed to avoid unnecessary database round trips.

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

The repository includes a sample project demonstrating the major SimpleORM features.

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

Use the sample project alongside this README when integrating SimpleORM into a new application.

---

# Documentation

Additional documentation is available in the `docs` directory.

This includes more detailed information about architecture and advanced features such as indexing, aggregation, MongoDB document queries, performance, concurrency, and model extensions.

---

# NuGet

Install the core package:

```bash
dotnet add package SimpleORM.Net
```

SQL Server:

```bash
dotnet add package SimpleORM.Net.SqlServer
```

MongoDB:

```bash
dotnet add package SimpleORM.Net.MongoDB
```

Package versions and release history are available through NuGet and the repository releases.

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

---

# Runtime Entities

SimpleORM can create and query entities at runtime without generating CLR classes and without changing `IDataRepository`.

Runtime entities use the separate `IRuntimeDataRepository` and `ISchemaManager` APIs while sharing SimpleORM concepts such as `SearchParam`, paging, tenant scoping, transactions, Code generation, soft delete, concurrency versioning, Count and Sum.

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

SimpleORM validates `Details` as a defined JSON runtime field and passes the remaining path to the provider. MongoDB supports native dotted document/array paths. SQL Server translates scalar JSON paths through its JSON functions; array-element matching that requires `OPENJSON` remains provider-specific.

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

SimpleORM validates the target database before opening provider connections.

- With individual connection settings, set `Connection.DatabaseName`.
- With a MongoDB connection string, either set `Connection.DatabaseName` or include the database in the connection string.
- With a SQL Server connection string, include `Initial Catalog` / `Database`.

A missing database name now produces a clear `InvalidOperationException` instead of failing later during provider initialization or the first database operation.
