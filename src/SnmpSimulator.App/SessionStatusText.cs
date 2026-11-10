using SnmpSimulator.Core;

namespace SnmpSimulator.App;

// No WPF dependency: the same rendering rule is linked into the cross-platform tests.
public static class SessionStatusText
{
    public static string WindowTitle(Version version) => $"SNMP Simulator v{version.Major}.{version.Minor}.{version.Build}";

    public static string Format(SessionSnapshot snapshot) => snapshot.State switch
    {
        SessionState.Starting => "載入及啟動中…",
        SessionState.Running => $"模擬中：{snapshot.DeviceName} · {snapshot.OidCount} 個 OID · {snapshot.Endpoint}",
        SessionState.Stopping => "停止中…",
        _ => snapshot.LastFailure is null ? "已停止。" : $"無法執行：{snapshot.LastFailure.Message}"
    };
}
