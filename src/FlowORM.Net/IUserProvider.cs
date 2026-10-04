using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Abstractions;

/// <summary>Current user resolver.</summary>
public interface IUserProvider
{

    /// <summary>Current user.</summary>
    string? GetUserCode();

}
