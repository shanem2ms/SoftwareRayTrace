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
using System.Diagnostics;
using static System.Net.WebRequestMethods;

namespace SoftwareRayTrace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MipArray mipArray;
        int curLod = 5;
        
        TraceStep curTs;
        public MainWindow()
        {
            InitializeComponent();

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

            this.topDown.MouseDown += TopDown_MouseDown;
            Repaint();
        }

        private void TopDown_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double xPos = e.GetPosition(this.topDown).X / this.topDown.ActualWidth;
            double yPos = e.GetPosition(this.topDown).Y / this.topDown.ActualHeight;

            this.curTs.ray = new Ray(new Vector3((float)xPos, 1.0f, 0.9f), new Vector3(-0.2f, -1, 0));
            Repaint();
            //Debug.WriteLine($"{xPos} {yPos}");
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
            this.topDown.DrawTiles(this.mipArray, this.curLod);
            FindIntersectionPixels(this.curTs.ray, this.curLod);
            this.topDown.End();
        }

        void DrawRayLine()
        {
            float mint = float.MaxValue;
            int minPlane = -1;
            for (int i = 0; i < Cube.SidePlanes.Length; ++i)
            {
                float t = Cube.SidePlanes[i].Intersect(this.curTs.ray);
                if (t > 0 && t < mint)
                {
                    minPlane = i;
                    mint = t;
                }
            }
            Vector3 endPt = this.curTs.ray.AtT(mint);
            this.topDown.DrawLine(new Vector2(this.curTs.ray.pos.X, this.curTs.ray.pos.Y), new Vector2(endPt.X, endPt.Y), DrawCtrl.RGBToI(255, 255, 0));

        }
        void FindIntersectionPixels(Ray ray, int lod)
        {
            Mip mip = mipArray.mips[lod];
            Vector2 invscale = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            Ray cr = ray;
            cr.pos = ray.pos * new Vector3(mip.width, mip.height, 1);

            bool leftPlane = cr.dir.X < 0;
            bool backPlane = cr.dir.Y < 0;
            float epsilon = mip.width / 100.0f;
            float prevZ = cr.pos.Z;
            while (cr.pos.X > 0 && cr.pos.Y > 0)
            {
                Vector2 origPos = new Vector2(cr.pos.X, cr.pos.Y);
                float nextPlaneX = leftPlane ? MathF.Truncate(cr.pos.X - epsilon) :
                    MathF.Truncate(cr.pos.X + epsilon + 1);
                float nextPlaneY = backPlane ? MathF.Truncate(cr.pos.Y - epsilon) :
                    MathF.Truncate(cr.pos.Y + epsilon + 1);

                cr.pos = RayUtils.IntersectXYPlance(cr, nextPlaneX, nextPlaneY);
                float nx = MathF.Truncate((origPos.X + cr.pos.X) * 0.5f) + 0.5f;
                float ny = MathF.Truncate((origPos.Y + cr.pos.Y) * 0.5f) + 0.5f;
                

                Vector2 np = new Vector2(nx, ny) * invscale;
                Vector2 p = new Vector2(cr.pos.X, cr.pos.Y) * invscale;

                float v = mip.Sample(np.X, np.Y);
                bool isHit = false;
                if (prevZ < v || cr.pos.Z < v)
                {
                    isHit = true;
                    // Hit
                }

                prevZ = cr.pos.Z;
                //this.topDown.DrawLine(new Vector2(p.X, p.Y), new Vector2(np.X, np.Y), DrawCtrl.RGBToI(0, 0, 255));
                this.topDown.DrawPoint(new Vector2(p.X, p.Y), isHit ? 1: 0, isHit ? DrawCtrl.RGBToI(255, 0, 255) : DrawCtrl.RGBToI(255, 0, 0));
                if (isHit) break;
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
