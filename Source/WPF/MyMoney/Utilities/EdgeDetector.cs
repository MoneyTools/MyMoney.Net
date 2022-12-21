using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;


namespace Walkabout.Utilities
{
    public class EdgeDetectedEventArgs : EventArgs
    {
        public EdgeDetectedEventArgs(List<Point> edge)
        {
            this.Edge = edge;
        }

        public List<Point> Edge { get; set; }
    }

    /// <summary>
    /// This class implements the Canny Edge Detection algorithm.
    /// See http://en.wikipedia.org/wiki/Canny_edge_detector
    /// </summary>
    public class CannyEdgeDetector
    {
        private const int DefaultMaskSize = 5;
        private const int DefaultSigma = 1;
        private const float DefaultMaxHysteresisThresh = 20F;
        private const float DefaultMinHysteresisThresh = 10F;
        private const int DefaultMinimumEdgeLength = 10;
        private const int BlackThreshold = 3;
        private const int WhiteThreshold = 252;

        private readonly int width, height;
        private readonly BitmapSource bitmap;
        private int[,] greyImage;
        private Thickness margin;

        //Gaussian Kernel Data
        private int[,] gaussianKernel;
        private int kernelWeight;
        private readonly int kernelSize = 5;
        private readonly float sigma = 1;   // for N=2 Sigma =0.85  N=5 Sigma =1, N=9 Sigma = 2    2*Sigma = (int)N/2

        //Canny Edge Detection Parameters
        private readonly float maxHysteresisThresh, minHysteresisThresh;
        private readonly int minEdgeLength = DefaultMinimumEdgeLength;
        private Rect edgeBounds;

        private float[,] derivativeX;
        private float[,] derivativeY;
        private int[,] filteredImage;
        private float[,] gradient;
        private float[,] nonMax;
        private int[,] postHysteresis;
        private int[,] edgePoints;
        private int[,] edgeMap;
        private int[,] visitedMap;

        public CannyEdgeDetector(BitmapSource input,
            float Th = DefaultMaxHysteresisThresh,
            float Tl = DefaultMinHysteresisThresh,
            int minEdgeLength = DefaultMinimumEdgeLength,
            int GaussianMaskSize = DefaultMaskSize,
            float SigmaforGaussianKernel = DefaultSigma
            )
        {
            // Gaussian and Canny Parameters
            this.maxHysteresisThresh = Th;
            this.minHysteresisThresh = Tl;
            this.minEdgeLength = minEdgeLength;
            this.kernelSize = GaussianMaskSize;
            this.sigma = SigmaforGaussianKernel;

            this.bitmap = input;
            this.width = this.bitmap.PixelWidth;
            this.height = this.bitmap.PixelHeight;

            this.ReadImage();

            this.margin = this.GetImageMargin(this.greyImage);
        }

        public Rect EdgeBounds
        {
            get { return this.edgeBounds; }
        }

        public BitmapSource ToImage<T>(T[,] image)
        {
            var format = System.Windows.Media.PixelFormats.Pbgra32;
            int bitsPerPixel = format.BitsPerPixel;
            int stride = ((this.width * bitsPerPixel) + 7) / 8;
            int bytesPerPixel = format.BitsPerPixel / 8;

            // 4 bytes per pixel.
            byte[] pixels = new byte[stride * this.height];
            int i = 0;

            for (int y = 0; y < this.height; y++)
            {
                for (int x = 0; x < this.width; x++)
                {
                    // write the logic implementation here
                    object t = image[x, y];
                    byte b = 0;
                    if (t is int)
                    {
                        b = (byte)(int)t;
                    }
                    else if (t is float)
                    {
                        b = (byte)(float)t;
                    }
                    else
                    {
                        throw new Exception("Parameter type must be int or float");
                    }
                    pixels[i + 0] = b;
                    pixels[i + 1] = b;
                    pixels[i + 2] = b;
                    pixels[i + 3] = 255;
                    //4 bytes per pixel
                    i += 4;
                }//end for j

                // account for any padding at the end of the stride so we're ready for new row...
                i += stride - (this.width * 4);
            }//end for i

            return BitmapSource.Create(this.width, this.height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, pixels, stride);
        }      // Display Grey Image


        private const double clampThreshold = 30;

