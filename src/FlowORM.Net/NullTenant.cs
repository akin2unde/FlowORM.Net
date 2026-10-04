using FlowORM.Net.Abstractions;

namespace FlowORM.Net;

internal sealed class NullTenant : ITenantProvider
{

    public string? GetTenant() => null;

}
