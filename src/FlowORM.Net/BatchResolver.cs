using System.Linq.Expressions;

using System.Security.Cryptography;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Services;

/// <summary>Batch resolver.</summary>
public static class BatchResolver
{

    /// <summary>Method override, then configured, then 100; capped at 500.</summary>
    public static int Resolve(int? requested,int configured)
    {
        var x=requested??configured;

        if(x<=0)x=100;

        return Math.Min(x,500);

    }

}
