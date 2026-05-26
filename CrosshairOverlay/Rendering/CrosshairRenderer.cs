using CrosshairOverlay.Models;
using SkiaSharp;

namespace CrosshairOverlay.Rendering;

public class CrosshairRenderer
{
    public SKBitmap Render(CrosshairProfile profile)
    {
        var renderSize = profile.Size * 3;

        var bitmap = new SKBitmap(renderSize, renderSize, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var center = renderSize / 2f;
        var baseColor = (SKColor.TryParse(profile.Color, out var bc) ? bc : SKColors.Lime).WithAlpha((byte)(255 * profile.Opacity));
        var outlineColor = (SKColor.TryParse(profile.OutlineColor, out var oc) ? oc : SKColors.Black).WithAlpha((byte)(255 * profile.Opacity));

        using var fillPaint = new SKPaint
        {
            Color = baseColor,
            StrokeWidth = profile.Thickness,
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

        switch (profile.Style)
        {
            case CrosshairStyle.Cross:
                DrawCross(canvas, center, fillPaint, profile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Dot:
                DrawDot(canvas, center, dotPaint, profile, baseColor, outlineColor);
                break;
            case CrosshairStyle.CrossDot:
                DrawCross(canvas, center, fillPaint, profile, baseColor, outlineColor);
                DrawDot(canvas, center, dotPaint, profile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Circle:
                DrawCircle(canvas, center, fillPaint, profile, baseColor, outlineColor);
                break;
            case CrosshairStyle.CircleDot:
                DrawCircle(canvas, center, fillPaint, profile, baseColor, outlineColor);
                DrawDot(canvas, center, dotPaint, profile, baseColor, outlineColor);
                break;
            case CrosshairStyle.Outline:
                DrawOutline(canvas, center, fillPaint, profile, baseColor, outlineColor);
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
