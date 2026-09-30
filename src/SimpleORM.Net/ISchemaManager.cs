#pragma warning disable CS1591
using SimpleORM.Net.Runtime;
namespace SimpleORM.Net.Abstractions;
/// <summary>Creates and evolves runtime entities without changing IDataRepository.</summary>
public interface ISchemaManager : IRuntimeSchemaProvider
{
}
