#pragma warning disable CS1591
using FlowORM.Net.Abstractions;
using FlowORM.Net.Runtime;
namespace FlowORM.Net.Services;
/// <summary>Provider-neutral facade for runtime schema management.</summary>
public sealed class SchemaManager(IRuntimeSchemaProvider provider) : ISchemaManager
{
    public Task CreateEntity(RuntimeEntityDefinition definition, CancellationToken ct = default) => provider.CreateEntity(definition, ct);
    public Task DropEntity(string entity, bool allowDestructiveChange = false, CancellationToken ct = default) => provider.DropEntity(entity, allowDestructiveChange, ct);
    public Task AddField(string entity, RuntimeFieldDefinition field, CancellationToken ct = default) => provider.AddField(entity, field, ct);
    public Task DropField(string entity, string field, bool allowDestructiveChange = false, CancellationToken ct = default) => provider.DropField(entity, field, allowDestructiveChange, ct);
    public Task CreateIndex(string entity, RuntimeIndexDefinition index, CancellationToken ct = default) => provider.CreateIndex(entity, index, ct);
    public Task DropIndex(string entity, string indexName, CancellationToken ct = default) => provider.DropIndex(entity, indexName, ct);
    public Task<RuntimeEntityDefinition?> GetEntity(string entity, CancellationToken ct = default) => provider.GetEntity(entity, ct);
    public Task<IReadOnlyList<RuntimeEntityDefinition>> GetEntities(CancellationToken ct = default) => provider.GetEntities(ct);
}
