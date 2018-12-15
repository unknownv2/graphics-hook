using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using CoreHook;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace DirectX.Direct3D11.Overlay
{
    internal class Direct3DHookModule : Direct3DHook
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int DXGISwapChain_PresentDelegate(IntPtr swapChain, int syncInterval, PresentFlags flags);

        private IHook<DXGISwapChain_PresentDelegate> _d3DPresentHook;
        private List<IntPtr> _d3DDeviceFunctions = new List<IntPtr>();
        private OverlayRenderer _overlayRenderer;

        public const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;
        Device _device;
        SwapChain _swapChain;
        private DXFont _framesPerSecondFont;

        private int _frameCount;
        private int _lastTickCount;
        private float _lastFrameRate;

        private IntPtr _swapChainPtr;

        private static SwapChainDescription CreateSwapChainDescription(IntPtr windowHandle)
        {
            return new SwapChainDescription
            {
                BufferCount = 1,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                OutputHandle = windowHandle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };
        }

        public override void CreateHooks()
        {
            var renderForm = new SharpDX.Windows.RenderForm();
            Device.CreateWithSwapChain(
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                CreateSwapChainDescription(renderForm.Handle),
                out _device,
                out _swapChain);
            //
            if (_swapChain != null)
            {
                _d3DDeviceFunctions.AddRange(ReadVTableAddresses(_swapChain.NativePointer, DXGI_SWAPCHAIN_METHOD_COUNT));
            }

            _d3DPresentHook = HookFactory.CreateHook<DXGISwapChain_PresentDelegate>(
                _d3DDeviceFunctions[(int)FunctionOrdinals.Present],
                Detour_Present,
                this);

            Overlays = new List<IOverlay>
            {
                //var font = new System.Drawing.Font("Arial", 16, FontStyle.Bold);
                // Add the Frames Per Second overlay
                new Direct3D.Core.Drawing.Overlay
                {
                    Elements =
                    {
                        new FramesPerSecondOverlay(new Font("Arial", 16, FontStyle.Bold))
                        {
                            Location = new Point(25, 25),
                            Color = Color.Red,
                            AntiAliased = true,
                            Text = "{0:N0} FPS"
                        }
                    },
                    Hidden = false
                }
            };

            _d3DPresentHook.Enabled = true;
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

        private int Detour_Present(IntPtr swapChainPtr, int syncInterval, SharpDX.DXGI.PresentFlags flags)
        {
            SwapChain swapChain = (SwapChain)swapChainPtr;

            DrawFramesPerSecond(swapChain);

            return _d3DPresentHook.Original(swapChainPtr, syncInterval, flags);
        }

        private void DrawFramesPerSecond(SwapChain swapChain)
        {
            Capture(swapChain);
        }

        private void CalculateFps()
        {
            _frameCount++;
            var tickCount = Environment.TickCount;
            if (Math.Abs(tickCount - _lastTickCount) > 1000)
            {
                _lastFrameRate = (float)_frameCount * 1000 / Math.Abs(tickCount - _lastTickCount);
                _frameCount = 0;
                _lastTickCount = tickCount;
            }
        }

        private void Capture(SwapChain swapChain)
        {
            CalculateFps();
            
            try
            {
                // Draw overlays
                var displayOverlays = Overlays;
                
                if (_overlayRenderer == null ||
                    _swapChainPtr != swapChain.NativePointer ||
                    PendingUpdate)
                {
                    
                    if (_overlayRenderer != null)
                    {
                        _overlayRenderer.Dispose();
                    }

                    _swapChainPtr = swapChain.NativePointer;

                    _overlayRenderer = ToDispose((new OverlayRenderer()));
                    _overlayRenderer.Overlays.AddRange(displayOverlays);
                    _overlayRenderer.Initialize(swapChain);
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
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }
    }
}
