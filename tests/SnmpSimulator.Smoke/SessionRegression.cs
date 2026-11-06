using System.Net;
using System.Net.Sockets;
using SnmpSimulator.App;
using SnmpSimulator.Core;

internal static class SessionRegression
{
    public static async Task RunAsync(DeviceProfile profile, Action<bool, string> check)
    {
        check(SessionStatusText.WindowTitle(new Version(0, 1, 1, 0)) == "SNMP Simulator v0.1.1", "window version formatting has three components");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var session = new SimulatorSession();
        var loads = 0;
        Task<DeviceProfile> Load(CancellationToken _) { loads++; return Task.FromResult(profile); }
        foreach (var ip in new[] { "invalid", "::1", "" })
        {
            await session.StartAsync(Load, ip, "161");
            var failed = session.Snapshot;
            var text = SessionStatusText.Format(failed);
            check(failed.State == SessionState.Stopped && failed.LastFailure?.Kind == SessionFailureKind.InputValidation, "invalid IPv4 produces persistent input failure");
            check(Enumerable.Range(0, 100).All(_ => SessionStatusText.Format(session.Snapshot) == text), "100 UI projections preserve IPv4 failure");
        }
        foreach (var port in new[] { "abc", "0", "65536", "-1", "" })
        {
            await session.StartAsync(Load, "127.0.0.1", port);
            check(session.Snapshot.LastFailure?.Kind == SessionFailureKind.InputValidation && session.Error!.Contains("port"), "invalid port replaces preceding error");
        }
        check(loads == 0 && session.Agent is null, "invalid endpoint never loads a profile or creates an agent");
        var first = session.Snapshot;
        _ = session.Snapshot; // Merely reading/rendering state cannot dismiss an error.
        check(ReferenceEquals(first, session.Snapshot), "snapshot polling has no mutation");

        await session.StartAsync(_ => Task.FromException<DeviceProfile>(new FileNotFoundException()), new IPEndPoint(IPAddress.Loopback, 0));
        check(session.Snapshot.LastFailure?.Kind == SessionFailureKind.ProfileLoad && session.Error!.Contains("找不到"), "missing profile has an actionable failure");
        await session.StartAsync(Load, "invalid", "161");
        check(session.Snapshot.LastFailure?.Kind == SessionFailureKind.InputValidation && !session.Error!.Contains("找不到"), "new validation error replaces prior session failure");
        var latest = SessionStatusText.Format(session.Snapshot);
        check(Enumerable.Range(0, 100).All(_ => SessionStatusText.Format(session.Snapshot) == latest), "refresh never resurrects previous profile error");

        var invalidFile = Path.GetTempFileName();
        try
        {
            foreach (var data in new[] { "{", "{\"schemaVersion\":1,\"name\":\"x\",\"community\":\"public\",\"objects\":[{\"oid\":\"1.3.6.0\",\"type\":\"Integer32\",\"value\":1},{\"oid\":\"1.3.6.0\",\"type\":\"Integer32\",\"value\":1}]}" })
            {
                await File.WriteAllTextAsync(invalidFile, data, timeout.Token);
                await session.StartAsync(token => DeviceProfile.LoadAsync(invalidFile, token), "127.0.0.1", "1161");
                check(session.State == SessionState.Stopped && session.Snapshot.LastFailure?.Kind == SessionFailureKind.ProfileLoad, "bad JSON or duplicate OID fails without a listener");
            }
        }
        finally { File.Delete(invalidFile); }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = session.StartAsync(async token =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return profile;
        }, "127.0.0.1", "1161");
        await started.Task.WaitAsync(timeout.Token);
        check(session.State == SessionState.Starting && session.Snapshot.LastFailure is null, "accepted valid start clears old failure before load completes");
        check(!session.CanStart, "start remains disabled until prior task completes");
        var valid = session.Snapshot;
        try { _ = session.StartAsync(Load, "invalid", "0"); throw new Exception("duplicate accepted"); }
        catch (InvalidOperationException) { check(ReferenceEquals(valid, session.Snapshot), "invalid duplicate start leaves active snapshot untouched"); }
        await session.StopAsync(); await running;
        check(session.State == SessionState.Stopped && session.Error is null, "normal startup cancellation has no failure");
        check(SessionStatusText.Format(session.Snapshot) == "已停止。", "normal cancellation renders a stopped status");

        // An unrelated cancellation must not masquerade as the user's normal Stop.
        await session.StartAsync(_ => Task.FromException<DeviceProfile>(new OperationCanceledException()), "127.0.0.1", "1161");
        check(session.Snapshot.LastFailure is not null, "unrequested cancellation remains an execution failure");

        using var occupied = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)occupied.Client.LocalEndPoint!;
        await session.StartAsync(Load, endpoint);
        check(session.State == SessionState.Stopped && session.Snapshot.LastFailure?.Kind == SessionFailureKind.Bind, "occupied endpoint is classified as bind failure");
        var bindText = SessionStatusText.Format(session.Snapshot);
        check(Enumerable.Range(0, 100).All(_ => SessionStatusText.Format(session.Snapshot) == bindText), "bind failure persists after background cleanup");
        occupied.Close();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += state => { if (state == SessionState.Running) ready.TrySetResult(); };
        var recovered = session.StartAsync(Load, endpoint);
        await ready.Task.WaitAsync(timeout.Token);
        var active = session.Snapshot;
        check(active.State == SessionState.Running && active.LastFailure is null && active.Endpoint == endpoint.ToString(), "same endpoint recovers with correct immutable metadata");
        check(SessionStatusText.Format(active).Contains(profile.Name), "running text uses current snapshot metadata");
        using (var client = new UdpClient())
        {
            // Fixed v2c GET sysDescr request, request-id 42; independent of the test encoder.
            var request = Convert.FromHexString("302602010104067075626C6963A01902012A020100020100300E300C06082B060102010101000500");
            await client.SendAsync(request, endpoint, timeout.Token);
            var response = await client.ReceiveAsync(timeout.Token);
            check(response.RemoteEndPoint.Equals(endpoint) && response.Buffer.Length > 0, "recovered session responds to a fixed UDP request");
        }
        var oldState = session.Snapshot;
        try { _ = session.StartAsync(Load, "invalid", "0"); throw new Exception("duplicate accepted"); }
        catch (InvalidOperationException) { check(ReferenceEquals(oldState, session.Snapshot), "running session unaffected by invalid duplicate"); }
        await session.StopAsync(); await recovered;
        check(session.Error is null && session.State == SessionState.Stopped && session.CanStart, "recovered session stops cleanly and permits retry");
        check(session.Snapshot.Agent is not null, "stopping retains the final counters source");

        var disposing = new SimulatorSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loading = disposing.StartAsync(async token => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return profile; }, endpoint);
        await entered.Task.WaitAsync(timeout.Token);
        await disposing.DisposeAsync(); await loading;
        try { _ = disposing.StartAsync(Load, endpoint); throw new Exception("disposed start accepted"); }
        catch (ObjectDisposedException) { check(true, "closing/disposed session rejects new starts"); }
        check(disposing.State == SessionState.Stopped && disposing.Error is null && !disposing.CanStart, "dispose awaits clean cancellation and disables future starts");
    }
}
