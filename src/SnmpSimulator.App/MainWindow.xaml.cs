using System.IO;
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
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public MainWindow()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version!;
        Title = SessionStatusText.WindowTitle(version);
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "device.json");
        ProfilePath.Text = File.Exists(defaultPath) ? defaultPath : "（內建範例）";
        // Poll state/counters instead of queuing a UI callback for each packet.
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Closing += Window_Closing;
    }
    private void Refresh()
    {
        var snapshot = session.Snapshot;
        var active = snapshot.IsActive;
        StartButton.IsEnabled = BrowseButton.IsEnabled = ProfilePath.IsEnabled = BindAddress.IsEnabled = Port.IsEnabled = session.CanStart && !closing;
        StopButton.IsEnabled = active && snapshot.State != SessionState.Stopping && !closing;
        Status.Text = SessionStatusText.Format(snapshot);
        var agent = snapshot.Agent;
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
        if (closing || !session.CanStart) return;
        var source = ProfilePath.Text;
        var running = session.StartAsync(async token =>
        {
            if (source != "（內建範例）") return await DeviceProfile.LoadAsync(source, token);
            using var stream = typeof(App).Assembly.GetManifestResourceStream("DefaultDevice.json")!;
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(token);
            token.ThrowIfCancellationRequested();
            return DeviceProfile.Parse(json);
        }, BindAddress.Text, Port.Text);
        Refresh();
        await running;
        Refresh();
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
        Refresh();
        await session.DisposeAsync();
        timer.Stop(); allowClose = true; Close();
    }
}
