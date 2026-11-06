using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SnmpSimulator.Core;

public enum SessionState { Stopped, Starting, Running, Stopping, Faulted }
public enum SessionFailureKind { InputValidation, ProfileLoad, Bind, Execution }
public sealed record SessionFailure(SessionFailureKind Kind, string Message, string? Detail = null);
public sealed record SessionSnapshot(SessionState State, SessionFailure? LastFailure = null,
    string? DeviceName = null, int OidCount = 0, string? Endpoint = null, SnmpAgent? Agent = null)
{
    public bool IsActive => State is SessionState.Starting or SessionState.Running or SessionState.Stopping;
}

// Only this owner writes lifecycle and failures; UI polling cannot clear them.
public sealed class SimulatorSession : IAsyncDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task? execution;
    private bool disposed;
    private SessionSnapshot snapshot = new(SessionState.Stopped);
    public SessionSnapshot Snapshot { get { lock (gate) return snapshot; } }
    public bool CanStart { get { lock (gate) return !disposed && (execution is null || execution.IsCompleted); } }
    public SessionState State => Snapshot.State;
    public SnmpAgent? Agent => Snapshot.Agent;
    public string? Error => Snapshot.LastFailure?.Message;
    public string? DeviceName => Snapshot.DeviceName;
    public int OidCount => Snapshot.OidCount;
    public event Action<SessionState>? Changed;

    public Task StartAsync(Func<CancellationToken, Task<DeviceProfile>> load, string addressText, string portText)
    {
        ArgumentNullException.ThrowIfNull(load);
        lock (gate)
        {
            EnsureCanStart();
            if (!IPAddress.TryParse(addressText, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                return InvalidInput("請輸入本機 IPv4 位址，例如 127.0.0.1。");
            if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
                return InvalidInput("UDP port 必須為 1～65535 的整數，請修正後重新開始。");
            return StartUnderLock(load, new IPEndPoint(address, port));
        }
    }

    // Programmatic endpoint entry point shares the same lifecycle (port 0 permits test allocation).
    public Task StartAsync(Func<CancellationToken, Task<DeviceProfile>> load, IPEndPoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (gate)
        {
            EnsureCanStart();
            if (endpoint.AddressFamily != AddressFamily.InterNetwork)
                return InvalidInput("目前僅支援 IPv4，請選擇本機 IPv4 位址。");
            return StartUnderLock(load, new IPEndPoint(endpoint.Address, endpoint.Port));
        }
    }

    private void EnsureCanStart()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (execution is { IsCompleted: false }) throw new InvalidOperationException("請先停止目前的模擬。");
    }

    private Task InvalidInput(string message)
    {
        snapshot = snapshot with
        {
            State = SessionState.Stopped,
            LastFailure = new(SessionFailureKind.InputValidation, message)
        };
        return Task.CompletedTask;
    }

    private Task StartUnderLock(Func<CancellationToken, Task<DeviceProfile>> load, IPEndPoint endpoint)
    {
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        snapshot = new(SessionState.Starting, Endpoint: endpoint.ToString());
        execution = Task.Run(async () =>
        {
            var stage = SessionFailureKind.ProfileLoad;
            try
            {
                var profile = await load(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var agent = new SnmpAgent(profile);
                lock (gate) snapshot = snapshot with { Agent = agent, DeviceName = profile.Name, OidCount = profile.Objects.Count };
                stage = SessionFailureKind.Bind;
                await agent.RunAsync(endpoint, token, () =>
                {
                    stage = SessionFailureKind.Execution;
                    SetState(SessionState.Running, token);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                lock (gate) snapshot = snapshot with { LastFailure = DescribeFailure(stage, ex) };
                SetState(SessionState.Faulted);
            }
            finally { SetState(SessionState.Stopped); }
        });
        return execution;
    }

    private static SessionFailure DescribeFailure(SessionFailureKind kind, Exception ex)
    {
        var message = kind switch
        {
            SessionFailureKind.ProfileLoad when ex is FileNotFoundException or DirectoryNotFoundException => "找不到設備設定檔，請重新選擇存在的 JSON 檔案。",
            SessionFailureKind.ProfileLoad when ex is UnauthorizedAccessException => "無法讀取設定檔，請確認檔案讀取權限。",
            SessionFailureKind.ProfileLoad when ex is InvalidDataException => $"設備設定檔無效：{ex.Message} 請修正後重新開始。",
            SessionFailureKind.ProfileLoad => "設定檔載入失敗，請確認 JSON 內容與檔案是否可讀取。",
            SessionFailureKind.Bind when ex is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } => "UDP 端點已被使用，請停止占用服務或改用其他 port。",
            SessionFailureKind.Bind when ex is SocketException { SocketErrorCode: SocketError.AddressNotAvailable } => "無法綁定此位址，請使用本機網卡的 IPv4 位址。",
            SessionFailureKind.Bind when ex is SocketException { SocketErrorCode: SocketError.AccessDenied } => "系統拒絕綁定此端點，請確認權限或改用其他 port。",
            SessionFailureKind.Bind => "UDP 端點啟動失敗，請確認本機位址與 port 可用。",
            _ => "模擬執行失敗，請檢查本機網路後重新開始。"
        };
        // Preserve classification without exposing arbitrary stack traces or environment credentials.
        if (message.Length > 600) message = message[..600];
        return new(kind, message, ex.GetType().Name);
    }

    public async Task StopAsync()
    {
        Task? task;
        lock (gate)
        {
            task = execution;
            if (task is { IsCompleted: false })
            {
                snapshot = snapshot with { State = SessionState.Stopping };
                cancellation?.Cancel();
            }
        }
        if (task is not null) await task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate) disposed = true; // Reject new starts while asynchronous cleanup is in progress.
        await StopAsync().ConfigureAwait(false);
        lock (gate) { cancellation?.Dispose(); cancellation = null; }
    }

    private void SetState(SessionState state, CancellationToken token = default)
    {
        lock (gate)
        {
            if (state == SessionState.Running && token.IsCancellationRequested) return;
            snapshot = snapshot with { State = state };
        }
        Changed?.Invoke(state);
    }
}
