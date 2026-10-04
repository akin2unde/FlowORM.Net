using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Abstractions;

/// <summary>Current tenant resolver.</summary>
public interface ITenantProvider
{

    /// <summary>Current tenant.</summary>
    string? GetTenant();

}
