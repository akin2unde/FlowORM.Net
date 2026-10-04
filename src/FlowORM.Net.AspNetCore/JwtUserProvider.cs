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

/// <summary>JWT user resolver.</summary>
public sealed class JwtUserProvider(IHttpContextAccessor h,FlowOrmAspNetCoreOptions o):IUserProvider
{

    /// <inheritdoc />
    public string? GetUserCode()=>h.HttpContext?.User?.FindFirst(o.UserClaim)?.Value;

}
