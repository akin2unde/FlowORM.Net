#pragma warning disable CS1591
namespace SimpleORM.Net.Runtime;
/// <summary>Defines an entity whose schema is created at runtime.</summary>
public sealed class RuntimeEntityDefinition
{
    public string Name { get;
    set;
} = string.Empty;
public string? CodePrefix { get;
set;
}
public bool Global { get;
set;
}
public bool SoftDelete { get;
set;
} = true;
public bool ConcurrencyEnabled { get;
set;
} = true;
public List<RuntimeFieldDefinition> Fields { get;
set;
} = [];
public List<RuntimeIndexDefinition> Indexes { get;
set;
} = [];
}
