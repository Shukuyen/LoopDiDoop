using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace LoopDiDoop.Views;

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private Storyboard? _recPulseStoryboard;
    private Storyboard? _loopRotateStoryboard;
    private bool _isVisible;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        // Force HWND creation early so WS_EX_NOACTIVATE is attached before first Show
        new WindowInteropHelper(this).EnsureHandle();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _recPulseStoryboard = (Storyboard)FindResource("RecPulseStoryboard");
        _loopRotateStoryboard = (Storyboard)FindResource("LoopRotateStoryboard");
        Recenter();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Recenter();
    }

    private void Recenter()
    {
        Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = ActualWidth > 0 ? ActualWidth : (DesiredSize.Width > 0 ? DesiredSize.Width : 260);
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = Math.Max(0, (screenWidth - width) / 2.0);
        Top = 16.0;
    }

    public void ShowRecording(int maxSeconds)
    {
        Dispatcher.Invoke(() =>
        {
            RecordingPanel.Visibility = Visibility.Visible;
            LoopingPanel.Visibility = Visibility.Collapsed;
            TimerText.Text = $"0:00 / 0:{maxSeconds:D2}";
            LevelBar.Width = 0;

            _loopRotateStoryboard?.Stop();
            _recPulseStoryboard?.Begin();

            AnimateIn();
        });
    }

    public void UpdateAudioLevel(float level)
    {
        Dispatcher.Invoke(() =>
        {
            // LevelBar max width is 36
            double targetWidth = Math.Clamp(level * 36.0, 0, 36);
            LevelBar.Width = targetWidth;
        });
    }

    public void UpdateTimer(TimeSpan elapsed, int maxSeconds)
    {
        Dispatcher.Invoke(() =>
        {
            int elapsedSec = (int)elapsed.TotalSeconds;
            int elapsedMin = elapsedSec / 60;
            int remSec = elapsedSec % 60;

            int maxMin = maxSeconds / 60;
            int maxRemSec = maxSeconds % 60;

            TimerText.Text = $"{elapsedMin}:{remSec:D2} / {maxMin}:{maxRemSec:D2}";
        });
    }

    public void ShowLooping(string hotkeyText)
    {
        Dispatcher.Invoke(() =>
        {
            RecordingPanel.Visibility = Visibility.Collapsed;
            LoopingPanel.Visibility = Visibility.Visible;
            LoopHintText.Text = $"Tap {hotkeyText} to clear";

            _recPulseStoryboard?.Stop();
            _loopRotateStoryboard?.Begin();

            AnimateIn();
        });
    }

    public void HideOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (!_isVisible) return;
            _isVisible = false;

            _recPulseStoryboard?.Stop();
            _loopRotateStoryboard?.Stop();

            var animY = new DoubleAnimation(-35, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var animOpacity = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            animOpacity.Completed += (s, e) =>
            {
                if (!_isVisible)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, animY);
            RootContainer.BeginAnimation(OpacityProperty, animOpacity);
        });
    }

    private void AnimateIn()
    {
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
        }

        _isVisible = true;
        Recenter();

        var animY = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var animOpacity = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, animY);
        RootContainer.BeginAnimation(OpacityProperty, animOpacity);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
