using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Abstractions;

/// <summary>Provider startup schema/index synchronization.</summary>
public interface IDBSchemaSynchronizer
{

    /// <summary>Synchronizes.</summary>
    Task Synchronize(CancellationToken cancellationToken=default);

}
