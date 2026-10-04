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

/// <summary>JWT tenant resolver.</summary>
public sealed class JwtTenantProvider(IHttpContextAccessor h, FlowOrmOptions o) : ITenantProvider
{

    /// <inheritdoc />
    public string? GetTenant() => h.HttpContext?.User?.FindFirst(o.MultiTenancy.JwtClaim)?.Value;

}
