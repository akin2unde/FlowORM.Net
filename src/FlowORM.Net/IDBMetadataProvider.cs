using System.Collections.Concurrent;

using System.Reflection;

using FlowORM.Net.Attributes;

using FlowORM.Net.Configuration;

using FlowORM.Net.Models;

namespace FlowORM.Net.Metadata;

/// <summary>Metadata cache contract.</summary>
public interface IDBMetadataProvider
{

    /// <summary>Gets metadata.</summary>
    DBModelMetadata GetMetadata<T>() where T:DBModel;

    /// <summary>Gets runtime metadata.</summary>
    DBModelMetadata GetMetadata(Type type);

    /// <summary>Registers metadata.</summary>
    void RegisterModel(Type type);

    /// <summary>All registered metadata.</summary>
    IReadOnlyCollection<DBModelMetadata> GetRegisteredModels();

}
