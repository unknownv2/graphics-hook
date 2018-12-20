using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace DirectX.Direct3D.Core
{
 
    public class SurfaceCapture
    {
        public byte[] Data;
        public ImageFormat ImageFormat { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public PixelFormat PixelFormat { get; set; }
    }

    public class SurfaceCaptureResponse
    {
        public byte[] Data { get; set; }
    }

    public static class SurfaceCaptureExtensions
    {
        public static Bitmap ToBitmap(this SurfaceCapture surfaceCapture)
        {
            return surfaceCapture.ImageFormat == ImageFormat.PixelData ? 
                surfaceCapture.Data.ToBitmap(surfaceCapture.Width, surfaceCapture.Height, surfaceCapture.Stride, surfaceCapture.PixelFormat)
                : surfaceCapture.Data.ToBitmap();
        }
    }
}
