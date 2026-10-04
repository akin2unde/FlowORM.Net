using System.Linq.Expressions;

using System.Security.Cryptography;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Services;

/// <summary>Random code generator.</summary>
public interface ICodeGenerator
{

    /// <summary>One code.</summary>
    string Generate<T>() where T:DBModel;

}
