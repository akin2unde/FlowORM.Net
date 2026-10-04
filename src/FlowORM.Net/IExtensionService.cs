using System.Linq.Expressions;

using System.Security.Cryptography;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Services;

/// <summary>Dynamic extension service contract.</summary>
public interface IExtensionService
{

    /// <summary>Load model extension values.</summary>
    Task Load<T>(IReadOnlyList<T> models,CancellationToken cancellationToken=default) where T:DBModel;

    /// <summary>Save extension values in business transaction.</summary>
    Task Save<T>(IReadOnlyList<T> models,IDBTransaction transaction,CancellationToken cancellationToken=default) where T:DBModel;

}
