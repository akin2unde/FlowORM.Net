using MongoDB.Driver;
using FlowORM.Net.Abstractions;

namespace FlowORM.Net.MongoDB;

/// <summary>
/// Wraps an optional MongoDB client session transaction behind the provider-neutral
/// <see cref="IDBTransaction"/> contract.
/// </summary>
internal sealed class MongoTx : IDBTransaction
{
    /// <summary>Creates a non-transactional Mongo operation scope.</summary>
    public MongoTx()
    {
    }

    /// <summary>Creates a MongoDB transaction backed by the supplied session.</summary>
    /// <param name="session">The MongoDB client session with an active transaction.</param>
    public MongoTx(IClientSessionHandle session)
    {
        Session = session;
    }

    /// <summary>Gets the underlying MongoDB client session when transactions are enabled.</summary>
    public IClientSessionHandle? Session { get; }

    /// <summary>Gets whether this scope represents a real MongoDB transaction.</summary>
    public bool IsTransactional => Session is not null;

    /// <inheritdoc />
    public async Task Commit(CancellationToken cancellationToken = default)
    {
        if (Session is not null)
        {
            await Session.CommitTransactionAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task Rollback(CancellationToken cancellationToken = default)
    {
        if (Session is not null)
        {
            await Session.AbortTransactionAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Session?.Dispose();
        return ValueTask.CompletedTask;
    }
}
