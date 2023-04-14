using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
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
using static System.Net.WebRequestMethods;

namespace SoftwareRayTrace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap writeableBitmap;
        MipArray mipArray;
        int curLod = 0;
        struct Ray
        {
            public Ray(Vector3 p, Vector3 d)
            {
                pos = p;
                dir = Vector3.Normalize(d);
            }

            public Vector3 AtT(float t)
            {
                return pos + t * dir;
            }
            public Vector3 pos;
            public Vector3 dir;
        }

        struct Plane
        {
            public Plane(float _d, Vector3 _n)
            {
                d = _d;
                nrm = Vector3.Normalize(_n);
            }

            public Vector3 nrm;
            public float d;

            public float Intersect(Ray r)
            {
                return -(Vector3.Dot(r.pos, nrm) + d) / Vector3.Dot(r.dir, nrm);
            }
        }

        static Plane[] SidePlanes =
        {
            new Plane(0, new Vector3(1,0,0)),
            new Plane(-1, new Vector3(-1,0,0)),
            new Plane(0, new Vector3(0,1,0)),
            new Plane(-1, new Vector3(0,-1,0)),
            new Plane(0, new Vector3(0,0,1)),
            new Plane(-1, new Vector3(0,0,-1)),
        };

        Ray curRay;
        public MainWindow()
        {
            InitializeComponent();

            writeableBitmap = new WriteableBitmap(
            (int)256,
            (int)256,
            96,
            96,
            PixelFormats.Bgr32,
            null);
            
            RenderOptions.SetBitmapScalingMode(this.topDownView, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this.topDownView, EdgeMode.Aliased);
            this.topDownView.Source = writeableBitmap;

            topDownView.Stretch = Stretch.Uniform;
            topDownView.HorizontalAlignment = HorizontalAlignment.Left;
            topDownView.VerticalAlignment = VerticalAlignment.Top;
            mipArray = MipArray.LoadPng("na.png");
            for (int i = 0; i < mipArray.mips.Length; i++)
            {
                this.LODCb.Items.Add(i.ToString());
            }
            CastRay(new Ray(new Vector3(0.7f, 1.0f, 0.5f), new Vector3(-0.2f, -1, 0)));
            Repaint();
        }

        int RGBToI(byte R, byte G, byte B)
        {
            int color_data = R << 16; // R
            color_data |= G << 8;   // G
            color_data |= B << 0;   // B

            return color_data;
        }

        void DrawRayLine()
        {
            float mint = float.MaxValue;
            int minPlane = -1;
            for (int i = 0; i < SidePlanes.Length; ++i)
            {
                float t = SidePlanes[i].Intersect(this.curRay);
                if (t > 0 && t < mint)
                {
                    minPlane = i;
                    mint = t;
                }
            }
            Vector3 endPt = this.curRay.AtT(mint);
            DrawLine(new Vector2(this.curRay.pos.X, this.curRay.pos.Y), new Vector2(endPt.X, endPt.Y), RGBToI(255, 255, 0));

        }
        void Repaint()
        {
            if (writeableBitmap == null)
                return;
            writeableBitmap.Lock();
            DrawTiles();
            DrawRayLine();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, 256, 256));
            writeableBitmap.Unlock();
        }



        unsafe void DrawTiles()
        {
            // Get a pointer to the back buffer.
            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = 0; y < 256; y++)
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < 256; x++)
                {
                    float val = mipArray.SampleLod((x + 0.5f) / 256.0f , (y + 0.5f) / 256.0f, curLod);                    
                    *((int*)pRowPtr) = RGBToI((byte)(val * 255.0f), (byte)(val * 255.0f), (byte)(val * 255.0f));
                    pRowPtr += 4;
                }
            }
        }

        unsafe void DrawLine(Vector2 start, Vector2 end, int color)
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
                    int tmp = xStart;
                    xStart = xEnd;
                    xEnd = tmp;
                    tmp = yStart;
                    yStart = yEnd;
                    yEnd = tmp;
                }

                bool ishorizontal = dir.Y == 0;
                float slope = dir.X / dir.Y;
                for (int y = yStart; y <= yEnd; ++y)
                {
                    int xs = ishorizontal ? xStart : (int)((y - yStart) * slope) + xStart;
                    int xe = ishorizontal ? xEnd : (int)((y - yStart + 1) * slope) + xStart;
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
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
                    int tmp = xStart;
                    xStart = xEnd;
                    xEnd = tmp;
                    tmp = yStart;
                    yStart = yEnd;
                    yEnd = tmp;
                }
                bool isvertical = dir.X == 0;
                for (int x = xStart; x <= xEnd; ++x)
                {
                    int ys = isvertical ? Math.Min(yStart, yEnd) : (int)((x - xStart) * slope) + yStart;
                    int ye = isvertical ? Math.Max(yEnd, yStart) : (int)((x - xStart + 1) * slope) + yStart;
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


        void CastRay(Ray ray)
        {            
            this.curRay = ray;
            
            for (int lod = mipArray.mips.Length - 1; lod >= 0; --lod)
            {
                Mip mip = mipArray.mips[lod];
                Vector2 pixelSize = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            }
        }

        private void LODCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            curLod = int.Parse((string)this.LODCb.SelectedItem);
            Repaint();
        }
    }
}
