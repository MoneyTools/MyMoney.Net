using System;
using System.Windows.Media;

namespace Walkabout.Utilities
{

    public class HlsColor
    {
        private byte red = 0;
        private byte green = 0;
        private byte blue = 0;

        private float hue = 0;
        private float luminance = 0;
        private float saturation = 0;

        /// <summary>
        /// Constructs an instance of the class from the specified
        /// System.Drawing.Color
        /// </summary>
        /// <param name="c">The System.Drawing.Color to use to initialize the
        /// class</param>
        public HlsColor(Color c)
        {
            this.red = c.R;
            this.green = c.G;
            this.blue = c.B;
            this.ToHLS();
        }

        /// <summary>
        /// Constructs an instance of the class with the specified hue, luminance
        /// and saturation values.
        /// </summary>
        /// <param name="hue">The Hue (between 0.0 and 360.0)</param>
        /// <param name="luminance">The Luminance (between 0.0 and 1.0)</param>
        /// <param name="saturation">The Saturation (between 0.0 and 1.0)</param>
        /// <exception cref="ArgumentOutOfRangeException">If any of the H,L,S
        /// values are out of the acceptable range (0.0-360.0 for Hue and 0.0-1.0
        /// for Luminance and Saturation)</exception>
        public HlsColor(float hue, float luminance, float saturation)
        {
            this.Hue = hue;
            this.Luminance = luminance;
            this.Saturation = saturation;
            this.ToRGB();
        }

        /// <summary>
        /// Constructs an instance of the class with the specified red, green and
        /// blue values.
        /// </summary>
        /// <param name="red">The red component.</param>
        /// <param name="green">The green component.</param>
        /// <param name="blue">The blue component.</param>
        public HlsColor(byte red, byte green, byte blue)
        {
            this.red = red;
            this.green = green;
            this.blue = blue;
            this.ToHLS();
        }

        /// <summary>
        /// Constructs an instance of the class using the settings of another
        /// instance.
        /// </summary>
        /// <param name="HlsColor">The instance to clone.</param>
        public HlsColor(HlsColor hls)
        {
            this.red = hls.red;
            this.blue = hls.blue;
            this.green = hls.green;
            this.luminance = hls.luminance;
            this.hue = hls.hue;
            this.saturation = hls.saturation;
        }

        /// <summary>
        /// Constructs a new instance of the class initialised to black.
        /// </summary>
        public HlsColor()
        {
        }

        public byte Red { get { return this.red; } }
        public byte Green { get { return this.green; } }
        public byte Blue { get { return this.blue; } }

        /// <summary>
        /// Gets or sets the Luminance (0.0 to 1.0) of the colour.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If the value is out of
        /// the acceptable range for luminance (0.0 to 1.0)</exception>
        public float Luminance
        {
            get
            {
                return this.luminance;
            }
            set
            {
                if ((value < 0.0f) || (value > 1.0f))
                {
                    throw new ArgumentOutOfRangeException("value", "Luminance must be between 0.0 and 1.0");
                }
                this.luminance = value;
                this.ToRGB();
            }
        }

        /// <summary>
        /// Gets or sets the Hue (0.0 to 360.0) of the color.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If the value is out of
        /// the acceptable range for hue (0.0 to 360.0)</exception>
        public float Hue
        {
            get
            {
                return this.hue;
            }
            set
            {
                if ((value < 0.0f) || (value > 360.0f))
                {
                    throw new ArgumentOutOfRangeException("value", "Hue must be between 0.0 and 360.0");
                }
                this.hue = value;
                this.ToRGB();
            }
        }

        /// <summary>
        /// Gets or sets the Saturation (0.0 to 1.0) of the color.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If the value is out of
        /// the acceptable range for saturation (0.0 to 1.0)</exception>
        public float Saturation
        {
            get
            {
                return this.saturation;
            }
            set
            {
                if ((value < 0.0f) || (value > 1.0f))
                {
                    throw new ArgumentOutOfRangeException("value", "Saturation must be between 0.0 and 1.0");
                }
                this.saturation = value;
                this.ToRGB();
            }
        }

