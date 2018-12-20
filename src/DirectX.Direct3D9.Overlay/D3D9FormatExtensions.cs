using System;
using System.Collections.Generic;
using System.Text;

namespace DirectX.Direct3D9.Overlay
{
    internal static class D3D9FormatExtensions
    {

        internal static System.Drawing.Imaging.PixelFormat ToPixelFormat(this SharpDX.Direct3D9.Format format)
        {
            switch (format)
            {
                case SharpDX.Direct3D9.Format.A8R8G8B8:
                case SharpDX.Direct3D9.Format.X8R8G8B8:
                    return System.Drawing.Imaging.PixelFormat.Format32bppArgb;
                case SharpDX.Direct3D9.Format.R5G6B5:
                    return System.Drawing.Imaging.PixelFormat.Format16bppRgb565;
                case SharpDX.Direct3D9.Format.A1R5G5B5:
                case SharpDX.Direct3D9.Format.X1R5G5B5:
                    return System.Drawing.Imaging.PixelFormat.Format16bppArgb1555;
                default:
                    return System.Drawing.Imaging.PixelFormat.Undefined;
            }
        }
    }
}
