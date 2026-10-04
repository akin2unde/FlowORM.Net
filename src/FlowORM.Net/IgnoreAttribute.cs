using FlowORM.Net.Configuration;

namespace FlowORM.Net.Attributes;

/// <summary>Excludes a property from persistence.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreAttribute:Attribute
{
}