        /// <summary>
        /// Gets or sets the Color as a System.Drawing.Color instance
        /// </summary>
        public Color Color
        {
            get
            {
                Color c = Color.FromRgb(this.red, this.green, this.blue);
                return c;
            }
            set
            {
                this.red = value.R;
                this.green = value.G;
                this.blue = value.B;
                this.ToHLS();
            }
        }

        /// <summary>
        /// Lightens the colour by the specified amount by modifying
        /// the luminance (for example, 0.2 would lighten the colour by 20%)
        /// </summary>
        public void Lighten(float percent)
        {
            this.luminance *= 1.0f + percent;
            if (this.luminance > 1.0f)
            {
                this.luminance = 1.0f;
            }
            this.ToRGB();
        }

        /// <summary>
        /// Darkens the colour by the specified amount by modifying
        /// the luminance (for example, 0.2 would darken the colour by 20%)
        /// </summary>
        public void Darken(float percent)
        {
            this.luminance *= 1 - percent;
            this.ToRGB();
        }

        private void ToHLS()
        {
            byte minval = Math.Min(this.red, Math.Min(this.green, this.blue));
            byte maxval = Math.Max(this.red, Math.Max(this.green, this.blue));

            float mdiff = maxval - minval;
            float msum = maxval + minval;

            this.luminance = msum / 510.0f;

            if (maxval == minval)
            {
                this.saturation = 0.0f;
                this.hue = 0.0f;
            }
            else
            {
                float rnorm = (maxval - this.red) / mdiff;
                float gnorm = (maxval - this.green) / mdiff;
                float bnorm = (maxval - this.blue) / mdiff;

                this.saturation = (this.luminance <= 0.5f) ? (mdiff / msum) : (mdiff /
                 (510.0f - msum));

                if (this.red == maxval)
                {
                    this.hue = 60.0f * (6.0f + bnorm - gnorm);
                }
                if (this.green == maxval)
                {
                    this.hue = 60.0f * (2.0f + rnorm - bnorm);
                }
                if (this.blue == maxval)
                {
                    this.hue = 60.0f * (4.0f + gnorm - rnorm);
                }
                if (this.hue > 360.0f)
                {
                    this.hue = this.hue - 360.0f;
                }
            }
        }

        private void ToRGB()
        {
            if (this.saturation == 0.0)
            {
                this.red = (byte)(this.luminance * 255.0F);
                this.green = this.red;
                this.blue = this.red;
            }
            else
            {
                float rm1;
                float rm2;

                if (this.luminance <= 0.5f)
                {
                    rm2 = this.luminance + (this.luminance * this.saturation);
                }
                else
                {
                    rm2 = this.luminance + this.saturation - (this.luminance * this.saturation);
                }
                rm1 = (2.0f * this.luminance) - rm2;
                this.red = ToRGB1(rm1, rm2, this.hue + 120.0f);
                this.green = ToRGB1(rm1, rm2, this.hue);
                this.blue = ToRGB1(rm1, rm2, this.hue - 120.0f);
            }
        }

        static private byte ToRGB1(float rm1, float rm2, float rh)
        {
            if (rh > 360.0f)
            {
                rh -= 360.0f;
            }
            else if (rh < 0.0f)
            {
                rh += 360.0f;
            }

            if (rh < 60.0f)
            {
                rm1 = rm1 + ((rm2 - rm1) * rh / 60.0f);
            }
            else if (rh < 180.0f)
            {
                rm1 = rm2;
            }
            else if (rh < 240.0f)
            {
                rm1 = rm1 + ((rm2 - rm1) * (240.0f - rh) / 60.0f);
            }

            return (byte)(rm1 * 255);
        }

    }
}