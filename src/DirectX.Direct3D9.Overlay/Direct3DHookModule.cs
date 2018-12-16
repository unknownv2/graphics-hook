using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using DirectX.Direct3D.Core;
using CoreHook;
using DirectX.Direct3D.Core.Drawing;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Font = SharpDX.Direct3D9.Font;
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

            return _d3DResetHook.Original(direct3DDevice, ref parameters);
        }

        private void DrawFramesPerSecond(Device device)
        {
            Capture(device);
        }

        private void Capture(Device device)
        {
            try
            {
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

    }
}
