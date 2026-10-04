using SimpleORM.Net.Services;

namespace SimpleORM.Net.Abstractions;

/// <summary>Runs repository work in a fresh, asynchronously disposed dependency-injection scope.</summary>
/// <remarks>Each call uses the registered ORM setup and shared connection infrastructure.
/// An explicit tenant overrides identity only inside that scope. Omitted tenants use the existing provider.
/// Nested executor calls create independent scopes; use the supplied repository for one transaction.</remarks>
public interface ISimpleOrmExecutor
{
    /// <summary>Runs scoped repository work without starting an outer transaction.</summary>
    /// <typeparam name="TResult">Callback result type.</typeparam>
    /// <param name="work">Operation receiving the scoped repository and cancellation token.</param>
    /// <param name="tenant">Optional operation-specific tenant override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The callback result.</returns>
    Task<TResult> Run<TResult>(Func<IDataRepository, CancellationToken, Task<TResult>> work,
        string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>Runs scoped repository work returning no value.</summary>
    /// <param name="work">Operation receiving the scoped repository and cancellation token.</param>
    /// <param name="tenant">Optional operation-specific tenant override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Run(Func<IDataRepository, CancellationToken, Task> work,
        string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>Runs scoped repository work atomically through the registered transaction manager.</summary>
    /// <typeparam name="TResult">Callback result type.</typeparam>
    /// <param name="work">Operation receiving the scoped repository and cancellation token.</param>
    /// <param name="tenant">Optional operation-specific tenant override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The committed callback result.</returns>
    Task<TResult> Transaction<TResult>(Func<IDataRepository, CancellationToken, Task<TResult>> work,
        string? tenant = null, CancellationToken cancellationToken = default);

    /// <summary>Runs an atomic operation returning no value.</summary>
    /// <param name="work">Operation receiving the scoped repository and cancellation token.</param>
    /// <param name="tenant">Optional operation-specific tenant override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Transaction(Func<IDataRepository, CancellationToken, Task> work,
        string? tenant = null, CancellationToken cancellationToken = default);
}
