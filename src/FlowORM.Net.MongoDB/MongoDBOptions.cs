namespace FlowORM.Net.MongoDB.Options;

/// <summary>
/// Contains configuration used by the MongoDB provider.
/// </summary>
public sealed class MongoDBOptions
{
    /// <summary>
    /// Gets or sets whether MongoDB fields that do not have matching
    /// model properties should be ignored during deserialization.
    /// </summary>
    /// <remarks>
    /// When false, MongoDB throws a FormatException when a document
    /// contains a field that is not represented by the model.
    /// When true, unknown fields are ignored.
    /// </remarks>
    public bool IgnoreMongoId { get; set; }

    /// <summary>
    /// Gets or sets whether the configured MongoDB deployment supports transactions.
    /// Defaults to <see langword="false"/> for standalone MongoDB compatibility.
    /// </summary>
    /// <remarks>
    /// When enabled, FlowORM validates the MongoDB topology once during application startup.
    /// The application fails to start when the deployment is neither a replica set nor a
    /// sharded cluster. When disabled, transaction callbacks still execute, but FlowORM does
    /// not start a MongoDB transaction and rollback semantics are therefore unavailable.
    /// </remarks>
    public bool SupportTransactions { get; set; }
}
