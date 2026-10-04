using FlowORM.Net.Attributes;
using FlowORM.Net.Models;

namespace FlowORM.Net.Tests;

[DisableConcurrencyCheck]
internal sealed class NoConcurrencyModel : DBModel
{
    public string Name { get; set; } = string.Empty;
}
