using System.Collections;

using System.Dynamic;

using System.Globalization;

using System.Reflection;

using Microsoft.Data.SqlClient;

using Microsoft.Extensions.DependencyInjection;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.SqlServer;

/// <summary>SQL Server provider registration.</summary>
public static class SqlServerRegistration
{

    /// <summary>Registers provider services.</summary>
    public static IServiceCollection AddFlowOrmSqlServer(this IServiceCollection s)
    {
        s.AddScoped<SqlServerProvider>();

        s.AddScoped<IDatabaseProvider>(x=>x.GetRequiredService<SqlServerProvider>());

        s.AddScoped<IDBQuery>(x=>x.GetRequiredService<SqlServerProvider>());

        s.AddScoped<IDBSchemaSynchronizer,SqlServerSchemaSynchronizer>();

        s.AddScoped<SqlServerRuntimeProvider>();
        s.AddScoped<IRuntimeDatabaseProvider>(x => x.GetRequiredService<SqlServerRuntimeProvider>());
        s.AddScoped<IRuntimeSchemaProvider>(x => x.GetRequiredService<SqlServerRuntimeProvider>());

        return s;

    }

}
