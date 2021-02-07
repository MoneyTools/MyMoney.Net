using SkiaSharp;

namespace xMoney.UIControls
{
    public class ChartEntry
    {
        public ChartEntry(decimal value, SKColor color)
        {
            this.Value = value;
            this.Color = color;
            this.IsGapColumn = false;
        }

        public ChartEntry()
        {
            this.IsGapColumn = true;
        }

        public string TextTop { get; set; }
        public string TextBottom { get; set; }

        public decimal Value { private set; get; }

        public float RelativeValueInPercentage { set; get; }

        public SKColor Color { private set; get; }

        public bool IsGapColumn { set; get; }
    }
}
