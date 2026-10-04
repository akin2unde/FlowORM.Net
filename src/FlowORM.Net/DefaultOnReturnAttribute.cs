using FlowORM.Net.Configuration;

namespace FlowORM.Net.Attributes;

/// <summary>Resets a property to default before read results are returned.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultOnReturnAttribute:Attribute
{
}
