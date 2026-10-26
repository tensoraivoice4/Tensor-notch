using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SILENTREAPER.DynamicNotch;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly PerformanceCounter? _cpuCounter;
    private bool _expanded;
    private DateTime _lastEvent = DateTime.MinValue;
    private double _lastCpu;

    private const double CompactWidth = 190;
    private const double CompactHeight = 36;
    private const double ExpandedWidth = 590;
    private const double ExpandedHeight = 224;

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }
        catch { _cpuCounter = null; }

        PositionAtTopCenter();
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateStatus();
        _timer.Start();
        UpdateStatus();
    }

    private void PositionAtTopCenter()
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null) return;
        var area = screen.Bounds;
        Left = area.Left + (area.Width - Width) / 2.0;
        Top = area.Top;
    }

    private void UpdateStatus()
    {
        try
        {
            // Adaptive mode: hide over detected full-screen foreground windows.
            if (IsFullscreenForegroundWindow())
            {
                if (IsVisible) Hide();
                return;
            }
            if (!IsVisible) Show();

            var power = SystemInformation.PowerStatus;
            var percent = power.BatteryLifePercent;
            if (percent >= 0 && percent <= 1)
            {
                var battery = (int)Math.Round(percent * 100);
                BatteryValue.Text = battery + "%";
                var onAc = power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
                BatteryState.Text = onAc ? "⚡ Charging / AC power" :
                    (power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) ? "No battery detected" : "On battery");
                CompactText.Text = onAc ? $"⚡  {battery}%" : $"◉  {battery}%";
                StatusDot.Fill = onAc
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 235, 190))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 226, 237));
            }
            else
            {
                BatteryValue.Text = "N/A";
                BatteryState.Text = "Battery unavailable";
                CompactText.Text = "SILENTREAPER";
            }

            if (_cpuCounter is not null)
            {
                try { _lastCpu = Math.Clamp(_cpuCounter.NextValue(), 0, 100); } catch { }
            }
            CpuValue.Text = $"{_lastCpu:0}%";

            var memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memory))
            {
                var usedPercent = memory.dwMemoryLoad;
                var usedGb = (memory.ullTotalPhys - memory.ullAvailPhys) / 1024d / 1024d / 1024d;
                var totalGb = memory.ullTotalPhys / 1024d / 1024d / 1024d;
                RamValue.Text = $"{usedPercent:0}%";
                RamState.Text = $"{usedGb:0.0} / {totalGb:0.0} GB used";
            }

            if (_expanded)
                FooterText.Text = DateTime.Now.ToString("ddd, dd MMM  •  HH:mm");
        }
        catch
        {
            // Keep the visual shell alive if a system metric is temporarily unavailable.
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) ToggleExpanded();
        else if (!_expanded) ToggleExpanded();
    }

    private void ToggleExpanded()
    {
        if (_expanded) Collapse();
        else Expand();
    }

    private void Expand()
    {
        _expanded = true;
        Show();
        ExpandedView.Visibility = Visibility.Visible;
        CompactView.Visibility = Visibility.Collapsed;
        AnimateSize(ExpandedWidth, ExpandedHeight);
        UpdateStatus();
    }

    private void Collapse()
    {
        _expanded = false;
        AnimateSize(CompactWidth, CompactHeight, () =>
        {
            ExpandedView.Visibility = Visibility.Collapsed;
            CompactView.Visibility = Visibility.Visible;
        });
    }

    private void AnimateSize(double width, double height, Action? completed = null)
    {
        var duration = TimeSpan.FromMilliseconds(420);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var widthAnimation = new DoubleAnimation(width, duration) { EasingFunction = ease };
        var heightAnimation = new DoubleAnimation(height, duration) { EasingFunction = ease };
        if (completed is not null) heightAnimation.Completed += (_, _) => completed();
        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(HeightProperty, heightAnimation);
        var screen = Screen.PrimaryScreen;
        if (screen is not null)
            Left = screen.Bounds.Left + (screen.Bounds.Width - width) / 2.0;
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => Collapse();
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => UpdateStatus();

    private void SystemEvents_PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        _lastEvent = DateTime.Now;
        Dispatcher.BeginInvoke(UpdateStatus);
    }

    private void SystemEvents_SessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
            Dispatcher.BeginInvoke(Hide);
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
            Dispatcher.BeginInvoke(() => { Show(); PositionAtTopCenter(); });
    }

    private static bool IsFullscreenForegroundWindow()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT rect)) return false;
            var foreground = new System.Drawing.Rectangle(rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top);
            foreach (var screen in Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                if (foreground.Width >= bounds.Width - 2 &&
                    foreground.Height >= bounds.Height - 2 &&
                    Math.Abs(foreground.Left - bounds.Left) <= 2 &&
                    Math.Abs(foreground.Top - bounds.Top) <= 2)
                    return true;
            }
        }
        catch { }
        return false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        _cpuCounter?.Dispose();
        base.OnClosed(e);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile;
        public ulong ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
}
