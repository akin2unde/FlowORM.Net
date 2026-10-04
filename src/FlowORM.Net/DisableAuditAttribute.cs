using FlowORM.Net.Configuration;

namespace FlowORM.Net.Attributes;

/// <summary>Disables audit trail generation for a model.</summary>
[AttributeUsage(AttributeTargets.Class,Inherited=true)]
public sealed class DisableAuditAttribute:Attribute
{
}
