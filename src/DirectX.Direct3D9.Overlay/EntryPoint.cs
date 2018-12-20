using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using CoreHook;
using Direct3DCapture;
using DirectX.Direct3D.Core;
using JsonRpc.Standard;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace DirectX.Direct3D9.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        private Direct3DHookModule _direct3DHook;
        private string _pipeName;

        public EntryPoint(IContext context, string arg1) { }

        public void Run(IContext context,string pipeName)
        {
            _pipeName = pipeName;

            InitializeDeviceHook();
            while (true)
            {
                System.Threading.Thread.Sleep(30000);
            }
        }

        public void InitializeDeviceHook()
        {
            _direct3DHook = new Direct3DHookModule();
            ScreenshotServer.CreateRpcService(
                _pipeName,
                new PipeFactory(),
                typeof(ScreenCaptureService),
                async (context, next) =>
                {
                    System.Diagnostics.Debug.WriteLine("> {0}", context.Request);
                    await next();
                    System.Diagnostics.Debug.WriteLine("< {0}", context.Response);
                },
                _direct3DHook);
            _direct3DHook.CreateHooks();
        }
    }

    public class ScreenCaptureService : JsonRpcService
    {
        private ScreenshotSessionFeature Session => RequestContext.Features.Get<ScreenshotSessionFeature>();

        [JsonRpcMethod]
        public SurfaceCaptureResponse RequestScreenshot(CaptureRequest request)
        {
            // Since we have a new request, reset the completion flag
            ScreenshotRequest.Completed = false;
            // Set the new request flag
            ScreenshotRequest.Requested = true;
            // Reset capture data buffer
            ScreenshotRequest.Data = null;
            // Ensure the response data is reset
            ScreenshotRequest.Response = null;
            // Set the capture request information
            ScreenshotRequest.Request = request;

            // Wait for the buffer to be filled and the completion signal to be set.
            while (!ScreenshotRequest.Completed || ScreenshotRequest.Response == null)
            {
                Thread.Sleep(5000);
            }

            // Reset the capture request flag since the request has been fulfilled
            ScreenshotRequest.Requested = false;

            //return ScreenshotRequest.Response.Data;
            var response = ScreenshotRequest.Response;
            //return new SurfaceCaptureResponse() {Data = Convert.ToBase64String(response.Data)};
            return new SurfaceCaptureResponse { Data = response.Data};
            //return new SurfaceCapture {Data = response.Data, ImageFormat = response.ImageFormat, PixelFormat = response.PixelFormat, Width = response.Width, Height = response .Height, Stride = response.Stride};
        }
    }

}
