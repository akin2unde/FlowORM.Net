using System.Linq.Expressions;

using System.Security.Cryptography;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Services;

/// <summary>Operational constants.</summary>
public static class OrmConstants
{

    /// <summary>Default batch.</summary>
    public const int DefaultBatchSize=100;

    /// <summary>Hard max batch.</summary>
    public const int MaximumPhysicalBatchSize=500;

    /// <summary>Minimum code suffix.</summary>
    public const int MinimumCodeLength=4;

}
