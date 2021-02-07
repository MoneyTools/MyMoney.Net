using System;
using System.Collections.Generic;
using Xamarin.Forms;

namespace xMoney.UIControls
{
    public class Chart : ContentView
    {
        internal readonly List<ChartEntry> chartData = new();

        public Chart()
        {
            Content = new Label { Text = "< Chart goe here >" };
        }

        public void Clear()
        {
            chartData.Clear();
        }

        public void Add(decimal value, string textTop = "", string textBottom = "")
        {
            var entry = new ChartEntry(value, 0)
            {
                TextTop = textTop,
                TextBottom = textBottom,
            };

            chartData.Add(entry);
        }

        public void AddGapColumn(string textTop = "", string textBottom = "")
        {
            var entry = new ChartEntry()
            {
                TextTop = textTop,
                TextBottom = textBottom,
            };

            chartData.Add(entry);
        }

        public virtual void Render()
        {
        }

        public decimal ValueMin = 0;
        public decimal ValueMax = 0;
        public decimal ValueMaxNeutral = 0;
        public decimal ValueTotal = 0;

        public void EvalMinMaxTotal()
        {
            foreach (var entry in this.chartData)
            {
                this.ValueMin = Math.Min(this.ValueMin, entry.Value);
                this.ValueMax = Math.Max(this.ValueMax, entry.Value);
                this.ValueMaxNeutral = Math.Max(Math.Abs(this.ValueMax), Math.Abs(this.ValueMin));
                this.ValueTotal = entry.Value;
            }

            foreach (var entry in this.chartData)
            {
                entry.RelativeValueInPercentage = (float)(Math.Abs(entry.Value) / ValueMaxNeutral);
            }
        }
    }
}

