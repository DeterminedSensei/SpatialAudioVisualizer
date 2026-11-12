
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SpatialAudioRadar;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer;

    public OverlayWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };

        _timer.Tick += (_, _) => Radar.InvalidateVisual();
        _timer.Start();
    }

    public void SetSize(double size)
    {
        Width = size;
        Height = size;
    }

    private void Radar_MouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
            // Ignore unsuccessful drag operations.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}

public sealed class RadarSurface : FrameworkElement
{
    private static readonly Brush BaseBrush =
        new SolidColorBrush(Color.FromRgb(76, 222, 255));

    private static readonly Brush RingBrush =
        new SolidColorBrush(Color.FromRgb(175, 220, 245));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);

        if (size <= 0)
            return;

        double radius = size * 0.43;
        double cx = ActualWidth / 2;
        double cy = ActualHeight / 2;

        float level = Math.Clamp(AudioState.Level, 0f, 1f);
        float angle = AudioState.Angle;

        var backdrop = new SolidColorBrush(Color.FromRgb(7, 17, 29))
        {
            Opacity = 0.9
        };
        var borderPen = new Pen(BaseBrush, 2);

        dc.DrawEllipse(
            backdrop,
            borderPen,
            new Point(cx, cy),
            size * 0.49,
            size * 0.49);

        // Radar rings.
        for (int i = 1; i <= 3; i++)
        {
            double r = radius * i / 3.0;

            var ringPen = new Pen(
                new SolidColorBrush(Color.FromRgb(175, 220, 245))
                {
                    Opacity = 0.42 + level * 0.18
                },
                1.5);

            dc.DrawEllipse(
                null,
                ringPen,
                new Point(cx, cy),
                r, r);
        }

        // Crosshair.
        var crossBrush = new SolidColorBrush(Color.FromRgb(175, 220, 245))
        {
            Opacity = 0.48
        };

        var crossPen = new Pen(crossBrush, 1.5);

        dc.DrawLine(
            crossPen,
            new Point(cx - radius, cy),
            new Point(cx + radius, cy));

        dc.DrawLine(
            crossPen,
            new Point(cx, cy - radius),
            new Point(cx, cy + radius));

        // Calculate beam direction.
        double radians = angle * 2 * Math.PI / 180.0;

        double beamLength = radius *
            (0.45 + level * 0.55);

        double beamWidth = 4 + level * 24;

        Point tip = new(
            cx + Math.Sin(radians) * beamLength,
            cy - Math.Cos(radians) * beamLength);

        double leftAngle =
            radians - beamWidth * Math.PI / 180.0;

        double rightAngle =
            radians + beamWidth * Math.PI / 180.0;

        Point left = new(
            cx + Math.Sin(leftAngle) * radius,
            cy - Math.Cos(leftAngle) * radius);

        Point right = new(
            cx + Math.Sin(rightAngle) * radius,
            cy - Math.Cos(rightAngle) * radius);

        // Radar beam.
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(
                new Point(cx, cy), true, true);

            context.LineTo(left, true, false);
            context.LineTo(right, true, false);
        }

        geometry.Freeze();

        var glow = new SolidColorBrush(
            Color.FromRgb(14, 165, 233))
        {
            Opacity = 0.1 + level * 0.28
        };

        dc.DrawGeometry(glow, null, geometry);

        // Direction line.
        var beamBrush = new SolidColorBrush(
            Color.FromRgb(76, 222, 255))
        {
            Opacity = 0.9 + level * 0.1
        };

        var beamPen = new Pen(
            beamBrush,
            3 + level * 3);

        beamPen.StartLineCap = PenLineCap.Round;
        beamPen.EndLineCap = PenLineCap.Round;

        dc.DrawLine(
            beamPen,
            new Point(cx, cy),
            tip);

        // Direction endpoint.
        dc.DrawEllipse(
            Brushes.White,
            null,
            tip,
            4 + level * 5,
            4 + level * 5);

        // Center point.
        dc.DrawEllipse(
            Brushes.White,
            null,
            new Point(cx, cy),
            3.5,
            3.5);

        // Direction indicator.
        DrawLabel(dc, "N", cx, cy - radius + 12);
        DrawLabel(dc, "S", cx, cy + radius - 12);
        DrawLabel(dc, "W", cx - radius + 12, cy);
        DrawLabel(dc, "E", cx + radius - 12, cy);
    }

    private static void DrawLabel(
        DrawingContext dc,
        string label,
        double x,
        double y)
    {
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"),
            12,
            Brushes.White,
            VisualTreeHelper.GetDpi(
                Application.Current.MainWindow)
                .PixelsPerDip);

        text.TextAlignment = TextAlignment.Center;

        dc.DrawText(
            text,
            new Point(x - text.Width / 2,
                      y - text.Height / 2));
    }
}
