using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoftwareRayTrace
{
    internal class Draw
    {
        public WriteableBitmap writeableBitmap;

        public Draw()
        {
            writeableBitmap = new WriteableBitmap(
            (int)256,
            (int)256,
            96,
            96,
            PixelFormats.Bgr32,
            null);
        }

        
       

        public static int RGBToI(byte R, byte G, byte B)
        {
            int color_data = R << 16; // R
            color_data |= G << 8;   // G
            color_data |= B << 0;   // B

            return color_data;
        }

        public void Begin()
        {
            writeableBitmap.Lock();
        }

        public void End()
        {
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, 256, 256));
            writeableBitmap.Unlock();
        }

        public unsafe void DrawTiles(MipArray mipArray, int curLod)
        {
            // Get a pointer to the back buffer.
            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = 0; y < 256; y++)
            {
                nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                for (int x = 0; x < 256; x++)
                {
                    float val = mipArray.SampleLod((x + 0.5f) / 256.0f, (y + 0.5f) / 256.0f, curLod);
                    *((int*)pRowPtr) = RGBToI((byte)(val * 255.0f), (byte)(val * 255.0f), (byte)(val * 255.0f));
                    pRowPtr += 4;
                }
            }
        }
        public unsafe void DrawPoint(Vector2 pos, int color)
        {
            int ymid = (int)(pos.Y * 256);
            int xmid = (int)(pos.X * 256);

            nint pBackBuffer = writeableBitmap.BackBuffer;
            for (int y = ymid - 1; y <= ymid + 1; ++y)
            {
                for (int x = xmid - 1; x <= xmid +1; ++x)
                {
                    if (x < 0 || x >= 256 || y < 0 || y >= 256)
                        continue;
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                    nint pCur = pRowPtr + x * 4;
                    *((int*)pCur) = color;
                }
            }
        }

        public unsafe void DrawLine(Vector2 start, Vector2 end, int color)
        {
            nint pBackBuffer = writeableBitmap.BackBuffer;
            Vector2 dir = end - start;
            if (Math.Abs(dir.X) > Math.Abs(dir.Y))
            {
                int yStart = (int)(start.Y * 256);
                int xStart = (int)(start.X * 256);
                int yEnd = (int)(end.Y * 256);
                int xEnd = (int)(end.X * 256);
                if (yStart > yEnd)
                {
                    int tmp = xStart;
                    xStart = xEnd;
                    xEnd = tmp;
                    tmp = yStart;
                    yStart = yEnd;
                    yEnd = tmp;
                }

                bool ishorizontal = dir.Y == 0;
                float slope = dir.X / dir.Y;
                for (int y = yStart; y <= yEnd; ++y)
                {
                    int xs = ishorizontal ? xStart : (int)((y - yStart) * slope) + xStart;
                    int xe = ishorizontal ? xEnd : (int)((y - yStart + 1) * slope) + xStart;
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                    for (int x = xs; x < xe; ++x)
                    {
                        if (y < 0 || y > 255 || x < 0 || x > 255)
                            continue;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }
            }
            else
            {
                int yStart = (int)(start.Y * 256);
                int yEnd = (int)(end.Y * 256);
                int xStart = (int)(start.X * 256);
                int xEnd = (int)(end.X * 256);
                float slope = dir.Y / dir.X;
                if (xStart > xEnd)
                {
                    int tmp = xStart;
                    xStart = xEnd;
                    xEnd = tmp;
                    tmp = yStart;
                    yStart = yEnd;
                    yEnd = tmp;
                }
                bool isvertical = dir.X == 0;
                for (int x = xStart; x <= xEnd; ++x)
                {
                    int ys = isvertical ? Math.Min(yStart, yEnd) : (int)((x - xStart) * slope) + yStart;
                    int ye = isvertical ? Math.Max(yEnd, yStart) : (int)((x - xStart + 1) * slope) + yStart;
                    for (int y = ys; y < ye; ++y)
                    {
                        if (y < 0 || y > 255 || x < 0 || x > 255)
                            continue;
                        nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                        nint pCur = pRowPtr + x * 4;
                        *((int*)pCur) = color;
                    }
                }

            }
        }
    }
}
