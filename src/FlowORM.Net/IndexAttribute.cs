using FlowORM.Net.Configuration;

namespace FlowORM.Net.Attributes;

/// <summary>Creates a non-unique database index. A shared name defines a composite index.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class IndexAttribute : Attribute
{
    /// <summary>Creates a single-column index with an automatically generated name.</summary>
    public IndexAttribute() { }

    /// <summary>Creates or participates in a named composite index.</summary>
    public IndexAttribute(string name) => Name = name;

    /// <summary>Optional logical index name. Properties sharing the name form one composite index.</summary>
    public string? Name { get; }

    /// <summary>Column position within a composite index.</summary>
    public int Order { get; set; }

    /// <summary>Index key direction.</summary>
    public IndexDirection Direction { get; set; } = IndexDirection.Ascending;
}
