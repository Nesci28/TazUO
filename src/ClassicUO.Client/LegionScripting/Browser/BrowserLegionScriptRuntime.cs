using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.LegionScripting;

/// <summary>Result returned by a browser Legion runtime after one cooperative execution slice.</summary>
public readonly record struct LegionScriptPumpResult(
    bool Completed,
    bool Yielded,
    Exception Error = null);

/// <summary>
/// Adapter implemented by a browser Python runtime (for example, a JavaScript/WASM Python host).
/// The runtime must return control regularly from <see cref="PumpAsync"/>; it must never block the
/// browser event loop while waiting for a Legion API operation.
/// </summary>
public interface IBrowserLegionScriptRuntime : IAsyncDisposable
{
    /// <summary>Loads and validates a script without starting it.</summary>
    ValueTask LoadAsync(string scriptId, string source, CancellationToken cancellationToken = default);

    /// <summary>Starts the previously loaded script.</summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one bounded slice. The implementation must yield when the script calls a wait, callback
    /// pump, or reaches the supplied execution budget.
    /// </summary>
    ValueTask<LegionScriptPumpResult> PumpAsync(TimeSpan budget, CancellationToken cancellationToken = default);

    /// <summary>Requests cooperative cancellation and releases script resources.</summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
