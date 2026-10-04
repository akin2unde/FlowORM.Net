using SimpleORM.Net.Attributes;

namespace SimpleORM.Net.Models;

/// <summary>Paged or fetch-all result.</summary>
public sealed class PagedResult<T>
{

    /// <summary>Returned data.</summary>
    public IReadOnlyList<T> Data
    {
        get;

        init;

    }
    = Array.Empty<T>();

    /// <summary>Total matching records before skip/limit. Interpret only when TotalCalculated is true.</summary>
    public long TotalRecords
    {
        get;

        init;

    }

    /// <summary>Whether TotalRecords was calculated. False means the total is unknown; its numeric value is a placeholder.</summary>
    public bool TotalCalculated { get; init; } = true;

    /// <summary>Records skipped.</summary>
    public int Skipped
    {
        get;

        init;

    }

    /// <summary>Logical limit; zero means all remaining.</summary>
    public int Limit
    {
        get;

        init;

    }

}
