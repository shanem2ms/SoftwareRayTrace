using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
using System.Windows.Shapes;
using static System.Net.WebRequestMethods;

namespace SoftwareRayTrace
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap writeableBitmap;
        MipArray mipArray;
        int curLod = 0;

        public MainWindow()
        {
            InitializeComponent();

            writeableBitmap = new WriteableBitmap(
            (int)256,
            (int)256,
            96,
            96,
            PixelFormats.Bgr32,
            null);

            RenderOptions.SetBitmapScalingMode(this.img, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this.img, EdgeMode.Aliased);
            this.img.Source = writeableBitmap;

            img.Stretch = Stretch.Uniform;
            img.HorizontalAlignment = HorizontalAlignment.Left;
            img.VerticalAlignment = VerticalAlignment.Top;

            mipArray = LoadPng("na.png");
            for (int i = 0; i < mipArray.mips.Length; i++)
            {
                this.LODCb.Items.Add(i.ToString());
            }
            DrawTiles();
        }

        

        MipArray LoadPng(string f)
        {
            // Open a Stream and decode a PNG image
            Stream imageStreamSource = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read);
            PngBitmapDecoder decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            BitmapSource bitmapSource = decoder.Frames[0];
            byte[] pixels = new byte[(int)bitmapSource.Width * (int)bitmapSource.Height * 4];
            bitmapSource.CopyPixels(pixels, 256 * 4, 0);
            ulong maxVal = 0;
            ulong minVal = ulong.MaxValue;
            for (int i = 0; i < 256*256; ++i)
            {
                ulong b = pixels[i * 4];
                ulong g = pixels[i * 4 + 1];
                ulong r = pixels[i * 4 + 2];
                ulong eval = r * 256 * 256 + g * 256 + b;
                maxVal = Math.Max(maxVal, eval);
                minVal = Math.Min(minVal, eval);
            }
            ulong range = maxVal - minVal;
            byte []nrmVals = new byte[256 * 256 * 2];
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
            return new MipArray(new Mip { data = ptr, width = 256 });
        }


        void DrawTiles()
        {
            if (writeableBitmap == null)
                return;
            writeableBitmap.Lock();
            unsafe
            {
                // Get a pointer to the back buffer.
                nint pBackBuffer = writeableBitmap.BackBuffer;
                for (int y = 0; y < 256; y++)
                {
                    nint pRowPtr = pBackBuffer + y * writeableBitmap.BackBufferStride;
                    for (int x = 0; x < 256; x++)
                    {
                        ushort val = mipArray.SampleLod((x + 0.5f) / 256.0f , (y + 0.5f) / 256.0f, curLod);
                        int ival = (int)val >> 8;
                        // Compute the pixel's color.
                        int color_data = ival << 16; // R
                        color_data |= ival << 8;   // G
                        color_data |= ival << 0;   // B

                        *((int*)pRowPtr) = color_data;
                        pRowPtr += 4;
                    }
                }
            }
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, 256, 256));
            writeableBitmap.Unlock();
        }

        private void LODCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            curLod = int.Parse((string)this.LODCb.SelectedItem);
            DrawTiles();
        }
    }
}
