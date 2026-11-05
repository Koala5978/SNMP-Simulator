using System.Net;

namespace SnmpSimulator.Core;

public enum SessionState { Stopped, Starting, Running, Stopping, Faulted }

// A session serializes lifecycle ownership. Callbacks never depend on a UI dispatcher.
public sealed class SimulatorSession : IAsyncDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task? execution;
    private volatile SessionState state = SessionState.Stopped;
    public SessionState State => state;
    public SnmpAgent? Agent { get; private set; }
    public string? Error { get; private set; }
    public string? DeviceName { get; private set; }
    public int OidCount { get; private set; }
    public event Action<SessionState>? Changed;

    public Task StartAsync(Func<CancellationToken, Task<DeviceProfile>> load, IPEndPoint endpoint)
    {
        lock (gate)
        {
            if (execution is { IsCompleted: false }) throw new InvalidOperationException("請先停止目前的模擬。");
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            Error = null; Agent = null; DeviceName = null; OidCount = 0;
            state = SessionState.Starting;
            execution = Task.Run(async () =>
            {
                try
                {
                    var profile = await load(token);
                    token.ThrowIfCancellationRequested();
                    var agent = new SnmpAgent(profile);
                    Agent = agent; DeviceName = profile.Name; OidCount = profile.Objects.Count;
                    await agent.RunAsync(endpoint, token, () => SetState(SessionState.Running, token));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex) { Error = ex.Message; SetState(SessionState.Faulted); }
                finally { SetState(SessionState.Stopped); }
            });
            return execution;
        }
    }
    public async Task StopAsync()
    {
        Task? task;
        lock (gate)
        {
            task = execution;
            if (task is { IsCompleted: false })
            {
                state = SessionState.Stopping;
                cancellation?.Cancel();
            }
        }
        if (task is not null) await task;
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        lock (gate) { cancellation?.Dispose(); cancellation = null; }
    }
    private void SetState(SessionState state, CancellationToken token = default)
    {
        lock (gate)
        {
            if (state == SessionState.Running && token.IsCancellationRequested) return;
            this.state = state;
        }
        Changed?.Invoke(state);
    }
}
