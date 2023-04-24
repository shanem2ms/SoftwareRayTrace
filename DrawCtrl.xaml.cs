using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
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

        public int BitmapSize { get; set; } = 256;
        public DrawCtrl()
        {
            InitializeComponent();

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

        void InitBitmap()
        {
            writeableBitmap = new WriteableBitmap(
            (int)BitmapSize,
            (int)BitmapSize,
            96,
            96,
            PixelFormats.Bgr32,
            null);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            this.Source = writeableBitmap;

        }

        public void Begin()
        {
            if (writeableBitmap == null) { InitBitmap(); }
            writeableBitmap.Lock();
        }

        public void End()
        {
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, BitmapSize, BitmapSize));
            writeableBitmap.Unlock();
        }

        public unsafe void DrawTiles(MipArray mipArray, int curLod)
        {
            // Get a pointer to the back buffer.
            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = 0; y < BitmapSize; y++)
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < BitmapSize; x++)
                {
                    float val = mipArray.SampleLod((x + 0.5f) / (float)BitmapSize, (y + 0.5f) / (float)BitmapSize, curLod);
                    *((int*)pRowPtr) = RGBToI((byte)(val * 255.0f), (byte)(val * 255.0f), (byte)(val * 255.0f));
                    pRowPtr += 4;
                }
            }
        }
        public unsafe void DrawPoint(Vector2 pos, int pad, int color)
        {
            int ymid = (int)(pos.Y * BitmapSize);
            int xmid = (int)(pos.X * BitmapSize);

            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = ymid - pad; y <= ymid + pad; ++y)
            {
                for (int x = xmid - pad; x <= xmid + pad; ++x)
                {
                    if (x < 0 || x >= BitmapSize || y < 0 || y >= BitmapSize)
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
                int yStart = (int)(start.Y * BitmapSize);
                int xStart = (int)(start.X * BitmapSize);
                int yEnd = (int)(end.Y * BitmapSize);
                int xEnd = (int)(end.X * BitmapSize);
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
                        if (y < 0 || y > (BitmapSize - 1) || x < 0 || x > (BitmapSize - 1))
                            continue;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }
            }
            else
            {
                int yStart = (int)(start.Y * BitmapSize);
                int yEnd = (int)(end.Y * BitmapSize);
                int xStart = (int)(start.X * BitmapSize);
                int xEnd = (int)(end.X * BitmapSize);
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
                        if (y < 0 || y > (BitmapSize - 1) || x < 0 || x > (BitmapSize - 1))
                            continue;
                        nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }

            }
        }


        public unsafe void DrawViewFwd(MipArray mipArray, Matrix4x4 viewProj)
        {
            Clear();
            for (int x = 0; x < 10; ++x)
            {
                for (int y = 0; y < 10; ++y)
                {
                    Matrix4x4 matViewProj = Matrix4x4.CreateScale(0.1f) * 
                        Matrix4x4.CreateTranslation(new Vector3(x * 0.1f, 0, y * 0.1f)) * viewProj;
                    DrawQuad(matViewProj, RGBToI(255, 0, 0));
                }
            }
            for (int x = 0; x < 10; ++x)
            {
                for (int y = 0; y < 10; ++y)
                {
                    Matrix4x4 matViewProj = Matrix4x4.CreateScale(0.1f) *
                        Matrix4x4.CreateTranslation(new Vector3(x * 0.1f, 0.1f, y * 0.1f)) * viewProj;
                    DrawQuad(matViewProj, RGBToI(0, 255, 0));
                }
            }
        }

        void DrawQuad(Matrix4x4 matViewProj, int color)
        {
            Vector4[] p = new Vector4[4]
            {
                new Vector4(0,0,0,1),
                new Vector4(1,0,0,1),
                new Vector4(1,0,1,1),
                new Vector4(0,0,1,1)
            };
            int[] ib = new int[6] { 0, 1, 2, 0, 2, 3 };

            for (int triIdx = 0; triIdx < ib.Length; triIdx += 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    int ni = (i + 1) % 3;
                    int i0 = ib[triIdx + i];
                    int i1 = ib[triIdx + ni];
                    Vector4 s0 = Vector4.Transform(p[i0], matViewProj);
                    Vector4 s1 = Vector4.Transform(p[i1], matViewProj);
                    s0 /= s0.W;
                    s1 /= s1.W;
                    if (s0.Z > 0 && s0.Z < 1 && s1.Z > 0 && s1.Z < 1)
                    {
                        DrawLine(new Vector2(s0.X, s0.Y), new Vector2(s1.X, s1.Y), color);
                    }
                }
            }
        }

        unsafe public void Clear()
        {
            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int i = 0; i < writeableBitmap.BackBufferStride * writeableBitmap.Height; ++i)
            {
                *(((byte*)pBackBuffer) + i) = 0;
            }
        }

        public unsafe void DrawView(MipArray mipArray, Matrix4x4 invMat)
        {
            Clear();
            Raycaster raycaster = new Raycaster(mipArray, 0);
            nint pBackBuffer = writeableBitmap.BackBuffer;
            Vector2 scale = new Vector2(1.0f / (float)writeableBitmap.Width, 
                1.0f / (float)writeableBitmap.Height);
            for (int y = 0; y < writeableBitmap.Height; ++y )
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < writeableBitmap.Width; ++x )
                {
                    Vector2 vps = new Vector2(x, y) * scale;
                    float viewDist;
                    Ray r = RayUtils.RayFromView(vps, invMat, out viewDist);
                    Vector3 color;
                    int numIters;
                    if (raycaster.SdfCast(r, viewDist, out color, out numIters))
                    {                        
                        * ((int*)pRowPtr) = RGBToI((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255));
                    }
                    else
                        * ((int*)pRowPtr) = RGBToI(0,100,255);
                    pRowPtr += 4;
                }
            }
        }
    }
}
