using System;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Microsoft.Gaming.XboxGameBar;
using CrosshairOverlay.Models;
using CrosshairOverlay.Widget.Services;

namespace CrosshairOverlay.Widget
{
    public sealed partial class CrosshairPage : Page
    {
        /// <summary>文件同步轮询间隔（秒）</summary>
        private const int FileSyncIntervalSeconds = 2;
        /// <summary>渲染重试间隔（毫秒），等待 Widget 窗口尺寸就绪</summary>
        private const int RenderRetryIntervalMs = 500;
        /// <summary>最大渲染重试次数，超过后停止尝试</summary>
        private const int MaxRenderRetries = 10;
        private CrosshairProfile _profile = new CrosshairProfile();
        private DispatcherTimer _syncTimer;
        private DispatcherTimer _renderRetryTimer;
        private string _lastJson = string.Empty;
        private bool _rendered;
        private int _retryCount;
        private bool _profileSubscribed;
        private XboxGameBarWidget _widget;
        private double _rawPixelsPerViewPixel = 1.0;
        private double _screenCenterRawX;
        private double _screenCenterRawY;

        public CrosshairPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            if (_profileSubscribed)
            {
                AppServiceClient.Instance.ProfileUpdated -= OnProfileUpdated;
                _profileSubscribed = false;
            }
            if (_widget != null)
            {
                _widget.WindowBoundsChanged -= OnWindowBoundsChanged;
            }
            StopSyncTimer();
            if (_renderRetryTimer != null)
            {
                _renderRetryTimer.Stop();
                _renderRetryTimer = null;
            }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (!_profileSubscribed)
            {
                AppServiceClient.Instance.ProfileUpdated += OnProfileUpdated;
                _profileSubscribed = true;
            }

            if (AppServiceClient.Instance.HasCurrentProfile)
                _profile = CrosshairProfileRules.Sanitize(AppServiceClient.Instance.CurrentProfile);

