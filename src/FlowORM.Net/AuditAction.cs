using FlowORM.Net.Attributes;

using FlowORM.Net.Models;

namespace FlowORM.Net.SystemModels;

/// <summary>Audit operation.</summary>
public enum AuditAction
{

    /// <summary>Insert.</summary>
    Insert,
    /// <summary>Update.</summary>
    Update,
    /// <summary>Delete.</summary>
    Delete
}
