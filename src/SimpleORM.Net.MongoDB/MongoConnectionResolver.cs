using MongoDB.Driver;
using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.MongoDB;

/// <summary>Resolves the MongoDB client connection and database name from SimpleORM options.</summary>
internal static class MongoConnectionResolver
{
    public static string ResolveConnectionString(SimpleOrmOptions options)
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

    public static string RequireDatabaseName(SimpleOrmOptions options)
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
            "SimpleORM MongoDB requires a DatabaseName. Set Connection.DatabaseName or include the database name in Connection.ConnectionString.");
    }
}
