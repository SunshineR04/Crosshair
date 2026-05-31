using System;
using Windows.Foundation;
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
        private CrosshairProfile _profile = new CrosshairProfile();

        public CrosshairPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
            AppServiceClient.Instance.ProfileUpdated += OnProfileUpdated;
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            AppServiceClient.Instance.ProfileUpdated -= OnProfileUpdated;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            RenderCrosshair();
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            RenderCrosshair();
        }

        private void OnProfileUpdated(CrosshairProfile profile)
        {
            _profile = profile;
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, RenderCrosshair);
        }

        private void RenderCrosshair()
        {
            CrosshairCanvas.Children.Clear();

            try
            {
                var displayInfo = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
                double rawWidth = displayInfo.ScreenWidthInRawPixels;
                double rawHeight = displayInfo.ScreenHeightInRawPixels;
                if (rawWidth > 0 && rawHeight > 0)
                {
                    double cx = rawWidth / 2.0;
                    double cy = rawHeight / 2.0;

                    var baseColor = ParseColor(_profile.Color, _profile.Opacity);
                    var outlineColor = ParseColor(_profile.OutlineColor, _profile.Opacity);
                    DrawCrosshair(cx, cy, baseColor, outlineColor);
                    return;
                }
            }
            catch { }

            double cxFallback = ActualWidth / 2.0;
            double cyFallback = ActualHeight / 2.0;
            if (cxFallback <= 0 || cyFallback <= 0) return;

            var fbColor = ParseColor(_profile.Color, _profile.Opacity);
            var fbOutline = ParseColor(_profile.OutlineColor, _profile.Opacity);
            DrawCrosshair(cxFallback, cyFallback, fbColor, fbOutline);
        }

        private void DrawCrosshair(double cx, double cy, Color baseColor, Color outlineColor)
        {
            switch (_profile.Style)
            {
                case CrosshairStyle.Cross: DrawCross(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Dot: DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.CrossDot: DrawCross(cx, cy, baseColor, outlineColor); DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Circle: DrawCircle(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.CircleDot: DrawCircle(cx, cy, baseColor, outlineColor); DrawDot(cx, cy, baseColor, outlineColor); break;
                case CrosshairStyle.Outline: DrawOutline(cx, cy, baseColor, outlineColor); break;
            }
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
            try
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = (byte)(255 * opacity);
                return Color.FromArgb(a, r, g, b);
            }
            catch { return Colors.Lime; }
        }
    }
}
