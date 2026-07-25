namespace CrosshairOverlay.Models;

/// <summary>准心样式枚举。</summary>
public enum CrosshairStyle
{
    /// <summary>十字形</summary>
    Cross,
    /// <summary>圆点</summary>
    Dot,
    /// <summary>十字 + 圆点</summary>
    CrossDot,
    /// <summary>圆环</summary>
    Circle,
    /// <summary>圆环 + 圆点</summary>
    CircleDot,
    /// <summary>实心轮廓（矩形臂）</summary>
    Outline
}

/// <summary>
/// 准心配置 POCO，同时用于 JSON 持久化和 Widget 同步。
/// WPF 端使用 System.Text.Json，Widget 端使用 Newtonsoft.Json，字段名必须保持一致。
/// </summary>
public class CrosshairProfile
{
    public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;

    public string Color { get; set; } = "#00FF00";

    public int Thickness { get; set; } = 3;

    public int Size { get; set; } = 24;

    public int Gap { get; set; } = 4;

    public int DotSize { get; set; } = 6;

    public double Opacity { get; set; } = 1.0;

    public bool OutlineEnabled { get; set; } = true;

    public string OutlineColor { get; set; } = "#000000";

    public int OutlineThickness { get; set; } = 1;

    public bool ExclusiveFullscreenMode { get; set; } = false;

    public bool IsVisible { get; set; } = true;
}
