using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Windows.Media;

namespace SoftwareRayTrace
{
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
        public static Vector3 IntersectXYPlance(Ray r, float xplane, float yplane)
        {
            float slope = r.dir.X / r.dir.Y;
            float tXPlane = (xplane - r.pos.X) / r.dir.X;
            float tYPlane = (yplane - r.pos.Y) / r.dir.Y;
            return r.AtT(Math.Min(tXPlane, tYPlane));
        }
    }
}
