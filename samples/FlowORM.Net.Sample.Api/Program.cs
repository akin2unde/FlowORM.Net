using FlowORM.Net;
using FlowORM.Net.Abstractions;
using FlowORM.Net.AspNetCore;
using FlowORM.Net.Configuration;
using FlowORM.Net.Metadata;
using FlowORM.Net.MongoDB;
using FlowORM.Net.Services;
using FlowORM.Net.SqlServer;

namespace FlowORM.Net.Sample.Api;

/// <summary>
/// Sample application entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Builds, configures, and runs the sample API.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the application.</param>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<ScopedExecutionExamples>();

        var provider = ResolveProvider(
            builder.Configuration["FlowOrm:Provider"]);

        builder.Services.AddFlowOrm(
            options => ConfigureFlowOrm(
                options,
                builder.Configuration,
                provider),
            typeof(Customer).Assembly,
            typeof(FlowORM.Net.SystemModels.DBExtensionDefinition).Assembly);

        if (provider == DatabaseType.MongoDb)
        {
            builder.Services.AddFlowOrmMongoDB();
        }
        else
        {
            builder.Services.AddFlowOrmSqlServer();
        }

        builder.Services.AddFlowOrmAspNetCore();

        // AddFlowOrm registers the built-in database-backed extension service.
        // Customer.Extended therefore works without replacing IExtensionService.
        builder.Services.AddScoped<ICustomerService, CustomerService>();

        builder.Services.AddControllers();

        var app = builder.Build();

        await SynchronizeSchema(
            app.Services,
            app.Lifetime.ApplicationStopping);

        var simpleOrmOptions = app.Services.GetRequiredService<FlowOrmOptions>();

        if (simpleOrmOptions.ErrorLog.Enabled)
        {
            app.UseFlowOrmErrors();
        }

        app.MapControllers();

        await app.RunAsync();
    }

    private static DatabaseType ResolveProvider(string? provider)
    {
        return provider?.Equals(
            "MongoDb",
            StringComparison.OrdinalIgnoreCase) == true
            ? DatabaseType.MongoDb
            : DatabaseType.SqlServer;
    }

    private static void ConfigureFlowOrm(
        FlowOrmOptions options,
        IConfiguration configuration,
        DatabaseType provider)
    {
        options.Database = provider;
        // Alternatively provide the complete provider connection string. When non-empty,
        // ConnectionString takes precedence over Host/Port/DatabaseName/credentials below.
        // options.Connection.ConnectionString = configuration.GetConnectionString("FlowOrm");
        options.Connection.Host = configuration["FlowOrm:Host"] ?? "localhost";
        options.Connection.Port = ResolvePort(
            configuration["FlowOrm:Port"],
            provider);
        options.Connection.DatabaseName = configuration["FlowOrm:Database"]
            ?? "FlowOrmSample";
        options.Connection.Username = configuration["FlowOrm:Username"];
        options.Connection.Password = configuration["FlowOrm:Password"];

        options.DefaultStringLength = 50;
        options.EnumStorage = EnumStorage.String;
        options.CodeGeneration.Length = 10;
        options.Batch.Save = 100;
        options.Batch.Select = 100;
        options.Concurrency.Enabled = configuration.GetValue(
            "FlowOrm:Concurrency:Enabled",
            true);
        options.AutoMigration = true;

        options.Extensions.RequirePublish = configuration.GetValue(
            "FlowOrm:Extensions:RequirePublish",
            false);

        options.MultiTenancy.Enabled = configuration.GetValue(
            "FlowOrm:MultiTenancy:Enabled",
            false);

        options.MultiTenancy.JwtClaim = configuration[
            "FlowOrm:MultiTenancy:JwtClaim"] ?? "tenant";

        options.Search.IncludeTenantCode = configuration.GetValue(
            "FlowOrm:Search:IncludeTenantCode",
            false);

        options.AuditTrail.Enabled = configuration.GetValue(
            "FlowOrm:AuditTrail:Enabled",
            false);

        options.ErrorLog.Enabled = configuration.GetValue(
            "FlowOrm:ErrorLog:Enabled",
            false);

        options.ErrorLog.AutoDeleteEnabled = configuration.GetValue(
            "FlowOrm:ErrorLog:AutoDeleteEnabled",
            false);

        options.ErrorLog.RetentionDays = configuration.GetValue(
            "FlowOrm:ErrorLog:RetentionDays",
            60);

        options.ErrorLog.CleanupCron = configuration[
            "FlowOrm:ErrorLog:CleanupCron"] ?? "0 0 1 * *";
    }

    private static int ResolvePort(
        string? configuredPort,
        DatabaseType provider)
    {
        if (int.TryParse(
                configuredPort,
                out var port))
        {
            return port;
        }

        return provider == DatabaseType.MongoDb
            ? 27017
            : 1433;
    }

    private static async Task SynchronizeSchema(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();

        var metadata = scope.ServiceProvider.GetRequiredService<IDBMetadataProvider>();
        var registry = scope.ServiceProvider.GetRequiredService<ModelAssemblyRegistry>();

        foreach (var modelType in ModelDiscovery.Discover(registry.Assemblies))
        {
            metadata.RegisterModel(modelType);
        }

        var synchronizer = scope.ServiceProvider.GetRequiredService<IDBSchemaSynchronizer>();

        await synchronizer.Synchronize(
            cancellationToken);
    }
}
