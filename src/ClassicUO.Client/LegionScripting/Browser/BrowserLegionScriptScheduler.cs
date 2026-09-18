using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.LegionScripting;

/// <summary>Lifecycle state of a browser Legion script.</summary>
public enum BrowserLegionScriptState
{
    Loaded,
    Running,
    Stopped,
    Completed,
    Faulted,
    WatchdogTerminated
}

/// <summary>
/// Schedules browser Legion scripts cooperatively on the game loop. A single scheduler tick never
/// runs scripts concurrently, which keeps the implementation compatible with iOS Safari's default
/// single-threaded WebAssembly runtime.
/// </summary>
public sealed class BrowserLegionScriptScheduler : IAsyncDisposable
{
    private sealed class Entry
    {
        public required string Id;
        public required IBrowserLegionScriptRuntime Runtime;
        public BrowserLegionScriptState State;
        public Exception Error;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _sliceBudget;
    private readonly TimeSpan _watchdogLimit;
    private bool _disposed;

    /// <summary>Creates a scheduler with conservative defaults suitable for a mobile browser.</summary>
    public BrowserLegionScriptScheduler(
        TimeSpan? sliceBudget = null,
        TimeSpan? watchdogLimit = null)
    {
        _sliceBudget = sliceBudget ?? TimeSpan.FromMilliseconds(4);
        _watchdogLimit = watchdogLimit ?? TimeSpan.FromMilliseconds(100);

        if (_sliceBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sliceBudget));
        if (_watchdogLimit < _sliceBudget)
            throw new ArgumentOutOfRangeException(nameof(watchdogLimit));
    }

    /// <summary>Returns the current state for a script, or <c>null</c> when it is not registered.</summary>
    public BrowserLegionScriptState? GetState(string scriptId) =>
        _entries.TryGetValue(scriptId, out Entry entry) ? entry.State : null;

    /// <summary>Loads a script into the scheduler. IDs must be unique within this scheduler.</summary>
    public async ValueTask LoadAsync(
        string scriptId,
        string source,
        IBrowserLegionScriptRuntime runtime,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(scriptId);
        ArgumentNullException.ThrowIfNull(runtime);

        if (_entries.ContainsKey(scriptId))
            throw new InvalidOperationException($"A Legion script with ID '{scriptId}' is already loaded.");

        await runtime.LoadAsync(scriptId, source ?? string.Empty, cancellationToken);
        _entries.Add(scriptId, new Entry { Id = scriptId, Runtime = runtime, State = BrowserLegionScriptState.Loaded });
    }

    /// <summary>Starts a loaded script.</summary>
    public async ValueTask StartAsync(string scriptId, CancellationToken cancellationToken = default)
    {
        Entry entry = GetRequired(scriptId);
        if (entry.State != BrowserLegionScriptState.Loaded)
            throw new InvalidOperationException($"Script '{scriptId}' is not in the loaded state.");

        await entry.Runtime.StartAsync(cancellationToken);
        entry.State = BrowserLegionScriptState.Running;
    }

    /// <summary>
    /// Pumps each running script once. Call this from the browser game loop and await its result
    /// before rendering the next frame.
    /// </summary>
    public async ValueTask PumpAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (Entry entry in _entries.Values)
        {
            if (entry.State != BrowserLegionScriptState.Running)
                continue;

            LegionScriptPumpResult result;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                result = await entry.Runtime.PumpAsync(_sliceBudget, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await FaultAsync(entry, exception, cancellationToken);
                continue;
            }

            if (stopwatch.Elapsed > _watchdogLimit)
            {
                await TerminateForWatchdogAsync(entry, cancellationToken);
                continue;
            }

            if (result.Error is not null)
            {
                await FaultAsync(entry, result.Error, cancellationToken);
            }
            else if (result.Completed)
            {
                entry.State = BrowserLegionScriptState.Completed;
                await entry.Runtime.StopAsync(cancellationToken);
            }
        }
    }

    /// <summary>Requests a cooperative stop for a running script.</summary>
    public async ValueTask StopAsync(string scriptId, CancellationToken cancellationToken = default)
    {
        Entry entry = GetRequired(scriptId);
        if (entry.State is not BrowserLegionScriptState.Running and not BrowserLegionScriptState.Loaded)
            return;

        await entry.Runtime.StopAsync(cancellationToken);
        entry.State = BrowserLegionScriptState.Stopped;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (Entry entry in _entries.Values)
        {
            if (entry.State == BrowserLegionScriptState.Running)
                await entry.Runtime.StopAsync();
            await entry.Runtime.DisposeAsync();
        }

        _entries.Clear();
    }

    private Entry GetRequired(string scriptId) =>
        _entries.TryGetValue(scriptId, out Entry entry)
            ? entry
            : throw new KeyNotFoundException($"Legion script '{scriptId}' is not loaded.");

    private static async ValueTask FaultAsync(Entry entry, Exception exception, CancellationToken cancellationToken)
    {
        entry.Error = exception;
        entry.State = BrowserLegionScriptState.Faulted;
        await entry.Runtime.StopAsync(cancellationToken);
    }

    private static async ValueTask TerminateForWatchdogAsync(Entry entry, CancellationToken cancellationToken)
    {
        entry.State = BrowserLegionScriptState.WatchdogTerminated;
        entry.Error = new TimeoutException($"Legion script '{entry.Id}' exceeded its browser execution budget.");
        await entry.Runtime.StopAsync(cancellationToken);
    }
}
