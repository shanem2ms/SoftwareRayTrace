using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace SoftwareRayTrace
{
    struct Mip
    {
        public nint data;
        public int width;

        public unsafe ushort Sample(float u, float v)
        {
            int iu = (int)(u * width);
            int iv = (int)(v * width);
            return *((ushort *)(data + (iv * width + iu) * sizeof(ushort)));
        }
    }

    struct MipArray
    {
        public Mip[] mips;
        public MipArray(Mip basemip)
        {
            int nLevels = (int)Math.Log2(basemip.width) + 1;
            mips = new Mip[nLevels];
            mips[0] = basemip;
            for (int i = 1; i < nLevels; i++)
            {
                mips[i] = DownSample(mips[i - 1]);
            }
        }

        public ushort SampleLod(float u, float v, int lod)
        {
            return mips[lod].Sample(u, v);
        }
        unsafe Mip DownSample(Mip mip)
        {
            Mip mipD = new Mip();
            mipD.width = mip.width / 2;

            nint data = Marshal.AllocHGlobal(mipD.width * mipD.width * 2);
            for (int y = 0; y < mipD.width; ++y)
            {
                nint outRow = data + (y * mipD.width * sizeof(ushort));
                nint inRow0 = mip.data + (y * 2 * mip.width * sizeof(ushort));
                nint inRow1 = mip.data + ((y * 2 + 1) * mip.width * sizeof(ushort));
                for (int x = 0; x < mipD.width; ++x)
                {
                    ushort val00 = *((ushort*)(inRow0 + x * 2 * sizeof(ushort)));
                    ushort val01 = *((ushort*)(inRow0 + (x + 1) * 2 * sizeof(ushort)));
                    ushort val10 = *((ushort*)(inRow1 + x * 2 * sizeof(ushort)));
                    ushort val11 = *((ushort*)(inRow1 + (x + 1) * 2 * sizeof(ushort)));
                    *((ushort*)(outRow + x * sizeof(ushort))) = Math.Max(val00, Math.Max(val01, Math.Max(val10, val11)));
                }
            }
            mipD.data = data;
            return mipD;
        }
    }
}
