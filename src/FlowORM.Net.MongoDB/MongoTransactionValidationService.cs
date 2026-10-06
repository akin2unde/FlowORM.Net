using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using FlowORM.Net.MongoDB.Options;

namespace FlowORM.Net.MongoDB;

/// <summary>
/// Validates the configured MongoDB transaction capability once during application startup.
/// </summary>
internal sealed class MongoTransactionValidationService(
    IMongoClient client,
    MongoDBOptions options) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.SupportTransactions)
        {
            return;
        }

        var admin = client.GetDatabase("admin");
        var hello = await admin.RunCommandAsync<BsonDocument>(
            new BsonDocument("hello", 1),
            cancellationToken: cancellationToken);

        var isReplicaSet = hello.TryGetValue("setName", out var setName)
            && setName.IsString
            && !string.IsNullOrWhiteSpace(setName.AsString);

        var isShardedCluster = hello.TryGetValue("msg", out var message)
            && message.IsString
            && string.Equals(message.AsString, "isdbgrid", StringComparison.Ordinal);

        if (!isReplicaSet && !isShardedCluster)
        {
            throw new InvalidOperationException(
                "MongoDB transactions are enabled in FlowORM, but the configured MongoDB deployment " +
                "is standalone and does not support multi-document transactions. Configure MongoDB " +
                "as a replica set or sharded cluster, or set SupportTransactions to false.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
