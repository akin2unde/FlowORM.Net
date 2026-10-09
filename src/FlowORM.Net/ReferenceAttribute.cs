namespace FlowORM.Net.Attributes;

/// <summary>Declares a one-level, optionally searchable model relationship.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ReferenceAttribute(Type model, string foreignField) : Attribute
{
    /// <summary>The referenced DBModel type.</summary>
    public Type Model { get; } = model;

    /// <summary>The referenced model property matched against this property's value.</summary>
    public string ForeignField { get; } = foreignField;

    /// <summary>Properties on the referenced model eligible for text search.</summary>
    public string[] SearchFields { get; set; } = [];
}
