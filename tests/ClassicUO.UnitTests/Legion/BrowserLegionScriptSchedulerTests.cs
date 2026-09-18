using System;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.LegionScripting;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Legion;

public sealed class BrowserLegionScriptSchedulerTests
{
    [Fact]
    public async Task RunsScriptUntilItCompletes()
    {
        await using var scheduler = new BrowserLegionScriptScheduler();
        var runtime = new FakeRuntime(2);

        await scheduler.LoadAsync("demo.py", "API.Pause(0.1)", runtime);
        await scheduler.StartAsync("demo.py");
        await scheduler.PumpAsync();
        await scheduler.PumpAsync();

        scheduler.GetState("demo.py").Should().Be(BrowserLegionScriptState.Completed);
        runtime.StopCalls.Should().Be(1);
    }

    [Fact]
    public async Task StopsScriptWhenRuntimeReportsAnError()
    {
        await using var scheduler = new BrowserLegionScriptScheduler();
        var runtime = new FakeRuntime(1) { Error = new InvalidOperationException("script failed") };

        await scheduler.LoadAsync("broken.py", "raise Exception()", runtime);
        await scheduler.StartAsync("broken.py");
        await scheduler.PumpAsync();

        scheduler.GetState("broken.py").Should().Be(BrowserLegionScriptState.Faulted);
        runtime.StopCalls.Should().Be(1);
    }

    [Fact]
    public async Task RejectsDuplicateScriptIds()
    {
        await using var scheduler = new BrowserLegionScriptScheduler();
        await scheduler.LoadAsync("same.py", "", new FakeRuntime(1));

        Func<Task> loadAgain = () => scheduler.LoadAsync("same.py", "", new FakeRuntime(1)).AsTask();
        await loadAgain.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TerminatesRuntimeThatExceedsWatchdogLimit()
    {
        await using var scheduler = new BrowserLegionScriptScheduler(
            sliceBudget: TimeSpan.FromMilliseconds(1),
            watchdogLimit: TimeSpan.FromMilliseconds(5));
        var runtime = new FakeRuntime(1, TimeSpan.FromMilliseconds(20));

        await scheduler.LoadAsync("busy.py", "while True: pass", runtime);
        await scheduler.StartAsync("busy.py");
        await scheduler.PumpAsync();

        scheduler.GetState("busy.py").Should().Be(BrowserLegionScriptState.WatchdogTerminated);
        runtime.StopCalls.Should().Be(1);
    }

    private sealed class FakeRuntime(int slices, TimeSpan? pumpDelay = null) : IBrowserLegionScriptRuntime
    {
        private int _remaining = slices;
        private readonly TimeSpan _pumpDelay = pumpDelay ?? TimeSpan.Zero;
        public Exception Error { get; init; }
        public int StopCalls { get; private set; }

        public ValueTask LoadAsync(string scriptId, string source, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async ValueTask<LegionScriptPumpResult> PumpAsync(TimeSpan budget, CancellationToken cancellationToken = default)
        {
            if (_pumpDelay > TimeSpan.Zero)
                await Task.Delay(_pumpDelay, cancellationToken);

            if (Error is not null)
                return new LegionScriptPumpResult(false, true, Error);

            _remaining--;
            return new LegionScriptPumpResult(_remaining <= 0, _remaining > 0);
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
