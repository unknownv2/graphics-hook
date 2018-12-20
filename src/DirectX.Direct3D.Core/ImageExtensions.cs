using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace DirectX.Direct3D.Core
{
    public static class ImageExtensions
    {
        public static byte[] ToArray(this Image image, System.Drawing.Imaging.ImageFormat imageFormat)
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream, imageFormat);
                stream.Close();
                return stream.ToArray();
            }
        }
    }
}