            try
            {
                var di = DisplayInformation.GetForCurrentView();
                _rawPixelsPerViewPixel = di.RawPixelsPerViewPixel;
                _screenCenterRawX = di.ScreenWidthInRawPixels / 2.0;
                _screenCenterRawY = di.ScreenHeightInRawPixels / 2.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Widget] DisplayInformation init failed: {ex.Message}");
            }
            StartSyncTimer();
            if (!AppServiceClient.Instance.IsConnected || !AppServiceClient.Instance.HasCurrentProfile)
                LoadSettingsFromFile();
            TryRenderOrRetry();
        }

        private void TryRenderOrRetry()
        {
            RenderCrosshair();
            if (!_rendered && _retryCount < MaxRenderRetries)
            {
                _retryCount++;
                if (_renderRetryTimer == null)
                {
                    _renderRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RenderRetryIntervalMs) };
                    _renderRetryTimer.Tick += OnRenderRetryTick;
                }
                _renderRetryTimer.Start();
            }
        }

        private void OnRenderRetryTick(object sender, object e)
        {
            _retryCount++;
            RenderCrosshair();
            if (_rendered || _retryCount >= MaxRenderRetries)
            {
                _renderRetryTimer.Stop();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderCrosshair();
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Parameter is XboxGameBarWidget widget)
            {
                _widget = widget;
                _widget.WindowBoundsChanged += OnWindowBoundsChanged;
                _ = _widget.CenterWindowAsync();  // fire-and-forget: centering is best-effort
            }
            if (_renderRetryTimer != null)
            {
                _renderRetryTimer.Stop();
                _renderRetryTimer = null;
            }
            _rendered = false;
            _retryCount = 0;
            RenderCrosshair();
        }

        private void OnWindowBoundsChanged(XboxGameBarWidget sender, object args)
        {
            RenderCrosshair();
        }

        private void OnProfileUpdated(CrosshairProfile profile)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                _profile = CrosshairProfileRules.Sanitize(profile);
                RenderCrosshair();
            });
        }

        private void StartSyncTimer()
        {
            StopSyncTimer();
            _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FileSyncIntervalSeconds) };
            _syncTimer.Tick += OnSyncTimerTick;
            _syncTimer.Start();
        }

        private void StopSyncTimer()
        {
            if (_syncTimer != null)
            {
                _syncTimer.Stop();
                _syncTimer = null;
            }
        }

        private void OnSyncTimerTick(object sender, object e)
        {
            if (AppServiceClient.Instance.IsConnected && AppServiceClient.Instance.HasCurrentProfile) return;
            LoadSettingsFromFile();
        }

        private async void LoadSettingsFromFile()
        {
            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var file = await folder.GetFileAsync("widget_settings.json");
                var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                if (json == _lastJson) return;
                _lastJson = json;

                var profile = Newtonsoft.Json.JsonConvert.DeserializeObject<CrosshairProfile>(json);
                if (profile != null)
                {
                    _profile = CrosshairProfileRules.Sanitize(profile);
                    RenderCrosshair();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Widget] widget_settings.json deserialized to null");
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // Widget first launch — file may not exist yet
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Widget] LoadSettings failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 profile 字段限制在合理范围内，防止恶意或损坏的配置导致渲染异常。
        /// </summary>
        private void RenderCrosshair()
        {
            CrosshairCanvas.Children.Clear();

            if (!_profile.IsVisible)
            {
                _rendered = true;
                return;
            }

            _rendered = false;

            double cx = ActualWidth / 2.0;
            double cy = ActualHeight / 2.0;

            if (_widget != null && _screenCenterRawX > 0 && _screenCenterRawY > 0)
            {
                var bounds = _widget.WindowBounds;
                double screenCenterViewX = _screenCenterRawX / _rawPixelsPerViewPixel;
                double screenCenterViewY = _screenCenterRawY / _rawPixelsPerViewPixel;
                double offsetX = screenCenterViewX - bounds.X;
                double offsetY = screenCenterViewY - bounds.Y;

                if (offsetX > 0 && offsetX < ActualWidth && offsetY > 0 && offsetY < ActualHeight)
                {
                    cx = offsetX;
                    cy = offsetY;
                }
            }

            if (cx <= 0 || cy <= 0) return;

            _rendered = true;

            var baseColor = ParseColor(_profile.Color, _profile.Opacity);
            var outlineColor = ParseColor(_profile.OutlineColor, _profile.Opacity);

            switch (_profile.Style)
            {
                case CrosshairStyle.Cross: DrawCross(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Dot: DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.CrossDot: DrawCross(cx, cy, baseColor, outlineColor); DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Circle: DrawCircle(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.CircleDot: DrawCircle(cx, cy, baseColor, outlineColor); DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Outline: DrawOutline(cx, cy, baseColor, outlineColor); break;
                default: return;
            }

            _rendered = true;
        }

        private void DrawCross(double cx, double cy, Color baseColor, Color outlineColor)
        {
            var halfSize = _profile.Size / 2.0;
            var halfGap = _profile.Gap / 2.0;

            if (_profile.OutlineEnabled)
            {
                AddLine(cx, cy - halfSize, cx, cy - halfGap, outlineColor, _profile.Thickness + _profile.OutlineThickness * 2);
                AddLine(cx, cy + halfGap, cx, cy + halfSize, outlineColor, _profile.Thickness + _profile.OutlineThickness * 2);
                AddLine(cx - halfSize, cy, cx - halfGap, cy, outlineColor, _profile.Thickness + _profile.OutlineThickness * 2);
                AddLine(cx + halfGap, cy, cx + halfSize, cy, outlineColor, _profile.Thickness + _profile.OutlineThickness * 2);
            }

            AddLine(cx, cy - halfSize, cx, cy - halfGap, baseColor, _profile.Thickness);
            AddLine(cx, cy + halfGap, cx, cy + halfSize, baseColor, _profile.Thickness);
            AddLine(cx - halfSize, cy, cx - halfGap, cy, baseColor, _profile.Thickness);
            AddLine(cx + halfGap, cy, cx + halfSize, cy, baseColor, _profile.Thickness);
        }

        private void DrawDot(double cx, double cy, Color baseColor, Color outlineColor)
        {
            var radius = _profile.DotSize / 2.0;
            if (_profile.OutlineEnabled)
                AddCircle(cx, cy, radius + _profile.OutlineThickness, outlineColor);
            AddCircle(cx, cy, radius, baseColor);
        }

        private void DrawCircle(double cx, double cy, Color baseColor, Color outlineColor)
        {
            var radius = _profile.Size / 2.0;
            if (_profile.OutlineEnabled)
            {
                var outerR = radius + (_profile.Thickness + _profile.OutlineThickness * 2) / 2.0;
                var outer = new Ellipse
                {
                    Width = outerR * 2, Height = outerR * 2,
                    Stroke = new SolidColorBrush(outlineColor), StrokeThickness = _profile.Thickness + _profile.OutlineThickness * 2
                };
                Canvas.SetLeft(outer, cx - outerR); Canvas.SetTop(outer, cy - outerR);
                CrosshairCanvas.Children.Add(outer);
            }
            var innerR = radius + _profile.Thickness / 2.0;
            var inner = new Ellipse
            {
                Width = innerR * 2, Height = innerR * 2,
                Stroke = new SolidColorBrush(baseColor), StrokeThickness = _profile.Thickness
            };
            Canvas.SetLeft(inner, cx - innerR); Canvas.SetTop(inner, cy - innerR);
            CrosshairCanvas.Children.Add(inner);
        }

        private void DrawOutline(double cx, double cy, Color baseColor, Color outlineColor)
        {
            var halfSize = _profile.Size / 2.0;
            var halfGap = _profile.Gap / 2.0;
            var halfThick = _profile.Thickness / 2.0;

            if (_profile.OutlineEnabled)
            {
                var ot = _profile.Thickness + _profile.OutlineThickness * 2;
                AddRect(cx - halfThick, cy - halfSize, ot, halfSize - halfGap, outlineColor);
                AddRect(cx - halfThick, cy + halfGap, ot, halfSize - halfGap, outlineColor);
                AddRect(cx - halfSize, cy - halfThick, halfSize - halfGap, ot, outlineColor);
                AddRect(cx + halfGap, cy - halfThick, halfSize - halfGap, ot, outlineColor);
            }

            AddRect(cx - halfThick, cy - halfSize, _profile.Thickness, halfSize - halfGap, baseColor);
            AddRect(cx - halfThick, cy + halfGap, _profile.Thickness, halfSize - halfGap, baseColor);
            AddRect(cx - halfSize, cy - halfThick, halfSize - halfGap, _profile.Thickness, baseColor);
            AddRect(cx + halfGap, cy - halfThick, halfSize - halfGap, _profile.Thickness, baseColor);
        }

        private void AddLine(double x1, double y1, double x2, double y2, Color color, double thickness)
        {
            CrosshairCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(color), StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });
        }

        private void AddCircle(double cx, double cy, double radius, Color color)
        {
            var dot = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = new SolidColorBrush(color) };
            Canvas.SetLeft(dot, cx - radius); Canvas.SetTop(dot, cy - radius);
            CrosshairCanvas.Children.Add(dot);
        }

        private void AddRect(double x, double y, double width, double height, Color color)
        {
            var rect = new Rectangle { Width = width, Height = height, Fill = new SolidColorBrush(color) };
            Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
            CrosshairCanvas.Children.Add(rect);
        }

        private static Color ParseColor(string hex, double opacity)
        {
            var safeOpacity = double.IsNaN(opacity) || double.IsInfinity(opacity)
                ? 1.0
                : Math.Max(0, Math.Min(1, opacity));
            var alpha = (byte)(safeOpacity * 255);

            try
            {
                if (string.IsNullOrEmpty(hex) || hex.Length < 7)
                    return Color.FromArgb(alpha, Colors.Lime.R, Colors.Lime.G, Colors.Lime.B);
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                if (hex.Length < 6)
                    return Color.FromArgb(alpha, Colors.Lime.R, Colors.Lime.G, Colors.Lime.B);
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromArgb(alpha, r, g, b);
            }
            catch
            {
                return Color.FromArgb(alpha, Colors.Lime.R, Colors.Lime.G, Colors.Lime.B);
            }
        }
    }
}
