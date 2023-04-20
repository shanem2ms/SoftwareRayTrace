using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Windows.Media;
using System.Collections;
using System.Windows.Media.Imaging;
using System.ComponentModel.DataAnnotations;

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


    public struct Plane
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
        

        static float IntersectPlane(Ray ray, Vector3 planeOffset, Vector3 N)
        {
            // t = -(n·P + d)            
            float t = 0;
            float denom = Vector3.Dot(N, ray.dir);
            if (MathF.Abs(denom) < 0.0001)    // Ray parallel to plane
            {
                return float.PositiveInfinity;
            }

            t = Vector3.Dot(N, (N * planeOffset - ray.pos)) / denom;

            return t;
        }

        public static float IntersectBoundingBox(Ray r, Vector3 min, Vector3 max)
        {
            Vector3 scale = (max - min);
            float outT = float.MaxValue;
            for (int i = 0; i < 6; ++i)
            {
                Vector3 pos = (Cube.SidePlanes[i].d * Cube.SidePlanes[i].nrm * scale) + min;
                float t = IntersectPlane(r, pos, Cube.SidePlanes[i].nrm);
                if (float.IsFinite(t) && t > 0)
                {
                    Vector3 hitpos = r.AtT(t);
                    if (hitpos.X >= min.X && hitpos.Y >= min.Y && hitpos.Z >= min.Z &&
                        hitpos.X <= max.X && hitpos.Y <= max.Y && hitpos.Z <= max.Z)
                    {
                        outT = MathF.Min(t, outT);
                    }
                }
            }
            return outT;
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

        public static bool IntersectAABoxRay(Vector3 boxMin, Vector3 boxMax, Ray ray, out float tIn, out float tOut)
        {
            tIn = -float.MaxValue;
            tOut = float.MaxValue;
            float t0, t1;
            const float epsilon = 0.0000001f;

            // YZ plane.
            if (MathF.Abs(ray.dir.X) < epsilon)
            {
                // Ray parallel to plane.
                if (ray.pos.X < boxMin.X || ray.pos.X > boxMax.X)
                {
                    return false;
                }
            }

            // XZ plane.
            if (MathF.Abs(ray.dir.Y) < epsilon)
            {
                // Ray parallel to plane.
                if (ray.pos.Y < boxMin.Y || ray.pos.Y > boxMax.Y)
                {
                    return false;
                }
            }

            // XY plane.
            if (MathF.Abs(ray.dir.Z) < epsilon)
            {
                // Ray parallel to plane.
                if (ray.pos.Z < boxMin.Z || ray.pos.Z > boxMax.Z)
                {
                    return false;
                }
            }

            // YZ plane.
            t0 = (boxMin.X - ray.pos.X) / ray.dir.X;
            t1 = (boxMax.X - ray.pos.X) / ray.dir.X;

            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }

            if (t0 > tIn)
            {
                tIn = t0;
            }
            if (t1 < tOut)
            {
                tOut = t1;
            }

            if (tIn > tOut || tOut < 0)
            {
                return false;
            }

            // XZ plane.
            t0 = (boxMin.Y - ray.pos.Y) / ray.dir.Y;
            t1 = (boxMax.Y - ray.pos.Y) / ray.dir.Y;

            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }

            if (t0 > tIn)
            {
                tIn = t0;
            }
            if (t1 < tOut)
            {
                tOut = t1;
            }

            if (tIn > tOut || tOut < 0)
            {
                return false;
            }

            // XY plane.
            t0 = (boxMin.Z - ray.pos.Z) / ray.dir.Z;
            t1 = (boxMax.Z - ray.pos.Z) / ray.dir.Z;

            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }

            if (t0 > tIn)
            {
                tIn = t0;
            }
            if (t1 < tOut)
            {
                tOut = t1;
            }

            if (tIn > tOut || tOut < 0)
            {
                return false;
            }

            return true;
        }
    }

    public class Raycaster
    {
        const float height = 0.1f;
        MipArray mipArray;
        public Raycaster(MipArray _mipArray)
        {
            mipArray = _mipArray;
        }

        public bool Raycast(Ray ray, out Vector3 outT)
        {
            float t0, t1;
            bool intersected = RayUtils.IntersectAABoxRay(new Vector3(0, 0, 0), new Vector3(1, height, 1), ray, out t0, out t1);
            Vector3 hitpos = ray.AtT(t0);
            ray.pos = hitpos;

            return RaycastStep(ray, mipArray.MaxLod, new Vector2(0.5f, 0.5f), out outT);
        }
        bool RaycastStep(Ray ray, int lod, Vector2 pixelCenter, out Vector3 outT)
        {
            outT = new Vector3(-1, -1, -1);
            bool isHit = false;
            Mip mip = mipArray.mips[lod];
            Vector2 invscale = new Vector2(1.0f / mip.width, 1.0f / mip.height);
            Ray cr = ray;
            //cr.pos = ray.pos * new Vector3(mip.width, 1, mip.height);

            bool leftPlane = cr.dir.X < 0;
            bool backPlane = cr.dir.Z < 0;
            float epsilon = mip.width / 100.0f;
            float prevY = cr.pos.Y;
            while (!isHit)
            {
                Vector2 origPos = new Vector2(cr.pos.X, cr.pos.Z);
                float nextPlaneX = leftPlane ? MathF.Floor(cr.pos.X * mip.width - epsilon) :
                    MathF.Floor(cr.pos.X * mip.width + epsilon + 1);
                float nextPlaneY = backPlane ? MathF.Floor(cr.pos.Z * mip.height - epsilon) :
                    MathF.Floor(cr.pos.Z * mip.height + epsilon + 1);

                float it = RayUtils.IntersectXZPlane(cr, nextPlaneX * invscale.X, nextPlaneY * invscale.Y);
                Vector3 newpos = cr.AtT(it);
                float nx = MathF.Floor((origPos.X + newpos.X) * mip.width * 0.5f) + 0.5f;
                float ny = MathF.Floor((origPos.Y + newpos.Z) * mip.width * 0.5f) + 0.5f;
                Vector2 np = new Vector2(nx, ny) * invscale;
                if (MathF.Abs(np.X - pixelCenter.X) >= invscale.X)
                    break;
                if (MathF.Abs(np.Y - pixelCenter.Y) >= invscale.Y)
                    break;

                float v = mip.Sample(np.X, np.Y);
                if (prevY < v || newpos.Y < v)
                {
                    if (lod > 0)
                    {
                        Ray r = cr;
                        isHit = RaycastStep(r, lod - 1, np, out outT);
                    }
                    else
                    {
                        outT = new Vector3(np.X, np.Y, v);
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
