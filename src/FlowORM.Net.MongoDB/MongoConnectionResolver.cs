using MongoDB.Driver;
using FlowORM.Net.Configuration;

namespace FlowORM.Net.MongoDB;

/// <summary>Resolves the MongoDB client connection and database name from FlowORM options.</summary>
internal static class MongoConnectionResolver
{
    public static string ResolveConnectionString(FlowOrmOptions options)
    {
        var connection = options.Connection;

        if (!string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            return connection.ConnectionString;
        }

        var databaseName = RequireDatabaseName(options);

        var connectionString = string.IsNullOrWhiteSpace(connection.Username)
            ? $"mongodb://{connection.Host}:{connection.Port}"
            : $"mongodb://{Uri.EscapeDataString(connection.Username)}:{Uri.EscapeDataString(connection.Password ?? string.Empty)}@{connection.Host}:{connection.Port}/{databaseName}";

        return connectionString + "?replicaSet=rs0&directConnection=true";
    }

    public static string RequireDatabaseName(FlowOrmOptions options)
    {
        var connection = options.Connection;

        if (!string.IsNullOrWhiteSpace(connection.DatabaseName))
        {
            return connection.DatabaseName;
        }

        if (!string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            var databaseName = MongoUrl.Create(connection.ConnectionString).DatabaseName;

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                return databaseName;
            }
        }

        throw new InvalidOperationException(
            "FlowORM MongoDB requires a DatabaseName. Set Connection.DatabaseName or include the database name in Connection.ConnectionString.");
    }
}
