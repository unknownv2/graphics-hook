using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core.Memory;
using System.IO;

namespace DirectX.Direct3D.Core
{
    public abstract class Direct3DHook : DisposableComponent, IDirect3DHook
    {
        protected List<IOverlay> Overlays { get; set; }

        protected bool PendingUpdate;

        public abstract void CreateHooks();

        protected static byte[] StreamToArray(Stream stream)
        {
            if(stream is MemoryStream ms)
            {
                return ms.ToArray();
            }
            else
            {
                byte[] streamBuffer = new byte[32768];
                using (var memoryStream = new MemoryStream())
                {
                    while(true)
                    {
                        int read = memoryStream.Read(streamBuffer, 0, streamBuffer.Length);
                        if(read > 0)
                        {
                            memoryStream.Write(streamBuffer, 0, read);
                        }
                        if(read < streamBuffer.Length)
                        {
                            return memoryStream.ToArray();
                        }
                    }
                }
            }
        }

        protected void CaptureSurfaceData(Stream surfaceStream, CaptureRequest captureRequest)
        {
            CaptureSurfaceData(StreamToArray(surfaceStream), captureRequest);
        }

        protected void CaptureSurfaceData(byte[] bitmapData, CaptureRequest captureRequest)
        {
            try
            {
                if(captureRequest != null)
                {
                    var surfaceCapture = new SurfaceCapture
                    {
                        Data = bitmapData,
                        ImageFormat = captureRequest.ImageFormat
                    };

                    CompleteCaptureRequest(surfaceCapture);
                }
            }
            catch(Exception e)
            {

            }
        }

        protected void CaptureSurfaceData(IntPtr captureData, int pitch, int width, int height, PixelFormat pixelFormat, CaptureRequest captureRequest)
        {
            if (captureRequest == null || pixelFormat == PixelFormat.Undefined)
            {
                return;
            }

            int bufferSize = height * pitch;
            var captureBuffer = new byte[bufferSize];
            Marshal.Copy(captureData, captureBuffer, 0, bufferSize);

            SurfaceCapture surfaceCapture = null;

            if (captureRequest.ImageFormat == ImageFormat.PixelData)
            {
                surfaceCapture = new SurfaceCapture
                {
                    Data = captureBuffer,
                    PixelFormat = pixelFormat,
                    Height = height,
                    Width = width,
                    Stride = pitch
                };
            }
            else
            {
                using (var bitmap = captureBuffer.ToBitmap(width, height, pitch, pixelFormat))
                {
                    var imageFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                    switch (captureRequest.ImageFormat)
                    {
                        case ImageFormat.Jpeg:
                            imageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                            break;
                        case ImageFormat.Png:
                            imageFormat = System.Drawing.Imaging.ImageFormat.Png;
                            break;
                    }

                    surfaceCapture = new SurfaceCapture
                    {
                        Data = bitmap.ToArray(imageFormat),
                        ImageFormat = captureRequest.ImageFormat,
                        Height = bitmap.Height,
                        Width = bitmap.Width
                    };
                }
            }
            CompleteCaptureRequest(surfaceCapture);
        }

        private void CompleteCaptureRequest(SurfaceCapture response)
        {
            ScreenshotRequest.Completed = true;
            ScreenshotRequest.Response = response;
        }
    }
}
