using System.Dynamic;

using System.Net.Http.Headers;

using System.Text;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

namespace FlowORM.Net.Http;

/// <summary>HTTP registration.</summary>
public static class HttpRegistration
{

    /// <summary>Registers wrapper.</summary>
    public static IServiceCollection AddFlowOrmHttp(this IServiceCollection s,Action<FlowOrmHttpOptions>? configure=null)
    {
        var o=new FlowOrmHttpOptions();

        configure?.Invoke(o);

        s.AddSingleton(o);

        s.AddHttpClient("FlowORM.Net.Http");

        s.AddTransient<IHttpService,HttpService>();

        return s;

    }

}
