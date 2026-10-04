#pragma warning disable CS1591
namespace FlowORM.Net.Runtime;

/// <summary>Defines a runtime database index.</summary>
public sealed class RuntimeIndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool Unique { get; set; }
    public List<RuntimeIndexFieldDefinition> Fields { get; set; } = [];
}
