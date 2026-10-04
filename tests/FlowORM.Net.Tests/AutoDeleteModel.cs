using FlowORM.Net.Attributes;
using FlowORM.Net.Models;

namespace FlowORM.Net.Tests;

[AutoDelete(60, "0 0 1 * *")]
internal sealed class AutoDeleteModel : DBModel
{
}
