using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using JsonRpc.Standard.Server;
using JsonRpc.AspNetCore;
using JsonRpc.Standard.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Direct3DCapture;
using DirectX.Direct3D.Core;
using System.Threading;

namespace DirectX.Direct3D11.Overlay
{
    public class ScreenshotAspServer
    {
        public ScreenshotAspServer()
        {
            var host = new WebHostBuilder()
                  .UseKestrel()
                  .UseContentRoot(Directory.GetCurrentDirectory())
                  .UseIISIntegration()
                  .UseStartup<Startup>()
                  //.UseApplicationInsights()
                  .Build();

            host.Run();
        }
    }
    public class ValuesService : JsonRpcService
    {
        private readonly ILogger logger;

        public ValuesService(ILoggerFactory loggerFactory)
        {
            // Inject loggerFactory from constructor.
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            logger = loggerFactory.CreateLogger<ValuesService>();
        }

        [JsonRpcMethod]
        public object GetValue()
        {
            return new[] { "value1", "value2" };
        }

        [JsonRpcMethod]
        public object GetValue(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Id should be a non-negative integer.");
            return "value of " + id;
        }

        [JsonRpcMethod(IsNotification = true)]
        public void Notify()
        {
            var session = RequestContext.GetHttpContext().Session;
            var ct = session.GetInt32("counter") ?? 0;
            ct++;
            session.SetInt32("counter", ct);
            logger.LogInformation("Counter increased: {counter}.", ct);
        }

        [JsonRpcMethod]
        public int GetCounter()
        {
            return RequestContext.GetHttpContext().Session.GetInt32("counter") ?? -1;
        }

        [JsonRpcMethod]
        public byte[] GetBytesValue()
        {
            return new byte[] {1, 2, 3, 4, 5};
        }
        [JsonRpcMethod]
        public SurfaceCaptureResponse RequestScreenshot2(CaptureRequest request)
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

            ScreenshotRequest.Requested = false;
            //return ScreenshotRequest.Response.Data;
            var response = ScreenshotRequest.Response;
            //return new SurfaceCaptureResponse() {Data = Convert.ToBase64String(response.Data)};
            return new SurfaceCaptureResponse { Data = response.Data };
            //return new SurfaceCapture {Data = response.Data, ImageFormat = response.ImageFormat, PixelFormat = response.PixelFormat, Width = response.Width, Height = response .Height, Stride = response.Stride};
        }
    }
}
