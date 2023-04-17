using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SoftwareRayTrace
{
    public struct Mip
    {
        public nint data;
        public int width;
        public int height;
        const float mult = 1.0f / (float)ushort.MaxValue;

        public unsafe float Sample(float u, float v)
        {
            int iu = (int)(u * width);
            int iv = (int)(v * height);
            ushort sval = *((ushort *)(data + (iv * width + iu) * sizeof(ushort)));
            return (float)sval * mult;
        }
    }

    public struct MipArray
    {
        public Mip[] mips;

        public int MaxLod => mips.Length - 1;
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

        public static MipArray LoadPng(string f)
        {
            // Open a Stream and decode a PNG image
            Stream imageStreamSource = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
            PngBitmapDecoder decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            BitmapSource bitmapSource = decoder.Frames[0];
            byte[] pixels = new byte[(int)bitmapSource.Width * (int)bitmapSource.Height * 4];
            bitmapSource.CopyPixels(pixels, 256 * 4, 0);
            ulong maxVal = 0;
            ulong minVal = ulong.MaxValue;
            for (int i = 0; i < 256 * 256; ++i)
            {
                ulong b = pixels[i * 4];
                ulong g = pixels[i * 4 + 1];
                ulong r = pixels[i * 4 + 2];
                ulong eval = r * 256 * 256 + g * 256 + b;
                maxVal = Math.Max(maxVal, eval);
                minVal = Math.Min(minVal, eval);
            }
            ulong range = maxVal - minVal;
            byte[] nrmVals = new byte[256 * 256 * 2];
            for (int i = 0; i < 256 * 256; ++i)
            {
                ulong b = pixels[i * 4];
                ulong g = pixels[i * 4 + 1];
                ulong r = pixels[i * 4 + 2];
                ulong eval = r * 256 * 256 + g * 256 + b;
                ushort nrmval = (ushort)(((eval - minVal) * ushort.MaxValue) / range);
                nrmVals[i * 2] = (byte)(nrmval & 0xFF);
                nrmVals[i * 2 + 1] = (byte)(nrmval >> 8);
            }
            nint ptr = Marshal.AllocHGlobal(nrmVals.Length);
            Marshal.Copy(nrmVals, 0, ptr, nrmVals.Length);
            return new MipArray(new Mip { data = ptr, width = 256, height = 256 });
        }
        public float SampleLod(float u, float v, int lod)
        {
            return mips[lod].Sample(u, v);
        }
        unsafe Mip DownSample(Mip mip)
        {
            Mip mipD = new Mip();
            mipD.width = mip.width / 2;
            mipD.height = mip.height / 2;

            nint data = Marshal.AllocHGlobal(mipD.width * mipD.height * 2);
            for (int y = 0; y < mipD.height; ++y)
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
