#pragma warning disable CS1591
namespace FlowORM.Net.Runtime;

/// <summary>Defines one field on a runtime entity.</summary>
public sealed class RuntimeFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public RuntimeDataType DataType { get; set; } = RuntimeDataType.String;
    public int? Size { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
}
