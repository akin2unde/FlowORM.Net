#pragma warning disable CS1591
using SimpleORM.Net.Configuration;
namespace SimpleORM.Net.Runtime;
/// <summary>Defines one ordered field in a runtime index.</summary>
public sealed class RuntimeIndexFieldDefinition
{
    public RuntimeIndexFieldDefinition()
    {
    }
    public RuntimeIndexFieldDefinition(string name, IndexDirection direction = IndexDirection.Ascending)
    {
        Name = name;
        Direction = direction;
    }
    public string Name { get;
    set;
} = string.Empty;
public IndexDirection Direction { get;
set;
} = IndexDirection.Ascending;
}
