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
using System.ComponentModel;
using System.Threading;
using System.Windows.Media.Animation;

namespace SoftwareRayTrace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MipArray mipArray;
        int curLod = 5;
        Matrix4x4 projMat = 
                Matrix4x4.CreatePerspectiveFieldOfView(60.0f * MathF.PI / 180.0f, 1.0f, 0.01f, 100);

        float yaw = -0.006532222f;
        float pitch = -0.55914086f;
        Vector3 pos = new Vector3(-0.11f, 0.24000022f, -2.0600004f);
        bool raycastMode = false;
        TraceStep curTs;
        public Matrix4x4 ViewProj
        {
            get
            {
                return Matrix4x4.CreateScale(new Vector3(1, -1, 1)) * 
                    Matrix4x4.CreateRotationX(yaw) *
                    Matrix4x4.CreateRotationY(pitch) *
                    Matrix4x4.CreateTranslation(pos) *                    
                    projMat;
            }
        }

        public Matrix4x4 InvMat
        {
            get
            {
                Matrix4x4 inv;
                Matrix4x4.Invert(ViewProj, out inv);
                return inv;
            }
        }
        
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
                ray = new Ray(new Vector3(0.12873986f, 0.3276748f, 2.129348f), new Vector3(0.15054968f, -0.11253275f, -0.9821768f))
            };

            this.topDown.MouseDown += TopDown_MouseDown;
            this.camView.MouseDown += CamView_MouseDown;
            this.camView.MouseMove += CamView_MouseMove;
            this.camView.MouseUp += CamView_MouseUp;
            Repaint();
        }

        private void CamView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            
        }

        float yawMouseDown;
        float yMouseDown;
        float pitchMouseDown;
        float xMouseDown;
        private void CamView_MouseMove(object sender, MouseEventArgs e)
        {
            double xPos = e.GetPosition(this.camView).X / this.camView.ActualWidth;
            double yPos = e.GetPosition(this.camView).Y / this.camView.ActualHeight;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                yaw = yawMouseDown - ((float)yPos - yMouseDown);
                pitch = pitchMouseDown + ((float)xPos - xMouseDown);
                Repaint();
            }
        }

        private void CamView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double xPos = e.GetPosition(this.camView).X / this.camView.ActualWidth;
            double yPos = e.GetPosition(this.camView).Y / this.camView.ActualHeight;
            Vector2 vps = new Vector2((float)xPos, (float)(yPos));
            float viewDist;
            Ray r = RayUtils.RayFromView(vps, InvMat, out viewDist);
            this.curTs.ray = r;
            Repaint();

            yawMouseDown = yaw;
            yMouseDown = (float)yPos;
            pitchMouseDown = pitch;
            xMouseDown = (float)xPos;
        }

        private void TopDown_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double xPos = e.GetPosition(this.topDown).X / this.topDown.ActualWidth;
            double yPos = e.GetPosition(this.topDown).Y / this.topDown.ActualHeight;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.curTs.ray = new Ray(new Vector3((float)xPos, 0.9f, 0.0f), this.curTs.ray.dir);
            }
            else
            {
                this.curTs.ray = new Ray(this.curTs.ray.pos, new Vector3((float)xPos, 0.9f, 1.0f) - this.curTs.ray.pos);
            }
            Repaint();
            //Debug.WriteLine($"{xPos} {yPos}");
        }

        float speed = 0.01f;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Right)
            {
                Trace(this.curTs);
                Repaint();
            }

            switch (e.Key)
            {
                case Key.W:
                    this.pos.Z += speed;
                    break;
                case Key.S:
                    this.pos.Z -= speed;
                    break;
                case Key.A:
                    this.pos.X += speed;
                    break;
                case Key.D:
                    this.pos.X -= speed;
                    break;
                case Key.Q:
                    this.pos.Y += speed;
                    break;
                case Key.Z:
                    this.pos.Y -= speed;
                    break;
                case Key.O:
                    Debug.WriteLine($"        float yaw = {yaw}f;");
                    Debug.WriteLine($"        float pitch = {pitch}f;");
                    Debug.WriteLine($"        Vector3 pos = new Vector3({pos.X}f, {pos.Y}f, {pos.Z}f);");

                    break;
            }
            Repaint();
            base.OnKeyDown(e);
        }

        void RayCastTracePt(TraceData d)
        {
            TraceItemsList.Items.Add($"{d.lod} smp=[{d.spos.X} {d.spos.Y}] v={d.val} pos[{d.pos.X}, {d.pos.Y}, {d.pos.Z}] testpos[{d.nextPos.X} {d.nextPos.Y} {d.nextPos.Z}] ");
        }
        void Repaint()
        {
            this.rayPos.Text = this.curTs.ray.pos.ToString();
            this.rayDir.Text = this.curTs.ray.dir.ToString();
            this.topDown.Begin();
            this.topDown.DrawTiles(this.mipArray, this.curLod);
            TraceItemsList.Items.Clear();
            Raycaster raycaster = new Raycaster(this.mipArray, 0);
            raycaster.TraceFunc = RayCastTracePt;

            int numIters;
            raycaster.SdfCast(this.curTs.ray, 100, out _, out numIters);
            TraceItemsList.Items.Add($"numiters = {numIters}");

            DrawPath(this.curTs.ray);
            //FindIntersectionPixels(this.curTs.ray, this.curLod);
            this.topDown.End();

            this.camView.Begin();
            if (raycastMode)
                this.camView.DrawView(this.mipArray, this.InvMat);
            else
                this.camView.DrawViewFwd(this.mipArray, this.ViewProj);
            this.camView.End();
        }


        void DrawPath(Ray ray)
        {
            float t0, t1;
            bool intersected = RayUtils.IntersectAABoxRay(new Vector3(0, 0, 0), new Vector3(1, 0.1f, 1), ray, out t0, out t1);
            if (intersected)
            {
                Vector3 hitpos0 = ray.AtT(t0);
                Vector3 hitpos1 = ray.AtT(t1);
                this.topDown.DrawLine(new Vector2(hitpos0.X, hitpos0.Z), new Vector2(hitpos1.X, hitpos1.Z), DrawCtrl.RGBToI(0, 50, 255));
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


        private void LODCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            curLod = int.Parse((string)this.LODCb.SelectedItem);
            Repaint();
        }

        private void camViewType_Checked(object sender, RoutedEventArgs e)
        {
            raycastMode = (sender as CheckBox).IsChecked == true;
            Repaint();
        }
    }
}
