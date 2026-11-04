using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SnmpSimulator.Core;

namespace SnmpSimulator.App;

public partial class MainWindow : Window
{
    private readonly SimulatorSession session = new();
    private bool closing, allowClose;
    private string endpointText = "";
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public MainWindow()
    {
        InitializeComponent();
        Title = "SNMP Simulator v0.1.0";
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "device.json");
        ProfilePath.Text = File.Exists(defaultPath) ? defaultPath : "（內建範例）";
        // Poll state/counters instead of queuing a UI callback for each packet.
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Closing += Window_Closing;
    }
    private void Refresh()
    {
        var active = session.State is SessionState.Starting or SessionState.Running or SessionState.Stopping;
        StartButton.IsEnabled = BrowseButton.IsEnabled = ProfilePath.IsEnabled = BindAddress.IsEnabled = Port.IsEnabled = !active && !closing;
        StopButton.IsEnabled = active && session.State != SessionState.Stopping && !closing;
        Status.Text = session.State switch
        {
            SessionState.Starting => "載入及啟動中…",
            SessionState.Running => $"模擬中：{session.DeviceName} · {session.OidCount} 個 OID · {endpointText}",
            SessionState.Stopping => "停止中…",
            _ => session.Error is null ? "已停止。" : $"無法執行：{session.Error}"
        };
        var agent = session.Agent;
        Counters.Text = agent is null ? "" : $"收到：{agent.Received:N0}　送出：{agent.Sent:N0}　丟棄：{agent.Dropped:N0}";
    }
    private void Licenses_Click(object sender, RoutedEventArgs e)
    {
        var text = new System.Text.StringBuilder();
        foreach (var name in typeof(App).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("Licenses.")).Order())
        {
            using var stream = typeof(App).Assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            text.AppendLine(name).AppendLine(reader.ReadToEnd()).AppendLine();
        }
        var window = new Window
        {
            Title = "授權聲明",
            Width = 800,
            Height = 600,
            Owner = this,
            Content = new System.Windows.Controls.TextBox
            {
                Text = text.ToString(),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            }
        };
        window.Show();
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "設備設定檔 (*.json)|*.json" };
        if (dialog.ShowDialog() == true) ProfilePath.Text = dialog.FileName;
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!IPAddress.TryParse(BindAddress.Text, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidDataException("請輸入本機 IPv4 位址。");
            if (!int.TryParse(Port.Text, out var port) || port is < 1 or > 65535) throw new InvalidDataException("Port 必須介於 1～65535。");
            var source = ProfilePath.Text;
            endpointText = $"{address}:{port}";
            var running = session.StartAsync(async token =>
            {
                if (source != "（內建範例）") return await DeviceProfile.LoadAsync(source, token);
                using var stream = typeof(App).Assembly.GetManifestResourceStream("DefaultDevice.json")!;
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync(token);
                token.ThrowIfCancellationRequested();
                return DeviceProfile.Parse(json);
            }, new IPEndPoint(address, port));
            Refresh();
            await running;
            Refresh();
        }
        catch (Exception ex) { Status.Text = $"無法執行：{ex.Message}"; }
    }
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        var stopped = session.StopAsync(); Refresh(); await stopped; Refresh();
    }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        await session.DisposeAsync();
        timer.Stop(); allowClose = true; Close();
    }
}
