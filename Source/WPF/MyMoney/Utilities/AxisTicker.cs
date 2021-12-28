using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Walkabout.Utilities
{
    class AxisTickSpacer
    {
        private double minPoint;
        private double maxPoint;
        private double maxTicks = 10;
        private double tickSpacing;
        private double range;
        private double niceMin;
        private double niceMax;

        public AxisTickSpacer(double min, double max)
        {
            this.minPoint = min;
            this.maxPoint = max;
            Calculate();
        }

        private void Calculate()
        {
            this.range = GetNiceNum(maxPoint - minPoint, false);
            this.tickSpacing = GetNiceNum(range / (maxTicks - 1), true);
            this.niceMin = Math.Floor(minPoint / tickSpacing) * tickSpacing;
            this.niceMax = Math.Ceiling(maxPoint / tickSpacing) * tickSpacing;
        }

        private double GetNiceNum(double range, bool round)
        {
            double exponent; /** exponent of range */
            double fraction; /** fractional part of range */
            double niceFraction; /** nice, rounded fraction */

            exponent = Math.Floor(Math.Log10(range));
            fraction = range / Math.Pow(10, exponent);

            if (round)
            {
                if (fraction < 1.5)
                    niceFraction = 1;
                else if (fraction < 3)
                    niceFraction = 2;
                else if (fraction < 7)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }
            else
            {
                if (fraction <= 1)
                    niceFraction = 1;
                else if (fraction <= 2)
                    niceFraction = 2;
                else if (fraction <= 5)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }

            return niceFraction * Math.Pow(10, exponent);
        }

        public void SetMinMaxPoints(double minPoint, double maxPoint)
        {
            this.minPoint = minPoint;
            this.maxPoint = maxPoint;
            Calculate();
        }

        public void SetMaxTicks(double maxTicks)
        {
            this.maxTicks = maxTicks;
            Calculate();
        }

        public double GetTickSpacing()
        {
            return tickSpacing;
        }

        public double GetNiceMin()
        {
            return niceMin;
        }

        public double GetNiceMax()
        {
            return niceMax;
        }
    }
}
