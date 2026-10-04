using FlowORM.Net.Attributes;
using FlowORM.Net.Models;

namespace FlowORM.Net.Sample.Api;

/// <summary>
/// Sample customer model used to demonstrate FlowORM.Net end to end.
/// </summary>
[Extendable]
public sealed class Customer : DBModel
{
    /// <summary>
    /// Gets or sets the customer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer email address.
    /// </summary>
    [Unique]
    [DBColumn(Size = 150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer phone number.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value that should never be returned by normal ORM reads.
    /// </summary>
    [DefaultOnReturn]
    [NotSearchable]
    [DBColumn(Size = 250)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer status.
    /// </summary>
    [Index]
    public CustomerStatus Status { get; set; }

    /// <summary>Gets or sets the customer credit limit; used by the Sum sample.</summary>
    public decimal CreditLimit { get; set; }
}
