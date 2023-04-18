using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Windows.Media;
using System.Collections;
using System.Windows.Media.Imaging;

namespace SoftwareRayTrace
{
    public struct Ray
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

    struct Cube
    {
        public static Plane[] SidePlanes =
        {
            new Plane(0, new Vector3(1,0,0)),
            new Plane(-1, new Vector3(-1,0,0)),
            new Plane(0, new Vector3(0,1,0)),
            new Plane(-1, new Vector3(0,-1,0)),
            new Plane(0, new Vector3(0,0,1)),
            new Plane(-1, new Vector3(0,0,-1)),
        };
    }

    internal class RayUtils
    {
        public static float IntersectXZPlane(Ray r, float xplane, float yplane)
        {
            float slope = r.dir.X / r.dir.Z;
            float tXPlane = (xplane - r.pos.X) / r.dir.X;
            float tYPlane = (yplane - r.pos.Z) / r.dir.Z;
            return Math.Min(tXPlane, tYPlane);
        }


        public static Ray RayFromView(Vector2 vps, Matrix4x4 invMat)
        {
            Vector4 v0 = Vector4.Transform(new Vector4(vps.X, vps.Y, 0.0f, 1), invMat);
            Vector4 v1 = Vector4.Transform(new Vector4(vps.X, vps.Y, 1.0f, 1), invMat);
            v0 /= v0.W;
            v1 /= v1.W;

            Vector4 dir4 = v1 - v0;
            Vector3 dir = Vector3.Normalize(new Vector3(dir4.X, dir4.Y, dir4.Z));
            return new Ray(new Vector3(v0.X, v0.Y, v0.Z), dir);
        }

    }

    public class Raycaster
    {
        MipArray mipArray;
        public Raycaster(MipArray _mipArray)
        {
            mipArray = _mipArray;
        }

        public bool Raycast(Ray ray, out Vector2 outT)
        {
            return Raycast(ray, mipArray.MaxLod, new Vector2(0.5f, 0.5f), out outT);
        }
        bool Raycast(Ray ray, int lod, Vector2 pixelCenter, out Vector2 outT)
        {
            outT = new Vector2(-1, -1);
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
                if (isHit) break;
            }

            return isHit;
        }
    }
}
