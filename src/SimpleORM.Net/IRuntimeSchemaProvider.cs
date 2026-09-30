#pragma warning disable CS1591
using SimpleORM.Net.Runtime;

namespace SimpleORM.Net.Abstractions;

/// <summary>Provider-specific runtime schema operations.</summary>
public interface IRuntimeSchemaProvider
{
    Task CreateEntity(RuntimeEntityDefinition definition, CancellationToken cancellationToken = default);
    Task DropEntity(string entity, bool allowDestructiveChange = false, CancellationToken cancellationToken = default);
    Task AddField(string entity, RuntimeFieldDefinition field, CancellationToken cancellationToken = default);
    Task DropField(string entity, string field, bool allowDestructiveChange = false, CancellationToken cancellationToken = default);
    Task CreateIndex(string entity, RuntimeIndexDefinition index, CancellationToken cancellationToken = default);
    Task DropIndex(string entity, string indexName, CancellationToken cancellationToken = default);
    Task<RuntimeEntityDefinition?> GetEntity(string entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RuntimeEntityDefinition>> GetEntities(CancellationToken cancellationToken = default);
}
