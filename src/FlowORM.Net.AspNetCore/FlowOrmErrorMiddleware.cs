using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using Microsoft.AspNetCore.Builder;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;
using FlowORM.Net.Exceptions;

namespace FlowORM.Net.AspNetCore;

/// <summary>Optional request error middleware. It logs through ILogger and is designed for a DB error sink to be added without changing the middleware contract.</summary>
public sealed class FlowOrmErrorMiddleware(RequestDelegate next)
{

    /// <summary>Invokes middleware.</summary>
    public async Task InvokeAsync(HttpContext context,FlowOrmOptions o,ILogger<FlowOrmErrorMiddleware> log)
    {

        if(!o.ErrorLog.Enabled)
        {
            await next(context);

            return;

        }

        string? payload=null;

        if(o.ErrorLog.IncludePayload&&context.Request.ContentType?.Contains("json",StringComparison.OrdinalIgnoreCase)==true)
        {
            context.Request.EnableBuffering();

            using var sr=new StreamReader(context.Request.Body,Encoding.UTF8,false,leaveOpen:true);

            payload=await sr.ReadToEndAsync();

            context.Request.Body.Position=0;

            if(payload.Length>o.ErrorLog.MaxPayloadLength)payload=payload[..o.ErrorLog.MaxPayloadLength]+"[TRUNCATED]";

        }

        try
        {
            await next(context);

        }
        catch(Exception ex)
        {
            log.LogError(ex,"FlowORM.Net request failed. Trace={Trace} Url={Url} Method={Method} Payload={Payload}",context.TraceIdentifier,context.Request.Path,context.Request.Method,payload);

            if(!context.Response.HasStarted)
            {
                var concurrencyException = ex as DBConcurrencyException;
                context.Response.StatusCode = concurrencyException is null
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status409Conflict;

                context.Response.ContentType="application/json";

                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success=false,
                    message=concurrencyException?.Message ?? "An unexpected error occurred.",
                    traceCode=context.TraceIdentifier,
                    conflicts=concurrencyException?.Codes
                }
                ));

            }
            else throw;

        }

    }

}
