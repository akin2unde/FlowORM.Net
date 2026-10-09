using System.Reflection;
using System.Text.Json;
using FlowORM.Net.Abstractions;
using FlowORM.Net.Attributes;
using FlowORM.Net.Configuration;
using FlowORM.Net.Models;
using FlowORM.Net.Metadata;
using FlowORM.Net.SystemModels;

namespace FlowORM.Net.Services;

/// <summary>Persists audit entries using the same provider and transaction as the model write.</summary>
public sealed class DefaultAuditService(
    IDatabaseProvider provider,
    FlowOrmOptions options,
    IUserProvider userProvider,
    IDBMetadataProvider metadata) : IAuditService
{
    /// <inheritdoc />
    public async Task Write<T>(
        string action,
        IReadOnlyList<T> models,
        IDBTransaction transaction,
        CancellationToken cancellationToken = default)
        where T : DBModel
    {
        if (!options.AuditTrail.Enabled || models.Count == 0)
        {
            return;
        }

        if (!Enum.TryParse<AuditAction>(action, true, out var auditAction))
        {
            throw new ArgumentException($"Unknown audit action '{action}'.", nameof(action));
        }

        metadata.RegisterModel(typeof(DBAuditTrail));

        var now = DateTime.UtcNow;
        var userCode = userProvider.GetUserCode();
        var entries = models.Select(model => new DBAuditTrail
        {
            Code = $"AUD-{Guid.NewGuid():N}",
            Tenant = model.Tenant,
            ModelName = typeof(T).Name,
            RecordCode = model.Code,
            Action = auditAction,
            UserCode = userCode,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userCode,
            NewData = options.AuditTrail.IncludeNewValues && auditAction != AuditAction.Delete
                ? Serialize(model)
                : null
        }).ToArray();

        await provider.Insert(entries, transaction, cancellationToken);
    }

    private static string Serialize<T>(T model) where T : DBModel
    {
        var values = model.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead
                && property.GetIndexParameters().Length == 0
                && !Attribute.IsDefined(property, typeof(DoNotAuditAttribute), true)
                && !Attribute.IsDefined(property, typeof(IgnoreAttribute), true))
            .ToDictionary(property => property.Name, property => property.GetValue(model));

        return JsonSerializer.Serialize(values);
    }
}
