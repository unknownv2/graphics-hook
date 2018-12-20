using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DirectX.Direct3D.Core;
using CoreHook;
using DirectX.Direct3D.Core.Drawing;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using ImageFormat = DirectX.Direct3D.Core.ImageFormat;
using Rectangle = SharpDX.Rectangle;

namespace DirectX.Direct3D9.Overlay
{
    internal class Direct3DHookModule : Direct3DHook
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        unsafe delegate int IDirect3DDevice9_PresentDelegate(IntPtr device, Rectangle* sourceRectangle,
            Rectangle* destRectangle, IntPtr destWindowOverride, IntPtr dirtyRegion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int IDirect3DDevice9_EndSceneDelegate(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int IDirect3DDevice9_ResetDelegate(IntPtr device, ref PresentParameters parameters);

        private IHook<IDirect3DDevice9_PresentDelegate> _d3DPresentHook;
        private IHook<IDirect3DDevice9_EndSceneDelegate> _d3DEndSceneHook;
        private IHook<IDirect3DDevice9_ResetDelegate> _d3DResetHook;

        private OverlayRenderer _overlayRenderer;
        private List<IntPtr> _d3DDeviceFunctions = new List<IntPtr>();
        private const int D3DDevice9FunctionCount = 119;
        private bool _isUsingPresentHook = false;

        public override unsafe void CreateHooks()
        {
            _d3DDeviceFunctions = new List<IntPtr>();

            using (var direct3D = new SharpDX.Direct3D9.Direct3D())
            {
                using (var device = new Device(direct3D, 0, DeviceType.NullReference, IntPtr.Zero,
                    CreateFlags.HardwareVertexProcessing,
                    new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = IntPtr.Zero }))
                {
                    _d3DDeviceFunctions.AddRange(ReadVTableAddresses(device.NativePointer, D3DDevice9FunctionCount));
                }
            }

            // Create the hooks for our target Direct3D Device functions.
            _d3DEndSceneHook = HookFactory.CreateHook<IDirect3DDevice9_EndSceneDelegate>(
                _d3DDeviceFunctions[(int)FunctionOrdinals.EndScene],
                Detour_EndScene,
                this);

            _d3DPresentHook = HookFactory.CreateHook<IDirect3DDevice9_PresentDelegate>(
                    _d3DDeviceFunctions[(int) FunctionOrdinals.Present],
                    Detour_Present,
                    this);

            _d3DResetHook = HookFactory.CreateHook<IDirect3DDevice9_ResetDelegate>(
                _d3DDeviceFunctions[(int)FunctionOrdinals.Reset],
                Detour_Reset,
                this);

            // Add the Frames Per Second overlay.
            Overlays = new List<IOverlay>
            {
                new Direct3D.Core.Drawing.Overlay
                {
                    Elements =
                    {
                        new FramesPerSecondOverlay(new System.Drawing.Font("Arial", 16, FontStyle.Bold))
                        {
                            Location = new System.Drawing.Point(25, 25),
                            Color = Color.Red,
                            AntiAliased = true,
                            Text = "{0:N0} FPS"
                        }
                    },
                    Hidden = false
                }
            };

            // Enable the hooks for all threads except the current thread.
            _d3DEndSceneHook.ThreadACL.SetExclusiveACL(new int[1]);
            _d3DPresentHook.ThreadACL.SetExclusiveACL(new int[1]);
            _d3DResetHook.ThreadACL.SetExclusiveACL(new int[1]);
        }

        private static IEnumerable<IntPtr> ReadVTableAddresses(IntPtr vTableAddress, int vTableFunctionCount)
        {
            IntPtr[] addresses = new IntPtr[vTableFunctionCount];
            IntPtr vTable = Marshal.ReadIntPtr(vTableAddress);
            for (var i = 0; i < vTableFunctionCount; ++i)
            {
                addresses[i] = Marshal.ReadIntPtr(vTable, i * IntPtr.Size);
            }
            return addresses;
        }

        private unsafe int Detour_Present(
            IntPtr device,
            Rectangle* sourceRectangle,
            Rectangle* destRectangle,
            IntPtr destWindowOverride,
            IntPtr dirtyRegion)
        {
            _isUsingPresentHook = true;

            Device d3Device = (Device)device;
            DrawFramesPerSecond(d3Device);

            return _d3DPresentHook.Original(device, sourceRectangle, destRectangle, destWindowOverride, dirtyRegion);
        }

        private int Detour_EndScene(IntPtr direct3DDevice)
        {
            Device device = (Device)direct3DDevice;

            if (!_isUsingPresentHook)
            {
                DrawFramesPerSecond(device);
            }

            device.EndScene();

            return Result.Ok.Code;
        }

        private int Detour_Reset(IntPtr direct3DDevice, ref PresentParameters parameters)
        {
            _overlayRenderer?.ResetDeviceResources();

            CleanupSurfaceResources();

            return _d3DResetHook.Original(direct3DDevice, ref parameters);
        }

        private void DrawFramesPerSecond(Device device)
        {
            Capture(device);
        }

        private Query _captureQuery;
        private CaptureRequest _captureRequestCopy;
        private bool _captureRequested;
        private Surface _renderTargetSurface;
        private bool _renderTargetSurfaceLocked;

        private Surface _resolvingTargetSurface;
        private readonly object _renderSurfaceLock = new object();

        private void Capture(Device device)
        {
            try
            {
                // Check if the capture request for the render target has completed
                if (_captureRequested && _captureRequestCopy != null &&
                    _captureQuery.GetData(out bool _, false))
                {
                    _captureRequested = false;

                    var lockedRectangle = LockSurfaceForCopy(_renderTargetSurface, out SharpDX.Rectangle rectangle);
                    _renderTargetSurfaceLocked = true;

                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        lock (_renderSurfaceLock)
                        {
                            CaptureSurfaceData(lockedRectangle.DataPointer, lockedRectangle.Pitch, rectangle.Width,
                                rectangle.Height, _renderTargetSurface.Description.Format.ToPixelFormat(),
                                _captureRequestCopy);
                        }
                    });
                }

                if (ScreenshotRequest.Request != null)
                {
                    try
                    {
                        var captureRequest = ScreenshotRequest.Request;
                        using (Surface surfaceTarget = device.GetRenderTarget(0))
                        {

                            int width, height;

                            if (captureRequest.RegionSize != null
                                && (surfaceTarget.Description.Width > captureRequest.RegionSize.Value.Width
                                    || surfaceTarget.Description.Height > captureRequest.RegionSize.Value.Height))
                            {
                                if (surfaceTarget.Description.Width > captureRequest.RegionSize.Value.Width)
                                {
                                    width = captureRequest.RegionSize.Value.Width;
                                    height = (int) Math.Round(
                                        (surfaceTarget.Description.Height *
                                         ((double) captureRequest.RegionSize.Value.Width /
                                          (double) surfaceTarget.Description.Width)));
                                }
                                else
                                {
                                    height = captureRequest.RegionSize.Value.Height;
                                    width = (int) Math.Round((surfaceTarget.Description.Width *
                                                              (double) captureRequest.RegionSize.Value.Height /
                                                               (double) surfaceTarget.Description.Height));
                                }
                            }
                            else
                            {
                                width = surfaceTarget.Description.Width;
                                height = surfaceTarget.Description.Height;
                            }

                            if (_renderTargetSurface != null &&
                                (_renderTargetSurface.Description.Width != width
                                 || _renderTargetSurface.Description.Height != height
                                 || _renderTargetSurface.Description.Format != surfaceTarget.Description.Format))
                            {
                                CleanupSurfaceResources();
                            }

                            if (!_captureResourcesInitialized || _renderTargetSurface == null)
                            {
                                InitializeCaptureResources(device, surfaceTarget.Description.Format, width, height);
                            }

                            device.StretchRectangle(surfaceTarget, _resolvingTargetSurface, TextureFilter.None);
                        }

                        if (_renderTargetSurfaceLocked)
                        {
                            lock (_renderSurfaceLock)
                            {
                                if (_renderTargetSurfaceLocked)
                                {
                                    _renderTargetSurface.UnlockRectangle();
                                    _renderTargetSurfaceLocked = false;
                                }
                            }
                        }

                         device.GetRenderTargetData(_resolvingTargetSurface, _renderTargetSurface);

                        _captureRequestCopy = ScreenshotRequest.Request.Clone();
                        _captureQuery.Issue(Issue.End);
                        _captureRequested = true;
                    }
                    finally
                    {
                        // Mark the request as null so it is not processed again
                        ScreenshotRequest.Request = null;
                    }
                }

                // Draw any overlays that have been added to the global list.
                var displayOverlays = Overlays;
                if (_overlayRenderer == null ||
                    _overlayRenderer.Device.NativePointer != device.NativePointer ||
                    PendingUpdate)
                {
                    if (_overlayRenderer != null)
                    {
                        RemoveAndDispose(ref _overlayRenderer);
                    }

                    _overlayRenderer = ToDispose((new OverlayRenderer()));
                    _overlayRenderer.Overlays.AddRange(displayOverlays);
                    _overlayRenderer.Initialize(device);

                    PendingUpdate = false;
                }

                if (_overlayRenderer != null)
                {
                    foreach (var overlay in _overlayRenderer.Overlays)
                    {
                        overlay.OnFrame();
                    }

                    _overlayRenderer.DrawFrame();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{e}");
            }
        }

