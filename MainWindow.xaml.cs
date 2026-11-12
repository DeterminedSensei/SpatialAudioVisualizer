
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SpatialAudioRadar;

public sealed record ApplicationItem(int Id, string Label)
{
    public override string ToString() => Label;
}

public partial class MainWindow : Window
{
    private readonly AudioCaptureService _audio = new();

    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();

        RefreshApplications();
        _overlay = new OverlayWindow();
    }

    private void RefreshApplications()
    {
        SourceBox.Items.Clear();

        SourceBox.Items.Add(
            new ApplicationItem(0, "System-wide audio"));

        var applications = new List<ApplicationItem>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId)
                    continue;

                string title = process.MainWindowTitle;

                if (process.MainWindowHandle == IntPtr.Zero ||
                    string.IsNullOrWhiteSpace(title))
                    continue;

                applications.Add(
                    new ApplicationItem(
                        process.Id,
                        $"{title} ({process.ProcessName})"));
            }
            catch
            {
                // Some processes cannot be inspected.
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var application in applications.OrderBy(item => item.Label))
            SourceBox.Items.Add(application);

        SourceBox.SelectedIndex = 0;
    }

    private void Refresh_Click(
        object sender, RoutedEventArgs e)
    {
        RefreshApplications();
    }

    private async void Start_Click(
        object sender, RoutedEventArgs e)
    {
        if (SourceBox.SelectedItem is not ApplicationItem item)
            return;

        try
        {
            StatusText.Text = "Status: Starting...";

            await _audio.StartAsync(item.Id);

            if (OverlayEnabled.IsChecked == true)
                _overlay?.Show();

            StatusText.Text =
                $"Status: Monitoring {item.Label}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Status: Capture failed";

            MessageBox.Show(
                ex.Message,
                "Audio capture error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void Stop_Click(
        object sender, RoutedEventArgs e)
    {
        await _audio.StopAsync();

        StatusText.Text = "Status: Stopped";
    }

    private void Sensitivity_Changed(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        SpatialAnalyzer.Sensitivity =
            (float)e.NewValue;
    }

    private void Size_Changed(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        _overlay?.SetSize(e.NewValue);
    }

    protected override void OnClosed(EventArgs e)
    {
        _audio.Dispose();
        _overlay?.Close();

        base.OnClosed(e);
    }
}