        /// <summary>
        /// Convert the image to a gray-scale integer array for quick easy access.
        /// </summary>
        private void ReadImage()
        {
            this.greyImage = new int[this.width, this.height];  //[Row,Column]

            int bitsPerPixel = this.bitmap.Format.BitsPerPixel;
            int stride = ((this.bitmap.PixelWidth * bitsPerPixel) + 7) / 8;
            int bytesPerPixel = this.bitmap.Format.BitsPerPixel / 8;
            if (bytesPerPixel != 4 && bytesPerPixel != 3)
            {
                throw new Exception("This algorithm requires 3 or 4 bytes per pixel");
            }
            int rowSize = bytesPerPixel * this.width;
            byte[] pixels = new byte[rowSize];

            for (int y = 0; y < this.height; y++)
            {
                this.bitmap.CopyPixels(new Int32Rect(0, y, this.width, 1), pixels, stride, 0);

                for (int x = 0; x < rowSize; x += bytesPerPixel)
                {
                    double r = pixels[x + 2];
                    double g = pixels[x + 1];
                    double b = pixels[x + 0];
                    double sum = r + g + b;
                    if (sum < clampThreshold)
                    {
                        sum = 0;
                    }
                    if (sum > (255 * 3) - clampThreshold)
                    {
                        sum = 255 * 3;
                    }
                    this.greyImage[x / bytesPerPixel, y] = (int)(sum / 3.0);
                }
            }

            return;
        }

        private Thickness GetImageMargin(int[,] image)
        {
            Thickness margin = new Thickness(0);

            // Top margin:
            bool allWhite = true;
            bool allBlack = true;

            for (int y = 0; y < this.height; y++)
            {
                for (int x = 0; x < this.width; x++)
                {
                    int v = image[x, y];
                    if (v > BlackThreshold)
                    {
                        allBlack = false;
                    }
                    if (v < WhiteThreshold)
                    {
                        allWhite = false;
                    }
                }
                if (!allWhite && !allBlack)
                {
                    if (y > 0)
                    {
                        margin.Top = y + 1; // indent further to avoid any fuzzy outlines 
                    }
                    break;
                }
            }

            // bottom margin.
            allBlack = allWhite = true;
            for (int y = this.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < this.width; x++)
                {
                    int v = image[x, y];
                    if (v > BlackThreshold)
                    {
                        allBlack = false;
                    }
                    if (v < WhiteThreshold)
                    {
                        allWhite = false;
                    }
                }
                if (!allWhite && !allBlack)
                {
                    if (y < this.height - 1)
                    {
                        margin.Bottom = y - 1; // inset a bit further to avoid fuzzy outlines.
                    }
                    else
                    {
                        margin.Bottom = this.height;
                    }
                    break;
                }
            }

            // Left margin:
            allBlack = allWhite = true;
            for (int x = 0; x < this.width; x++)
            {
                for (int y = 0; y < this.height; y++)
                {
                    int v = image[x, y];
                    if (v > BlackThreshold)
                    {
                        allBlack = false;
                    }
                    if (v < WhiteThreshold)
                    {
                        allWhite = false;
                    }
                }
                if (!allWhite && !allBlack)
                {
                    if (x > 0)
                    {
                        margin.Left = x + 1;
                    }
                    break;
                }
            }

            // Right margin:
            allBlack = allWhite = true;
            for (int x = this.width - 1; x >= 0; x--)
            {
                for (int y = 0; y < this.height; y++)
                {
                    int v = image[x, y];
                    if (v > BlackThreshold)
                    {
                        allBlack = false;
                    }
                    if (v < WhiteThreshold)
                    {
                        allWhite = false;
                    }
                }
                if (!allWhite && !allBlack)
                {
                    if (x < this.width - 1)
                    {
                        margin.Right = x - 1;
                    }
                    else
                    {
                        margin.Right = this.width;
                    }
                    break;
                }
            }

            return margin;
        }


