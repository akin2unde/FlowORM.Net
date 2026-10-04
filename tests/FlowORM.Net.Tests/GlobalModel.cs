using FlowORM.Net.Attributes;
using FlowORM.Net.Models;

namespace FlowORM.Net.Tests;

[Global]
internal sealed class GlobalModel : DBModel
{
    public string Name { get; set; } = string.Empty;
}
