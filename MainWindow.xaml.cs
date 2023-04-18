﻿using System;
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
                ray = new Ray(new Vector3(0.7f, 0.9f, 1.0f), new Vector3(0, 0, -1))
            };

            this.topDown.MouseDown += TopDown_MouseDown;
            Repaint();
        }

        private void TopDown_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double xPos = e.GetPosition(this.topDown).X / this.topDown.ActualWidth;
            double yPos = e.GetPosition(this.topDown).Y / this.topDown.ActualHeight;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.curTs.ray = new Ray(new Vector3((float)xPos, 0.9f, 1.0f), this.curTs.ray.dir);
            }
            else
            {
                this.curTs.ray = new Ray(this.curTs.ray.pos, new Vector3((float)xPos, 0.9f, 0.0f) - this.curTs.ray.pos);
            }
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
            Vector2 hitpos;
            if (Raycast(this.curTs.ray, this.mipArray.MaxLod, new Vector2(0.5f, 0.5f), out hitpos))
            {
                float offX = hitpos.X - this.curTs.ray.pos.X;
                float offY = hitpos.Y - this.curTs.ray.pos.Y;
                Vector3 dir = this.curTs.ray.dir;
                float r1 = offX / dir.X;
                float r2 = offY / dir.Y;
                Vector3 p = this.curTs.ray.AtT(r1);
                Debug.WriteLine($"hitpos: {hitpos.X}, {hitpos.Y}");
            }

            //FindIntersectionPixels(this.curTs.ray, this.curLod);
            this.topDown.End();

            this.camView.Begin();
            this.camView.DrawView();
            this.camView.End();
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

        Vector2 FloorToSamplePixel(Vector2 pix, Vector2 scale)
        {
            Vector2 spix = pix * scale;
            return new Vector2(MathF.Floor(spix.X + 0.5f) / scale.X,
                MathF.Floor(spix.Y + 0.5f) / scale.X);
        }
        bool FindIntersectionPixels(Ray ray, int lod)
        {
            bool isHit = false;
            Mip mip = mipArray.mips[lod];
            Vector2 invscale = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            Ray cr = ray;
            cr.pos = ray.pos * new Vector3(mip.width, mip.height, 1);

            bool leftPlane = cr.dir.X < 0;
            bool backPlane = cr.dir.Y < 0;
            float epsilon = mip.width / 100.0f;
            float prevZ = cr.pos.Z;
            while (!isHit && cr.pos.X > 0 && cr.pos.Y > 0)
            {
                Vector2 origPos = new Vector2(cr.pos.X, cr.pos.Y);
                float nextPlaneX = leftPlane ? MathF.Floor(cr.pos.X - epsilon) :
                    MathF.Floor(cr.pos.X + epsilon + 1);
                float nextPlaneY = backPlane ? MathF.Floor(cr.pos.Y - epsilon) :
                    MathF.Floor(cr.pos.Y + epsilon + 1);

                float it = RayUtils.IntersectXZPlane(cr, nextPlaneX, nextPlaneY);
                cr.pos = cr.AtT(it);
                float nx = MathF.Floor((origPos.X + cr.pos.X) * 0.5f) + 0.5f;
                float ny = MathF.Floor((origPos.Y + cr.pos.Y) * 0.5f) + 0.5f;               

                Vector2 np = new Vector2(nx, ny) * invscale;
                Vector2 p = new Vector2(cr.pos.X, cr.pos.Y) * invscale;

                float v = mip.Sample(np.X, np.Y);
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

            return isHit;
        }

        bool Raycast(Ray ray, int lod, Vector2 pixelCenter, out Vector2 outT)
        {
            outT = new Vector2(-1,-1);
            bool isHit = false;
            Mip mip = mipArray.mips[lod];
            Vector2 invscale = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            Ray cr = ray;
            cr.pos = ray.pos * new Vector3(mip.width, 1, mip.height);

            bool leftPlane = cr.dir.X < 0;
            bool backPlane = cr.dir.Z < 0;
            float epsilon = mip.width / 100.0f;
            float prevY = cr.pos.Y;
            while (!isHit)
            {
                Vector2 origPos = new Vector2(cr.pos.X, cr.pos.Z);
                float nextPlaneX = leftPlane ? MathF.Floor(cr.pos.X - epsilon) :
                    MathF.Floor(cr.pos.X + epsilon + 1);
                float nextPlaneY = backPlane ? MathF.Floor(cr.pos.Z - epsilon) :
                    MathF.Floor(cr.pos.Z + epsilon + 1);

                float it = RayUtils.IntersectXZPlane(cr, nextPlaneX, nextPlaneY);
                Vector3 newpos = cr.AtT(it);
                float nx = MathF.Floor((origPos.X + newpos.X) * 0.5f) + 0.5f;
                float ny = MathF.Floor((origPos.Y + newpos.Z) * 0.5f) + 0.5f;
                Vector2 np = new Vector2(nx, ny) * invscale;
                if (MathF.Abs(np.X - pixelCenter.X) >= invscale.X)
                    break;
                if (MathF.Abs(np.Y - pixelCenter.Y) >= invscale.Y)
                    break;
                Vector2 p = new Vector2(newpos.X, newpos.Z) * invscale;

                float v = mip.Sample(np.X, np.Y);
                if (prevY < v || newpos.Y < v)
                {
                    if (lod > 0)
                    {
                        Ray r = cr;
                        r.pos *= new Vector3(invscale.X, 1, invscale.Y);
                        isHit = Raycast(r, lod - 1, np, out outT);
                    }
                    else
                    {
                        outT = new Vector2(np.X, np.Y);
                        isHit = true;
                    }
                    // Hit
                }

                cr.pos = newpos;
                prevY = cr.pos.Y;
                //this.topDown.DrawLine(new Vector2(p.X, p.Y), new Vector2(np.X, np.Y), DrawCtrl.RGBToI(0, 0, 255));
                bool foundPoint = isHit && lod == 0;
                this.topDown.DrawPoint(new Vector2(origPos.X * invscale.X, origPos.Y * invscale.Y), foundPoint ? 1 : 0, 
                    foundPoint ? DrawCtrl.RGBToI(0, 255, 0) : DrawCtrl.RGBToI(255, 0, 0));
                if (isHit) break;
            }

            return isHit;
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
    }
}