        private void GenerateGaussianKernel(int N, float S, out int Weight)
        {
            float Sigma = S;
            float pi;
            pi = (float)Math.PI;
            int i, j;
            int SizeofKernel = N;

            float[,] Kernel = new float[N, N];
            this.gaussianKernel = new int[N, N];
            float[,] OP = new float[N, N];
            float D1, D2;


            D1 = 1 / (2 * pi * Sigma * Sigma);
            D2 = 2 * Sigma * Sigma;

            float min = 1000;

            int halfKernel = SizeofKernel / 2;
            int kernelMax = halfKernel;
            if (kernelMax * 2 != SizeofKernel)
            {
                // odd sized kernel, we need one more column.
                kernelMax++;
            }

            for (i = -halfKernel; i < kernelMax; i++)
            {
                for (j = -halfKernel; j < kernelMax; j++)
                {
                    Kernel[halfKernel + i, halfKernel + j] = 1 / D1 * (float)Math.Exp(-((i * i) + (j * j)) / D2);
                    if (Kernel[halfKernel + i, halfKernel + j] < min)
                    {
                        min = Kernel[halfKernel + i, halfKernel + j];
                    }
                }
            }
            int mult = (int)(1 / min);
            int sum = 0;
            if ((min > 0) && (min < 1))
            {

                for (i = -halfKernel; i < kernelMax; i++)
                {
                    for (j = -halfKernel; j < kernelMax; j++)
                    {
                        Kernel[halfKernel + i, halfKernel + j] = (float)Math.Round(Kernel[halfKernel + i, halfKernel + j] * mult, 0);
                        this.gaussianKernel[halfKernel + i, halfKernel + j] = (int)Kernel[halfKernel + i, halfKernel + j];
                        sum = sum + this.gaussianKernel[halfKernel + i, halfKernel + j];
                    }

                }

            }
            else
            {
                sum = 0;
                for (i = -halfKernel; i < kernelMax; i++)
                {
                    for (j = -halfKernel; j < kernelMax; j++)
                    {
                        Kernel[halfKernel + i, halfKernel + j] = (float)Math.Round(Kernel[halfKernel + i, halfKernel + j], 0);
                        this.gaussianKernel[halfKernel + i, halfKernel + j] = (int)Kernel[halfKernel + i, halfKernel + j];
                        sum = sum + this.gaussianKernel[halfKernel + i, halfKernel + j];
                    }

                }

            }
            //Normalizing kernel Weight
            Weight = sum;

            return;
        }

        private int[,] GaussianFilter(int[,] data)
        {
            this.GenerateGaussianKernel(this.kernelSize, this.sigma, out this.kernelWeight);

            int[,] output = new int[this.width, this.height];
            int i, j, k, l;
            int limit = this.kernelSize / 2;
            int limitUpper = limit;
            if (limitUpper * 2 < this.kernelSize)
            {
                limitUpper++;
            }

            float sum = 0;

            int maxX = (int)this.margin.Right - limit;
            int maxY = (int)this.margin.Bottom - limit;

            for (i = (int)this.margin.Left + limit; i < maxX; i++)
            {
                for (j = (int)this.margin.Top + limit; j < maxY; j++)
                {
                    sum = 0;
                    for (k = -limit; k < limitUpper; k++)
                    {
                        for (l = -limit; l < limitUpper; l++)
                        {
                            sum = sum + ((float)data[i + k, j + l] * this.gaussianKernel[limit + k, limit + l]);
                        }
                    }
                    output[i, j] = (int)Math.Round(sum / this.kernelWeight);
                }

            }

            return output;
        }

        private void DifferentiateX()
        {
            //Sobel Masks
            int[,] Dx = {{1,0,-1},
                         {1,0,-1},
                         {1,0,-1}};

            this.derivativeX = this.Differentiate(this.filteredImage, Dx);
        }

        private void DifferentiateY()
        {

            int[,] Dy = {{1,1,1},
                         {0,0,0},
                         {-1,-1,-1}};

            this.derivativeY = this.Differentiate(this.filteredImage, Dy);
        }

        private float[,] Differentiate(int[,] data, int[,] filter)
        {
            int filterWidth = filter.GetLength(0);
            int filterHeight = filter.GetLength(1);
            float sum = 0;
            float[,] output = new float[this.width, this.height];

            int halfFilterWidth = filterWidth / 2;
            int halfFilterX = halfFilterWidth;
            if (halfFilterWidth * 2 != filterWidth)
            {
                halfFilterX++; // it was an odd size
            }
            int halfFilterHeight = filterHeight / 2;
            int halfFilterY = halfFilterHeight;
            if (halfFilterHeight * 2 != filterHeight)
            {
                halfFilterY++; // it was an odd size
            }

            // the gaussian filter creates a blank margin around the edge.
            // We don't want to mistakenly think those are edges, so we do not include
            // that margin in the differential.
            int startX = (int)this.margin.Left + Math.Max((this.kernelSize / 2) + 1, halfFilterWidth);
            int startY = (int)this.margin.Top + Math.Max((this.kernelSize / 2) + 1, halfFilterHeight);

            int maxX = (int)this.margin.Right - startX;
            int maxY = (int)this.margin.Bottom - startY;

            for (int i = startX; i < maxX; i++)
            {
                for (int j = startY; j < maxY; j++)
                {
                    sum = 0;
                    for (int k = -halfFilterWidth; k < halfFilterX; k++)
                    {
                        for (int l = -halfFilterHeight; l < halfFilterY; l++)
                        {
                            sum = sum + (data[i + k, j + l] * filter[halfFilterWidth + k, halfFilterHeight + l]);
                        }
                    }
                    output[i, j] = sum;
                }

            }
            return output;
        }

