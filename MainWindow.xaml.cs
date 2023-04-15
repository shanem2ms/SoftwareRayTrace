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

        class TraceStep
        {
            public Ray ray;
            public int lod;
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

        TraceStep curTs;
        Draw topDown;
        public MainWindow()
        {
            InitializeComponent();


            topDown = new Draw();
            RenderOptions.SetBitmapScalingMode(this.topDownView, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this.topDownView, EdgeMode.Aliased);
            this.topDownView.Source = topDown.writeableBitmap;

            topDownView.Stretch = Stretch.Uniform;
            topDownView.HorizontalAlignment = HorizontalAlignment.Left;
            topDownView.VerticalAlignment = VerticalAlignment.Top;
            mipArray = MipArray.LoadPng("na.png");
            for (int i = 0; i < mipArray.mips.Length; i++)
            {
                this.LODCb.Items.Add(i.ToString());
            }
            this.curTs = new TraceStep()
            {
                lod = this.mipArray.MaxLod,
                ray = new Ray(new Vector3(0.7f, 1.0f, 0.9f), new Vector3(-0.2f, -1, 0))
            };
            Repaint();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                Trace(this.curTs);
                Repaint();
            }
            base.OnKeyDown(e);
        }
        void Repaint()
        {
            this.topDown.Begin();
            this.topDown.DrawTiles(this.mipArray, this.curTs.lod);
            this.topDown.DrawPoint(new Vector2(
                this.curTs.ray.pos.X, this.curTs.ray.pos.Y), Draw.RGBToI(255,0,0));
            this.topDown.End();
        }

        void DrawRayLine()
        {
            float mint = float.MaxValue;
            int minPlane = -1;
            for (int i = 0; i < SidePlanes.Length; ++i)
            {
                float t = SidePlanes[i].Intersect(this.curTs.ray);
                if (t > 0 && t < mint)
                {
                    minPlane = i;
                    mint = t;
                }
            }
            Vector3 endPt = this.curTs.ray.AtT(mint);
            this.topDown.DrawLine(new Vector2(this.curTs.ray.pos.X, this.curTs.ray.pos.Y), new Vector2(endPt.X, endPt.Y), Draw.RGBToI(255, 255, 0));

        }
        void FindIntersectionPixels(Ray ray, int lod)
        {
            Mip mip = mipArray.mips[lod];
            Vector2 pixelSize = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            float dist = pixelSize.Length();
            Ray cr = ray;
            while (cr.pos.X >= 0 && cr.pos.Y >= 0)
            {
                Vector3 p = cr.AtT(dist * 0.5f);
                this.topDown.DrawPoint(new Vector2(p.X, p.Y), Draw.RGBToI(255, 0, 0));
                cr.pos = p;
            }
        }

        void Trace(TraceStep ts)
        {
            Mip mip = mipArray.mips[ts.lod];
            Vector2 pixelSize = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            float dist = pixelSize.Length();
            Vector3 p = ts.ray.AtT(dist * 0.5f);
            float val = mip.Sample(p.X, p.Y);
            if (p.Z < val)
            {
                ts.lod--;
            }
            else
            {
                ts.ray.pos = p;
                ts.lod++;
            }
        }


        bool RayCast(Ray ray)
        {
            TraceStep ts = new TraceStep { lod = this.mipArray.MaxLod, ray = ray };
            return false;
        }

        private void LODCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            curLod = int.Parse((string)this.LODCb.SelectedItem);
            Repaint();
        }
    }
}
