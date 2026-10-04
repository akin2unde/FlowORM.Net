using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Services;

namespace FlowORM.Net;

/// <summary>Assemblies scanned for DBModel types.</summary>
public sealed class ModelAssemblyRegistry(IEnumerable<Assembly> assemblies)
{

    /// <summary>Assemblies.</summary>
    public IReadOnlyList<Assembly> Assemblies
    {
        get;

    }
    =assemblies.Distinct().ToArray();

}