        private float[,] EdgeGradient(float[,] derivativeX, float[,] derivativeY)
        {
            var gradient = new float[this.width, this.height];

            //Compute the gradient magnitude based on derivatives in x and y:
            int maxX = (int)this.margin.Right;
            int maxY = (int)this.margin.Bottom;

            for (int i = (int)this.margin.Left; i < maxX; i++)
            {
                for (int j = (int)this.margin.Top; j < maxY; j++)
                {
                    float dx = derivativeX[i, j];
                    float dy = derivativeY[i, j];
                    gradient[i, j] = (float)Math.Sqrt((dx * dx) + (dy * dy));
                }

            }

            return gradient;
        }

        public void DetectEdges()
        {
            //Gaussian Filter Input Image to remove noise.
            this.filteredImage = this.GaussianFilter(this.greyImage);

            // the X and Y differentiation can be done in parallel since they don't depend on each other.
            var jobs = new List<Action>();
            jobs.Add(this.DifferentiateX);
            jobs.Add(this.DifferentiateY);

            System.Threading.Tasks.Parallel.ForEach<Action>(jobs, new Action<Action>((job) =>
            {
                job();
            }));

            this.gradient = this.EdgeGradient(this.derivativeX, this.derivativeY);

            // Perform Non maximum suppression
            this.nonMax = this.NonMaximumSuppression(this.gradient);

            // reclaim the memory...
            this.derivativeX = null;
            this.derivativeY = null;
            this.gradient = null;

            // Now do hysteresis thresholding
            this.edgeMap = this.HysterisisThresholding(this.nonMax);

            this.nonMax = null;
            this.visitedMap = null;
            this.edgePoints = null;

            this.Scale(this.edgeMap, 255);

            Thickness final = this.GetImageMargin(this.edgeMap);

            this.edgeBounds = new Rect(final.Left, final.Top, final.Right - final.Left, final.Bottom - final.Top);
        }

        private void Scale(int[,] data, int scale)
        {
            for (int i = 0; i < this.width; i++)
            {
                for (int j = 0; j < this.height; j++)
                {
                    data[i, j] = data[i, j] * scale;
                }
            }
        }

        private const float VerticalAngle = (float)(90 * Math.PI / 180); // radians
        private const float HorizontalMinimum = (float)(22.5 * Math.PI / 180); // radians
        private const float HorizontalMaximum = (float)(157.5 * Math.PI / 180); // radians
        private const float VerticalMinimum = (float)(67.5 * Math.PI / 180); // radians
        private const float VerticalMaximum = (float)(112.5 * Math.PI / 180); // radians

        private float[,] NonMaximumSuppression(float[,] gradient)
        {
            this.nonMax = this.Copy(gradient);

            int limit = this.kernelSize / 2;
            int limitX = this.width - limit;
            int limitY = this.height - limit;

            int i, j;

            for (i = limit; i < limitX; i++)
            {
                for (j = limit; j < limitY; j++)
                {
                    float tangent;

                    float dx = this.derivativeX[i, j];

                    if (dx == 0)
                    {
                        tangent = VerticalAngle;
                    }
                    else
                    {
                        float dy = this.derivativeY[i, j];
                        tangent = (float)Math.Atan(dy / dx);
                    }

                    float g = gradient[i, j];

                    //Horizontal Edge
                    if (((-HorizontalMinimum < tangent) && (tangent <= HorizontalMinimum)) ||
                        ((HorizontalMaximum < tangent) && (tangent <= -HorizontalMaximum)))
                    {
                        if ((g < gradient[i, j + 1]) || (g < gradient[i, j - 1]))
                        {
                            this.nonMax[i, j] = 0;
                        }
                    }


                    //Vertical Edge
                    if (((-VerticalMaximum < tangent) && (tangent <= -VerticalMinimum)) ||
                        ((VerticalMinimum < tangent) && (tangent <= VerticalMaximum)))
                    {
                        if ((g < gradient[i + 1, j]) || (g < gradient[i - 1, j]))
                        {
                            this.nonMax[i, j] = 0;
                        }
                    }

                    //+45 Degree Edge
                    if (((-VerticalMinimum < tangent) && (tangent <= -HorizontalMinimum)) ||
                        ((VerticalMaximum < tangent) && (tangent <= HorizontalMaximum)))
                    {
                        if ((g < gradient[i + 1, j - 1]) || (g < gradient[i - 1, j + 1]))
                        {
                            this.nonMax[i, j] = 0;
                        }
                    }

                    //-45 Degree Edge
                    if (((-HorizontalMaximum < tangent) && (tangent <= -VerticalMaximum)) ||
                        ((VerticalMinimum < tangent) && (tangent <= HorizontalMinimum)))
                    {
                        if ((g < gradient[i + 1, j + 1]) || (g < gradient[i - 1, j - 1]))
                        {
                            this.nonMax[i, j] = 0;
                        }
                    }

                }
            }
            return this.nonMax;
        }

