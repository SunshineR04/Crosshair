using CrosshairOverlay.Models;
using SkiaSharp;

namespace CrosshairOverlay.Rendering;

/// <summary>
/// 使用 SkiaSharp 矢量渲染准心位图。支持 6 种样式、描边、预乘 alpha。
/// 输出的 SKBitmap 使用 BGRA8888 + Premul，直接兼容 UpdateLayeredWindow。
/// </summary>
public class CrosshairRenderer
{
    /// <summary>
    /// 渲染画布尺寸相对于准心 Size 的倍率。
    /// 3倍确保描边和抗锯齿边缘不会被裁剪。
    /// </summary>
    private const int RenderSizeMultiplier = 3;

    /// <summary>
    /// 渲染准心位图。
    /// </summary>
    /// <param name="profile">准心配置。</param>
    /// <param name="dpiScale">DPI 缩放因子（1.0 = 100%，1.5 = 150%）。用于高 DPI 屏幕下保持准心视觉大小一致。</param>
    public SKBitmap Render(CrosshairProfile profile, float dpiScale = 1.0f)
    {
        var scaledSize = (int)(profile.Size * dpiScale);
        var scaledThickness = profile.Thickness * dpiScale;
        var scaledGap = (int)(profile.Gap * dpiScale);
        var scaledDotSize = (int)(profile.DotSize * dpiScale);
        var scaledOutlineThickness = profile.OutlineThickness * dpiScale;

        var renderSize = (int)(scaledSize * RenderSizeMultiplier);
        if (renderSize < 4) renderSize = 4;

        var bitmap = new SKBitmap(renderSize, renderSize, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var center = renderSize / 2f;
        var baseColor = (SKColor.TryParse(profile.Color, out var bc) ? bc : SKColors.Lime).WithAlpha((byte)(255 * profile.Opacity));
        var outlineColor = (SKColor.TryParse(profile.OutlineColor, out var oc) ? oc : SKColors.Black).WithAlpha((byte)(255 * profile.Opacity));

        using var fillPaint = new SKPaint
        {
            Color = baseColor,
            StrokeWidth = scaledThickness,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        };

        using var dotPaint = new SKPaint
        {
            Color = baseColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        // 使用缩放后的参数构建临时 profile 供绘制方法使用
        var scaledProfile = new CrosshairProfile
        {
            Style = profile.Style,
            Color = profile.Color,
            Thickness = (int)scaledThickness,
            Size = scaledSize,
            Gap = scaledGap,
            DotSize = scaledDotSize,
            Opacity = profile.Opacity,
            OutlineEnabled = profile.OutlineEnabled,
            OutlineColor = profile.OutlineColor,
            OutlineThickness = (int)scaledOutlineThickness,
        };

        switch (profile.Style)
        {
            case CrosshairStyle.Cross:
                DrawCross(canvas, center, fillPaint, scaledProfile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Dot:
                DrawDot(canvas, center, dotPaint, scaledProfile, baseColor, outlineColor);
                break;
            case CrosshairStyle.CrossDot:
                DrawCross(canvas, center, fillPaint, scaledProfile, baseColor, outlineColor);
                DrawDot(canvas, center, dotPaint, scaledProfile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Circle:
                DrawCircle(canvas, center, fillPaint, scaledProfile, baseColor, outlineColor);
                break;
            case CrosshairStyle.CircleDot:
                DrawCircle(canvas, center, fillPaint, scaledProfile, baseColor, outlineColor);
                DrawDot(canvas, center, dotPaint, scaledProfile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Outline:
                DrawOutline(canvas, center, fillPaint, scaledProfile, baseColor, outlineColor);
                break;
        }

        return bitmap;
    }

    private void DrawCross(SKCanvas canvas, float center, SKPaint paint, CrosshairProfile profile, SKColor baseColor, SKColor outlineColor)
    {
        var halfSize = profile.Size / 2f;
        var halfGap = profile.Gap / 2f;

        DrawLineWithOutline(canvas, center, center - halfSize, center, center - halfGap, paint, profile, baseColor, outlineColor);
        DrawLineWithOutline(canvas, center, center + halfGap, center, center + halfSize, paint, profile, baseColor, outlineColor);
        DrawLineWithOutline(canvas, center - halfSize, center, center - halfGap, center, paint, profile, baseColor, outlineColor);
        DrawLineWithOutline(canvas, center + halfGap, center, center + halfSize, center, paint, profile, baseColor, outlineColor);
    }

    private void DrawDot(SKCanvas canvas, float center, SKPaint paint, CrosshairProfile profile, SKColor baseColor, SKColor outlineColor)
    {
        var radius = profile.DotSize / 2f;

        if (profile.OutlineEnabled)
        {
            paint.Color = outlineColor;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(center, center, radius + profile.OutlineThickness, paint);
        }

        paint.Color = baseColor;
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(center, center, radius, paint);
    }

    private void DrawCircle(SKCanvas canvas, float center, SKPaint paint, CrosshairProfile profile, SKColor baseColor, SKColor outlineColor)
    {
        var radius = profile.Size / 2f;

        if (profile.OutlineEnabled)
        {
            paint.Color = outlineColor;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = profile.Thickness + profile.OutlineThickness * 2;
            canvas.DrawCircle(center, center, radius, paint);
        }

        paint.Color = baseColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = profile.Thickness;
        canvas.DrawCircle(center, center, radius, paint);
    }

    private void DrawOutline(SKCanvas canvas, float center, SKPaint paint, CrosshairProfile profile, SKColor baseColor, SKColor outlineColor)
    {
        var halfSize = profile.Size / 2f;
        var halfGap = profile.Gap / 2f;

        if (profile.OutlineEnabled)
        {
            paint.Color = outlineColor;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = profile.Thickness + profile.OutlineThickness * 2;

            canvas.DrawLine(center, center - halfSize + profile.Thickness, center, center - halfGap, paint);
            canvas.DrawLine(center, center + halfGap, center, center + halfSize - profile.Thickness, paint);
            canvas.DrawLine(center - halfSize + profile.Thickness, center, center - halfGap, center, paint);
            canvas.DrawLine(center + halfGap, center, center + halfSize - profile.Thickness, center, paint);
        }

        paint.Color = baseColor;
        paint.Style = SKPaintStyle.Fill;
        paint.StrokeWidth = profile.Thickness;
        paint.IsAntialias = true;

        SKRect top = new(center - profile.Thickness / 2f, center - halfSize, center + profile.Thickness / 2f, center - halfGap);
        SKRect bottom = new(center - profile.Thickness / 2f, center + halfGap, center + profile.Thickness / 2f, center + halfSize);
        SKRect left = new(center - halfSize, center - profile.Thickness / 2f, center - halfGap, center + profile.Thickness / 2f);
        SKRect right = new(center + halfGap, center - profile.Thickness / 2f, center + halfSize, center + profile.Thickness / 2f);

        canvas.DrawRect(top, paint);
        canvas.DrawRect(bottom, paint);
        canvas.DrawRect(left, paint);
        canvas.DrawRect(right, paint);
    }

    private void DrawLineWithOutline(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint paint, CrosshairProfile profile, SKColor baseColor, SKColor outlineColor)
    {
        if (profile.OutlineEnabled)
        {
            paint.Color = outlineColor;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = profile.Thickness + profile.OutlineThickness * 2;
            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        paint.Color = baseColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = profile.Thickness;
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }
}
