using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Diagnostics;


namespace Walkabout.Utilities
{
    public class EdgeDetectedEventArgs : EventArgs 
    {
        public EdgeDetectedEventArgs(List<Point> edge)
        {
            Edge = edge;
        }

        public List<Point> Edge{ get; set; }
    }

    /// <summary>
    /// This class implements the Canny Edge Detection algorithm.
    /// See http://en.wikipedia.org/wiki/Canny_edge_detector
    /// </summary>
    public class CannyEdgeDetector
    {
        const int DefaultMaskSize = 5;
        const int DefaultSigma = 1;
        const float DefaultMaxHysteresisThresh = 20F;
        const float DefaultMinHysteresisThresh = 10F;
        const int DefaultMinimumEdgeLength = 10;
        const int BlackThreshold = 3;
        const int WhiteThreshold = 252;

        private int width, height;
        private BitmapSource bitmap;
        private int[,] greyImage;
        private Thickness margin;

        //Gaussian Kernel Data
        private int[,] gaussianKernel;
        private int kernelWeight;
        private int kernelSize = 5;
        private float sigma = 1;   // for N=2 Sigma =0.85  N=5 Sigma =1, N=9 Sigma = 2    2*Sigma = (int)N/2

        //Canny Edge Detection Parameters
        private float maxHysteresisThresh, minHysteresisThresh;
        private int minEdgeLength = DefaultMinimumEdgeLength;
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
            maxHysteresisThresh = Th;
            minHysteresisThresh = Tl;
            this.minEdgeLength = minEdgeLength;
            kernelSize = GaussianMaskSize;
            sigma = SigmaforGaussianKernel;

            bitmap = input;
            width = bitmap.PixelWidth;
            height = bitmap.PixelHeight;

            ReadImage();

            margin = GetImageMargin(this.greyImage);
        }

        public Rect EdgeBounds
        {
            get { return edgeBounds; }
        }

        public BitmapSource ToImage<T>(T[,] image)
        {
            var format = System.Windows.Media.PixelFormats.Pbgra32;
            int bitsPerPixel = format.BitsPerPixel;
            int stride = (width * bitsPerPixel + 7) / 8;
            int bytesPerPixel = format.BitsPerPixel / 8;

            // 4 bytes per pixel.
            byte[] pixels = new byte[stride * height];
            int i = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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
                    pixels[i + 3] = (byte)255;
                    //4 bytes per pixel
                    i += 4;
                }//end for j

