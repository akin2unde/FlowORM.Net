using Cronos;
using FlowORM.Net.Metadata;

namespace FlowORM.Net.Services;

internal sealed class StaleDataCleanupPolicy
{
    public StaleDataCleanupPolicy(
        DBModelMetadata metadata,
        int retentionDays,
        CronExpression expression)
    {
        Metadata = metadata;
        RetentionDays = retentionDays;
        Expression = expression;
    }

    public DBModelMetadata Metadata { get; }

    public int RetentionDays { get; }

    public CronExpression Expression { get; }
}
