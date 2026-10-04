#pragma warning disable CS1591
using FlowORM.Net.Runtime;
namespace FlowORM.Net.Abstractions;
/// <summary>Creates and evolves runtime entities without changing IDataRepository.</summary>
public interface ISchemaManager : IRuntimeSchemaProvider
{
}
