using FlowORM.Net.Abstractions;

namespace FlowORM.Net;

internal sealed class NullUser : IUserProvider
{

    public string? GetUserCode() => null;

}