                // account for any padding at the end of the stride so we're ready for new row...
                i += (stride - (width * 4));
            }//end for i

            return BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, pixels, stride);
        }      // Display Grey Image


        const double clampThreshold = 30;

        /// <summary>
        /// Convert the image to a gray-scale integer array for quick easy access.
        /// </summary>
        private void ReadImage()
        {
            greyImage = new int[width, height];  //[Row,Column]

            int bitsPerPixel = bitmap.Format.BitsPerPixel;
            int stride = (bitmap.PixelWidth * bitsPerPixel + 7) / 8;
            int bytesPerPixel = bitmap.Format.BitsPerPixel / 8;
            if (bytesPerPixel != 4 && bytesPerPixel != 3)
            {
                throw new Exception("This algorithm requires 3 or 4 bytes per pixel");
            }
            int rowSize = bytesPerPixel * width;
            byte[] pixels = new byte[rowSize];

            for (int y = 0; y < height; y++)
            {
                bitmap.CopyPixels(new Int32Rect(0, y, width, 1), pixels, stride, 0);

                for (int x = 0; x < rowSize; x += bytesPerPixel)
                {
                    double r = (double)pixels[x + 2];
                    double g = (double)pixels[x + 1];
                    double b = (double)pixels[x + 0];
                    double sum = r + g + b;
                    if (sum < clampThreshold)
                    {
                        sum = 0;
                    }
                    if (sum > (255 * 3) - clampThreshold)
                    {
                        sum = (255 * 3);
                    }
                    greyImage[x / bytesPerPixel, y] = (int)(sum / 3.0);
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

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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
            for (int y = height-1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
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
                    if (y < height-1)
                    {
                        margin.Bottom = y - 1; // inset a bit further to avoid fuzzy outlines.
                    }
                    else
                    {
                        margin.Bottom = height;
                    }
                    break;
                }
            }

            // Left margin:
            allBlack = allWhite = true;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
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
            for (int x = width-1; x >= 0; x--)
            {
                for (int y = 0; y < height; y++)
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
                    if (x < width - 1)
                    {
                        margin.Right = x - 1;
                    }
                    else
                    {
                        margin.Right = width;
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
            gaussianKernel = new int[N, N];
            float[,] OP = new float[N, N];
            float D1, D2;


            D1 = 1 / (2 * pi * Sigma * Sigma);
            D2 = 2 * Sigma * Sigma;

            float min = 1000;

            int halfKernel = (SizeofKernel / 2);
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
                    Kernel[halfKernel + i, halfKernel + j] = ((1 / D1) * (float)Math.Exp(-(i * i + j * j) / D2));
                    if (Kernel[halfKernel + i, halfKernel + j] < min)
                        min = Kernel[halfKernel + i, halfKernel + j];

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
                        gaussianKernel[halfKernel + i, halfKernel + j] = (int)Kernel[halfKernel + i, halfKernel + j];
                        sum = sum + gaussianKernel[halfKernel + i, halfKernel + j];
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
                        gaussianKernel[halfKernel + i, halfKernel + j] = (int)Kernel[halfKernel + i, halfKernel + j];
                        sum = sum + gaussianKernel[halfKernel + i, halfKernel + j];
                    }

                }

            }
            //Normalizing kernel Weight
            Weight = sum;

            return;
        }

        private int[,] GaussianFilter(int[,] data)
        {
            GenerateGaussianKernel(kernelSize, sigma, out kernelWeight);

            int[,] output = new int[width, height];
            int i, j, k, l;
            int limit = kernelSize / 2;
            int limitUpper = limit;
            if (limitUpper * 2 < kernelSize)
            {
                limitUpper++;
            }

            float sum = 0;

            int maxX = (int)margin.Right - limit;
            int maxY = (int)margin.Bottom - limit;

            for (i = (int)margin.Left + limit; i < maxX; i++)
            {
                for (j = (int)margin.Top + limit; j < maxY; j++)
                {
                    sum = 0;
                    for (k = -limit; k < limitUpper; k++)
                    {
                        for (l = -limit; l < limitUpper; l++)
                        {
                            sum = sum + ((float)data[i + k, j + l] * gaussianKernel[limit + k, limit + l]);
                        }
                    }
                    output[i, j] = (int)(Math.Round(sum / (float)kernelWeight));
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

            derivativeX = Differentiate(filteredImage, Dx);
        }

        private void DifferentiateY()
        {

            int[,] Dy = {{1,1,1},
                         {0,0,0},
                         {-1,-1,-1}};

            derivativeY = Differentiate(filteredImage, Dy);
        }

        private float[,] Differentiate(int[,] data, int[,] filter)
        {
            int filterWidth = filter.GetLength(0);
            int filterHeight = filter.GetLength(1);
            float sum = 0;
            float[,] output = new float[width, height];

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
            int startX = (int)margin.Left + Math.Max(kernelSize / 2 + 1, halfFilterWidth);
            int startY = (int)margin.Top + Math.Max(kernelSize / 2 + 1, halfFilterHeight);

            int maxX = (int)margin.Right - startX;
            int maxY = (int)margin.Bottom - startY;

            for (int i = startX; i < maxX; i++)
            {
                for (int j = startY; j < maxY; j++)
                {
                    sum = 0;
                    for (int k = -halfFilterWidth; k < halfFilterX; k++)
                    {
                        for (int l = -halfFilterHeight; l < halfFilterY; l++)
                        {
                            sum = sum + data[i + k, j + l] * filter[halfFilterWidth + k, halfFilterHeight + l];
                        }
                    }
                    output[i, j] = sum;
                }

            }
            return output;
        }

        private float[,] EdgeGradient(float[,] derivativeX, float[,] derivativeY)
        {
            var gradient = new float[width, height];

            //Compute the gradient magnitude based on derivatives in x and y:
            int maxX = (int)margin.Right;
            int maxY = (int)margin.Bottom;

            for (int i = (int)margin.Left; i < maxX; i++)
            {
                for (int j = (int)margin.Top; j < maxY; j++)
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
            filteredImage = GaussianFilter(greyImage);

            // the X and Y differentiation can be done in parallel since they don't depend on each other.
            var jobs = new List<Action>();
            jobs.Add(DifferentiateX);
            jobs.Add(DifferentiateY);

            System.Threading.Tasks.Parallel.ForEach<Action>(jobs, new Action<Action>((job) =>
            {
                job();
            }));

            gradient = EdgeGradient(derivativeX, derivativeY);

            // Perform Non maximum suppression
            nonMax = NonMaximumSuppression(gradient);

            // reclaim the memory...
            derivativeX = null;
            derivativeY = null;
            gradient = null;

            // Now do hysteresis thresholding
            edgeMap = HysterisisThresholding(nonMax);

            nonMax = null;
            visitedMap = null;
            edgePoints = null;

            Scale(edgeMap, 255);

            Thickness final = GetImageMargin(edgeMap);

            edgeBounds = new Rect(final.Left, final.Top, final.Right - final.Left, final.Bottom - final.Top);
        }

        private void Scale(int[,] data, int scale)
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    data[i, j] = data[i, j] * scale;
                }
            }
        }

        const float VerticalAngle = (float)(90 * Math.PI / 180); // radians
        const float HorizontalMinimum = (float)(22.5 * Math.PI / 180); // radians
        const float HorizontalMaximum = (float)(157.5 * Math.PI / 180); // radians
        const float VerticalMinimum = (float)(67.5 * Math.PI / 180); // radians
        const float VerticalMaximum = (float)(112.5 * Math.PI / 180); // radians

        private float[,] NonMaximumSuppression(float[,] gradient)
        {
            nonMax = Copy(gradient);

            int limit = kernelSize / 2;
            int limitX = width - limit;
            int limitY = height - limit;

            int i, j;

            for (i = limit; i < limitX; i++)
            {
                for (j = limit; j < limitY; j++)
                {
                    float tangent;

                    float dx = derivativeX[i, j];

                    if (dx == 0)
                    {
                        tangent = VerticalAngle;
                    }
                    else
                    {
                        float dy = derivativeY[i, j];
                        tangent = (float)Math.Atan(dy / dx);
                    }

                    float g = gradient[i, j];

                    //Horizontal Edge
                    if (((-HorizontalMinimum < tangent) && (tangent <= HorizontalMinimum)) ||
                        ((HorizontalMaximum < tangent) && (tangent <= -HorizontalMaximum)))
                    {
                        if ((g < gradient[i, j + 1]) || (g < gradient[i, j - 1]))
                        {
                            nonMax[i, j] = 0;
                        }
                    }


                    //Vertical Edge
                    if (((-VerticalMaximum < tangent) && (tangent <= -VerticalMinimum)) ||
                        ((VerticalMinimum < tangent) && (tangent <= VerticalMaximum)))
                    {
                        if ((g < gradient[i + 1, j]) || (g < gradient[i - 1, j]))
                        {
                            nonMax[i, j] = 0;
                        }
                    }

                    //+45 Degree Edge
                    if (((-VerticalMinimum < tangent) && (tangent <= -HorizontalMinimum)) ||
                        ((VerticalMaximum < tangent) && (tangent <= HorizontalMaximum)))
                    {
                        if ((g < gradient[i + 1, j - 1]) || (g < gradient[i - 1, j + 1]))
                        {
                            nonMax[i, j] = 0;
                        }
                    }

                    //-45 Degree Edge
                    if (((-HorizontalMaximum < tangent) && (tangent <= -VerticalMaximum)) ||
                        ((VerticalMinimum < tangent) && (tangent <= HorizontalMinimum)))
                    {
                        if ((g < gradient[i + 1, j + 1]) || (g < gradient[i - 1, j - 1]))
                        {
                            nonMax[i, j] = 0;
                        }
                    }

                }
            }
            return nonMax;
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

        enum Direction 
        {
            None,
            TopLeft, TopCenter, TopRight,
            MiddleLeft, MiddleRight,
            BottomLeft, BottomCenter, BottomRight
        }

        class IntPoint
        {
            public int X;
            public int Y;

            public IntPoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }


        private int[,] HysterisisThresholding(float[,] nonMax)
        {
            int limit = kernelSize / 2;
            int limitX = width - limit;
            int limitY = height - limit;

            //PostHysteresis = NonMax;   
            postHysteresis = new int[width, height];

            for (int r = limit; r < limitX; r++)
            {
                for (int c = limit; c < limitY; c++)
                {
                    postHysteresis[r, c] = (int)nonMax[r, c];
                }
            }

            edgePoints = new int[width, height];

            for (int r = limit; r < limitX; r++)
            {
                for (int c = limit; c < limitY; c++)
                {
                    int ph = postHysteresis[r, c];
                    if (ph >= maxHysteresisThresh)
                    {
                        edgePoints[r, c] = 1;
                    }
                    if ((ph < maxHysteresisThresh) && (ph >= minHysteresisThresh))
                    {
                        edgePoints[r, c] = 2;
                    }
                }
            }

            edgeMap = new int[width, height];

            visitedMap = new int[width, height];

            int maxLength = 0;

            for (int i = limit; i < limitX; i++)
            {
                for (int j = limit; j < limitY; j++)
                {
                    if (edgePoints[i, j] == 1 && visitedMap[i, j] == 0)
                    {
                        maxLength = Math.Max(maxLength, TraverseEdges(i, j));
                    }
                }
            }

            return edgeMap;
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
            visitedMap[x, y] = 1;

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
                            if (edgePoints[i,j] != 0 && visitedMap[i,j] == 0)
                            {
                                IntPoint next = new IntPoint(i,j);
                                visitedMap[i,j] = 1;
                                stack.Push(next);
                                path.Add(next);
                                length++;
                            }
                        }
                    }
                }
            }
                       
            if (path.Count > minEdgeLength) 
            {
                List<Point> points = new List<Point>();
                foreach (IntPoint pt in path) 
                {
                    edgeMap[pt.X, pt.Y] = 1;
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
