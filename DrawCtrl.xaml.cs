using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SoftwareRayTrace
{
    /// <summary>
    /// Interaction logic for DrawCtrl.xaml
    /// </summary>
    public partial class DrawCtrl : Image
    {
        public WriteableBitmap writeableBitmap;
        public DrawCtrl()
        {
            InitializeComponent();
            writeableBitmap = new WriteableBitmap(
            (int)256,
            (int)256,
            96,
            96,
            PixelFormats.Bgr32,
            null);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            this.Source = writeableBitmap;

            this.Stretch = Stretch.Uniform;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;
        }

        public static int RGBToI(byte R, byte G, byte B)
        {
            int color_data = R << 16; // R
            color_data |= G << 8;   // G
            color_data |= B << 0;   // B

            return color_data;
        }

        public void Begin()
        {
            writeableBitmap.Lock();
        }

        public void End()
        {
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, 256, 256));
            writeableBitmap.Unlock();
        }

        public unsafe void DrawTiles(MipArray mipArray, int curLod)
        {
            // Get a pointer to the back buffer.
            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = 0; y < 256; y++)
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < 256; x++)
                {
                    float val = mipArray.SampleLod((x + 0.5f) / 256.0f, (y + 0.5f) / 256.0f, curLod);
                    *((int*)pRowPtr) = RGBToI((byte)(val * 255.0f), (byte)(val * 255.0f), (byte)(val * 255.0f));
                    pRowPtr += 4;
                }
            }
        }
        public unsafe void DrawPoint(Vector2 pos, int pad, int color)
        {
            int ymid = (int)(pos.Y * 256);
            int xmid = (int)(pos.X * 256);

            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = ymid - pad; y <= ymid + pad; ++y)
            {
                for (int x = xmid - pad; x <= xmid + pad; ++x)
                {
                    if (x < 0 || x >= 256 || y < 0 || y >= 256)
                        continue;
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                    nint pCur = pRowPtr + x * 4;
                    *((int*)pCur) = color;
                }
            }
        }

        public unsafe void DrawLine(Vector2 start, Vector2 end, int color)
        {
            nint pBackBuffer = writeableBitmap.BackBuffer;
            Vector2 dir = end - start;
            if (Math.Abs(dir.X) > Math.Abs(dir.Y))
            {
                int yStart = (int)(start.Y * 256);
                int xStart = (int)(start.X * 256);
                int yEnd = (int)(end.Y * 256);
                int xEnd = (int)(end.X * 256);
                if (yStart > yEnd)
                {
                    (xStart, xEnd) = (xEnd, xStart);
                    (yStart, yEnd) = (yEnd, yStart);
                }

                bool ishorizontal = dir.Y == 0;
                float slope = dir.X / dir.Y;
                for (int y = yStart; y <= yEnd; ++y)
                {
                    int xs = ishorizontal ? xStart : (int)((y - yStart) * slope) + xStart;
                    int xe = ishorizontal ? xEnd : (int)((y - yStart + 1) * slope) + xStart;
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                    if (xs > xe) (xs, xe) = (xe, xs);
                    for (int x = xs; x < xe; ++x)
                    {
                        if (y < 0 || y > 255 || x < 0 || x > 255)
                            continue;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }
            }
            else
            {
                int yStart = (int)(start.Y * 256);
                int yEnd = (int)(end.Y * 256);
                int xStart = (int)(start.X * 256);
                int xEnd = (int)(end.X * 256);
                float slope = dir.Y / dir.X;
                if (xStart > xEnd)
                {
                    (xStart, xEnd) = (xEnd, xStart);
                    (yStart, yEnd) = (yEnd, yStart);
                }
                bool isvertical = dir.X == 0;
                for (int x = xStart; x <= xEnd; ++x)
                {
                    int ys = isvertical ? yStart : (int)((x - xStart) * slope) + yStart;
                    int ye = isvertical ? yEnd : (int)((x - xStart + 1) * slope) + yStart;
                    if (ys > ye) (ys, ye) = (ye, ys);
                    for (int y = ys; y < ye; ++y)
                    {
                        if (y < 0 || y > 255 || x < 0 || x > 255)
                            continue;
                        nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }

            }
        }


        public unsafe void DrawView(MipArray mipArray, Matrix4x4 invMat)
        {
            Raycaster raycaster = new Raycaster(mipArray);
            nint pBackBuffer = writeableBitmap.BackBuffer;
            Vector2 scale = new Vector2(1.0f / (float)writeableBitmap.Width, 
                1.0f / (float)writeableBitmap.Height);
            for (int y = 0; y < writeableBitmap.Height; ++y )
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < writeableBitmap.Width; ++x )
                {
                    Vector2 vps = new Vector2(x, y) * scale;
                    vps.Y = 1 - vps.Y;
                    Ray r = RayUtils.RayFromView(vps, invMat);
                    Vector2 hitPos;
                    if (raycaster.Raycast(r, out hitPos))
                    {                        
                        * ((int*)pRowPtr) = RGBToI((byte)(hitPos.X * 255), (byte)(hitPos.Y * 255), 100);
                    }
                    else
                        * ((int*)pRowPtr) = RGBToI(0,100,255);
                    pRowPtr += 4;
                }
            }
        }
    }
}