        private float[,] Copy(float[,] data)
        {
            int w = data.GetLength(0);
            int h = data.GetLength(1);
            float[,] result = new float[w, h];
            int i, j;
            for (i = 0; i < w; i++)
            {
                for (j = 0; j < h; j++)
                {
                    result[i, j] = data[i, j];
                }
            }
            return result;
        }

        private enum Direction
        {
            None,
            TopLeft, TopCenter, TopRight,
            MiddleLeft, MiddleRight,
            BottomLeft, BottomCenter, BottomRight
        }

        private class IntPoint
        {
            public int X;
            public int Y;

            public IntPoint(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }


        private int[,] HysterisisThresholding(float[,] nonMax)
        {
            int limit = this.kernelSize / 2;
            int limitX = this.width - limit;
            int limitY = this.height - limit;

            //PostHysteresis = NonMax;   
            this.postHysteresis = new int[this.width, this.height];

            for (int r = limit; r < limitX; r++)
            {
                for (int c = limit; c < limitY; c++)
                {
                    this.postHysteresis[r, c] = (int)nonMax[r, c];
                }
            }

            this.edgePoints = new int[this.width, this.height];

            for (int r = limit; r < limitX; r++)
            {
                for (int c = limit; c < limitY; c++)
                {
                    int ph = this.postHysteresis[r, c];
                    if (ph >= this.maxHysteresisThresh)
                    {
                        this.edgePoints[r, c] = 1;
                    }
                    if ((ph < this.maxHysteresisThresh) && (ph >= this.minHysteresisThresh))
                    {
                        this.edgePoints[r, c] = 2;
                    }
                }
            }

            this.edgeMap = new int[this.width, this.height];

            this.visitedMap = new int[this.width, this.height];

            int maxLength = 0;

            for (int i = limit; i < limitX; i++)
            {
                for (int j = limit; j < limitY; j++)
                {
                    if (this.edgePoints[i, j] == 1 && this.visitedMap[i, j] == 0)
                    {
                        maxLength = Math.Max(maxLength, this.TraverseEdges(i, j));
                    }
                }
            }

            return this.edgeMap;
        }

        public event EventHandler<EdgeDetectedEventArgs> EdgeDetected;


        private int TraverseEdges(int x, int y)
        {
            // Traverse in all directions connecting weak edges (EdgePoints[x,y] == 2) to strong edges (EdgePoints[x,y] == 1)
            // Also, eliminate edges that are shorter than the minEdgeLength.
            int length = 0;

            // We use a stack instead of recurrsion so we don't get stack overflow in the case where we have a huge image and
            // very heavily connected edges.  The worst case is every pixel is connected to the next, so this stack could
            // get as big as the entire number of pixels in the image.
            List<IntPoint> path = new List<IntPoint>();
            Stack<IntPoint> stack = new Stack<IntPoint>();
            stack.Push(new IntPoint(x, y));
            this.visitedMap[x, y] = 1;

            while (stack.Count > 0)
            {
                IntPoint pt = stack.Pop();
                x = pt.X;
                y = pt.Y;

                for (int i = x - 1; i <= x + 1; i++)
                {
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (i != x || j != y)
                        {
                            if (this.edgePoints[i, j] != 0 && this.visitedMap[i, j] == 0)
                            {
                                IntPoint next = new IntPoint(i, j);
                                this.visitedMap[i, j] = 1;
                                stack.Push(next);
                                path.Add(next);
                                length++;
                            }
                        }
                    }
                }
            }

            if (path.Count > this.minEdgeLength)
            {
                List<Point> points = new List<Point>();
                foreach (IntPoint pt in path)
                {
                    this.edgeMap[pt.X, pt.Y] = 1;
                    if (EdgeDetected != null)
                    {
                        points.Add(new Point(pt.X, pt.Y));
                    }
                }
                if (EdgeDetected != null)
                {
                    EdgeDetected(this, new EdgeDetectedEventArgs(points));
                }
            }

            return length;
        }

        //Canny Class Ends
    }
}
