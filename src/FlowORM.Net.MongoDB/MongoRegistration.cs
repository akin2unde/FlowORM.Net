using MongoDB.Driver;
using FlowORM.Net.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FlowORM.Net.Abstractions;
using FlowORM.Net.MongoDB.Configuration;
using FlowORM.Net.MongoDB.Options;

namespace FlowORM.Net.MongoDB;

/// <summary>
/// MongoDB provider registration helpers.
/// </summary>
public static class MongoRegistration
{
    /// <summary>
    /// Registers the MongoDB provider and MongoDB index synchronizer.
    /// </summary>
    public static IServiceCollection AddFlowOrmMongoDB(
        this IServiceCollection services, Action<MongoDBOptions>? configure = null)
    {
        var options = new MongoDBOptions();

        configure?.Invoke(options);

        MongoDBConventionRegistrar.Register(options);

        services.AddSingleton(options);
        services.AddHostedService<MongoTransactionValidationService>();

        // MongoClient owns the driver's connection pools and is designed to be
        // long-lived. Both typed and runtime providers must use this same client
        // so transaction sessions are never passed across different clients.
        services.AddSingleton<IMongoClient>(provider =>
        {
            var simpleOrmOptions = provider.GetRequiredService<FlowOrmOptions>();
            var connectionString = MongoConnectionResolver.ResolveConnectionString(simpleOrmOptions);
            return new MongoClient(connectionString);
        });

        // MongoDatabaseProvider depends on ITenantProvider, which is scoped for
        // request-aware multi-tenancy. The provider therefore must not be singleton.
        services.AddScoped<MongoDatabaseProvider>();

        services.AddScoped<IDatabaseProvider>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBQuery>(
            provider => provider.GetRequiredService<MongoDatabaseProvider>());

        services.AddScoped<IDBSchemaSynchronizer, MongoIndexSynchronizer>();

        services.AddScoped<MongoRuntimeProvider>();
        services.AddScoped<IRuntimeDatabaseProvider>(provider => provider.GetRequiredService<MongoRuntimeProvider>());
        services.AddScoped<IRuntimeSchemaProvider>(provider => provider.GetRequiredService<MongoRuntimeProvider>());


        // Existing MongoDB registrations remain here.

        return services;
    }
}
