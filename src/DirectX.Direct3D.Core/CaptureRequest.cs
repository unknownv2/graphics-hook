using System;
using System.Drawing;

namespace DirectX.Direct3D.Core
{
    [Serializable]
    public  class CaptureRequest
    {
        public Rectangle Region;
        public Size? RegionSize;
        public ImageFormat ImageFormat;

        public CaptureRequest(Rectangle region, Size? size)
        {
            Region = region;
            RegionSize = size;
        }

        public CaptureRequest Clone()
        {
            return new CaptureRequest(Region, RegionSize);
        }
    }

    public enum ImageFormat
    {
        Bitmap,
        Jpeg,
        Png,
        PixelData
    }

}
