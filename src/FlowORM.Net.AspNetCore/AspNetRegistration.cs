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

/// <summary>ASP.NET registration helpers.</summary>
public static class AspNetRegistration
{

    /// <summary>Registers JWT identity providers and options.</summary>
    public static IServiceCollection AddFlowOrmAspNetCore(this IServiceCollection s,Action<FlowOrmAspNetCoreOptions>? configure=null)
    {
        var o=new FlowOrmAspNetCoreOptions();

        configure?.Invoke(o);

        s.AddSingleton(o);

        s.AddHttpContextAccessor();

        s.AddScoped<ITenantProvider,JwtTenantProvider>();

        s.AddScoped<IUserProvider,JwtUserProvider>();

        return s;

    }

    /// <summary>Adds optional error middleware.</summary>
    public static IApplicationBuilder UseFlowOrmErrors(this IApplicationBuilder a)=>a.UseMiddleware<FlowOrmErrorMiddleware>();

    /// <summary>Adds optional encryption middleware.</summary>
    public static IApplicationBuilder UseFlowOrmEncryption(this IApplicationBuilder a)=>a.UseMiddleware<FlowOrmEncryptionMiddleware>();

}
