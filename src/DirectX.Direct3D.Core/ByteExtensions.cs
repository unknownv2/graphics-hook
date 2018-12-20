using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DirectX.Direct3D.Core
{
    public static class ByteExtensions
    {
        public static Bitmap ToBitmap(this byte[] imageBytes)
        {
            MemoryStream ms = new MemoryStream(imageBytes);
            try
            {
                return (Bitmap)Image.FromStream(ms);
            }
            catch
            {
                return null;
            }
        }

        public static Bitmap ToBitmap(this byte[] imageBytes, int imageWidth, int imageHeight, int imageStride,
            System.Drawing.Imaging.PixelFormat pixelFormat)
        {

            GCHandle imageHandle = GCHandle.Alloc(imageBytes, GCHandleType.Pinned);
            try
            {
                return new Bitmap(imageWidth, imageHeight, imageStride, pixelFormat, imageHandle.AddrOfPinnedObject());
            }
            finally
            {
                if (imageHandle.IsAllocated)
                {
                    imageHandle.Free();
                }
            }
        }
    }
}