        private SharpDX.DataRectangle LockSurfaceForCopy(Surface surface, out SharpDX.Rectangle rectangle)
        {
            if (_captureRequestCopy.Region.Height > 0 && _captureRequestCopy.Region.Width > 0)
            {
                rectangle = new Rectangle(_captureRequestCopy.Region.Left, _captureRequestCopy.Region.Top,
                    _captureRequestCopy.Region.Width, _captureRequestCopy.Region.Height);
            }
            else
            {
                rectangle = new SharpDX.Rectangle(0, 0, surface.Description.Width, surface.Description.Height);
            }

            return surface.LockRectangle(rectangle, LockFlags.ReadOnly);
        }

        private bool _captureResourcesInitialized;

        private void InitializeCaptureResources(Device device, Format dataFormat, int width, int height)
        {
            if (!_captureResourcesInitialized)
            {
                _captureResourcesInitialized = true;

                _renderTargetSurface =
                    ToDispose(Surface.CreateOffscreenPlain(device, width, height, dataFormat, Pool.SystemMemory));

                _resolvingTargetSurface = ToDispose(Surface.CreateRenderTarget(device, width, height, dataFormat,
                    MultisampleType.None, 0, false));

                _captureQuery = ToDispose(new Query(device, QueryType.Event));
            }
        }



        private void CleanupSurfaceResources()
        {
            lock (_renderSurfaceLock)
            {
                _captureResourcesInitialized = false;

                RemoveAndDispose(ref _renderTargetSurface);
                _renderTargetSurfaceLocked = false;

                RemoveAndDispose(ref _resolvingTargetSurface);
                RemoveAndDispose(ref _captureQuery);

                _captureRequested = false;

                RemoveAndDispose(ref _overlayRenderer);
            }
        }
    }
}
