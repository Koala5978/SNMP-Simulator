using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SnmpSimulator.Core;

namespace SnmpSimulator.App;
public partial class MainWindow : Window
{
    private CancellationTokenSource? cancellation;
    private Task? running;
    private SnmpAgent? agent;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public MainWindow()
    {
        InitializeComponent();
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "device.json");
        ProfilePath.Text = File.Exists(defaultPath) ? defaultPath : "（內建範例）";
        timer.Tick += (_, _) => Counters.Text = agent is null ? "" : $"收到：{agent.Received:N0}　回應：{agent.Sent:N0}　丟棄：{agent.Dropped:N0}";
        timer.Start();
        Closed += (_, _) => { timer.Stop(); cancellation?.Cancel(); };
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
            DeviceProfile profile;
            if (ProfilePath.Text == "（內建範例）")
            {
                using var stream = typeof(App).Assembly.GetManifestResourceStream("DefaultDevice.json")!;
                using var reader = new StreamReader(stream);
                profile = DeviceProfile.Parse(reader.ReadToEnd());
            }
            else profile = DeviceProfile.Load(ProfilePath.Text);
            if (!IPAddress.TryParse(BindAddress.Text, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidDataException("請輸入本機 IPv4 位址。");
            if (!int.TryParse(Port.Text, out var port) || port is < 1 or > 65535) throw new InvalidDataException("Port 必須介於 1～65535。");
            agent = new SnmpAgent(profile);
            cancellation = new CancellationTokenSource();
            SetRunning(true);
            var currentAgent = agent;
            var token = cancellation.Token;
            running = Task.Run(() => currentAgent.RunAsync(new IPEndPoint(address, port), token,
                () => Dispatcher.Invoke(() => Status.Text = $"模擬中：{profile.Name} · {profile.Objects.Count} 個 OID · {address}:{port}")));
            await running;
            Status.Text = "已停止。";
        }
        catch (Exception ex) { Status.Text = $"無法執行：{ex.Message}"; }
        finally { cancellation?.Dispose(); cancellation = null; SetRunning(false); }
    }
    private void Stop_Click(object sender, RoutedEventArgs e) => cancellation?.Cancel();
    private void SetRunning(bool value)
    {
        StartButton.IsEnabled = BrowseButton.IsEnabled = ProfilePath.IsEnabled = BindAddress.IsEnabled = Port.IsEnabled = !value;
        StopButton.IsEnabled = value;
    }
}
