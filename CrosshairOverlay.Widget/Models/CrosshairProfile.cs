namespace CrosshairOverlay.Models
{
    public enum CrosshairStyle
    {
        Cross,
        Dot,
        CrossDot,
        Circle,
        CircleDot,
        Outline
    }

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
}
