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

    }

    public class Raycaster
    {
        MipArray mipArray;
        Raycaster(MipArray _mipArray)
        {
            mipArray = _mipArray;
        }

        bool Raycast(Ray ray, int lod, Vector2 pixelCenter, out Vector2 outT)
        {
            outT = new Vector2(-1, -1);
            bool isHit = false;
            Mip mip = mipArray.mips[lod];
            Vector2 invscale = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            Ray cr = ray;
            cr.pos = ray.pos * new Vector3(mip.width, mip.height, 1);

            bool leftPlane = cr.dir.X < 0;
            bool backPlane = cr.dir.Y < 0;
            float epsilon = mip.width / 100.0f;
            float prevZ = cr.pos.Z;
            while (!isHit)
            {
                Vector2 origPos = new Vector2(cr.pos.X, cr.pos.Y);
                float nextPlaneX = leftPlane ? MathF.Floor(cr.pos.X - epsilon) :
                    MathF.Floor(cr.pos.X + epsilon + 1);
                float nextPlaneY = backPlane ? MathF.Floor(cr.pos.Y - epsilon) :
                    MathF.Floor(cr.pos.Y + epsilon + 1);

                float it = RayUtils.IntersectXZPlane(cr, nextPlaneX, nextPlaneY);
                Vector3 newpos = cr.AtT(it);
                float nx = MathF.Floor((origPos.X + newpos.X) * 0.5f) + 0.5f;
                float ny = MathF.Floor((origPos.Y + newpos.Y) * 0.5f) + 0.5f;
                Vector2 np = new Vector2(nx, ny) * invscale;
                if (MathF.Abs(np.X - pixelCenter.X) >= invscale.X)
                    break;
                if (MathF.Abs(np.Y - pixelCenter.Y) >= invscale.Y)
                    break;
                Vector2 p = new Vector2(newpos.X, newpos.Y) * invscale;

                float v = mip.Sample(np.X, np.Y);
                if (prevZ < v || newpos.Z < v)
                {
                    if (lod > 0)
                    {
                        Ray r = cr;
                        r.pos *= new Vector3(invscale.X, invscale.Y, 1);
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
                prevZ = cr.pos.Z;
                //this.topDown.DrawLine(new Vector2(p.X, p.Y), new Vector2(np.X, np.Y), DrawCtrl.RGBToI(0, 0, 255));
                bool foundPoint = isHit && lod == 0;
                //this.topDown.DrawPoint(new Vector2(origPos.X * invscale.X, origPos.Y * invscale.Y), foundPoint ? 1 : 0,
                //    foundPoint ? DrawCtrl.RGBToI(0, 255, 0) : DrawCtrl.RGBToI(255, 0, 0));
                if (isHit) break;
            }

            return isHit;
        }

    }
}
