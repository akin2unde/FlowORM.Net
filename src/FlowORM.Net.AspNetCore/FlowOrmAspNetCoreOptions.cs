using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using Microsoft.AspNetCore.Builder;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

namespace FlowORM.Net.AspNetCore;

/// <summary>ASP.NET-specific options.</summary>
public sealed class FlowOrmAspNetCoreOptions
{

    /// <summary>User JWT claim.</summary>
    public string UserClaim
    {
        get;

        set;

    }
    ="sub";

    /// <summary>Payload encryption options.</summary>
    public PayloadEncryptionOptions Encryption
    {
        get;

    }
    =new();

}
