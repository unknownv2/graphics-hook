﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using CoreHook;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using DirectX.Direct3D.Core;
namespace DirectX.Direct3D11.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        /// <summary>
        /// The IDXGISwapChain.Present function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int DXGISwapChain_PresentDelegate(IntPtr swapChain, int syncInterval, SharpDX.DXGI.PresentFlags flags);

        private IHook<DXGISwapChain_PresentDelegate> _d3DPresentHook;
        private List<IntPtr> _d3DDeviceFunctions = new List<IntPtr>();

        public EntryPoint(IContext context) { }
        public const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;
        SharpDX.Direct3D11.Device _device;
        SwapChain _swapChain;
        private DXFont _framesPerSecondFont;

        private int _frameCount;
        private int _lastTickCount;
        private float _lastFrameRate;


        public static SharpDX.DXGI.SwapChainDescription CreateSwapChainDescription(IntPtr windowHandle)
        {
            return new SharpDX.DXGI.SwapChainDescription
            {
                BufferCount = 1,
                Flags = SharpDX.DXGI.SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new SharpDX.DXGI.ModeDescription(100, 100, new Rational(60, 1), SharpDX.DXGI.Format.R8G8B8A8_UNorm),
                OutputHandle = windowHandle,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                SwapEffect = SharpDX.DXGI.SwapEffect.Discard,
                Usage = SharpDX.DXGI.Usage.RenderTargetOutput
            };
        }
        private Direct3DHook _direct3DHook;

        public void Run(IContext context)
        {
            _direct3DHook = new Direct3DHookModule();
            _direct3DHook.CreateHooks();
            /*
            var renderForm = new SharpDX.Windows.RenderForm();
            SharpDX.Direct3D11.Device.CreateWithSwapChain(
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

            _d3DPresentHook.Enabled = true;*/

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
            _frameCount++;

            DrawFramesPerSecond(swapChain);

            return _d3DPresentHook.Original(swapChainPtr, syncInterval, flags);
        }


        private void DrawFramesPerSecond(SwapChain swapChain)
        {
            try
            {
                var tickCount = Environment.TickCount;
                if (Math.Abs(tickCount - _lastTickCount) > 1000)
                {
                    _lastFrameRate = (float)_frameCount * 1000 / Math.Abs(tickCount - _lastTickCount);
                    _frameCount = 0;
                    _lastTickCount = tickCount;
                }

                var device = swapChain.GetDevice<Device>();
                var deviceContext = device.ImmediateContext;
                var spriteEngine = new DXSprite(device, deviceContext);
                if (!spriteEngine.Initialize())
                {
                    return;
                }
                var font = new DXFont(device, deviceContext);
                font.Initialize("Arial", 20, FontStyle.Bold, true);
                var color = System.Drawing.Color.FromArgb(244, 66, 86);
                spriteEngine.DrawString(25, 25, $"{_lastFrameRate:N0} FPS", color, font);
                /*
                if (_framesPerSecondFont == null)
                {
                    _framesPerSecondFont = new Font(device, new FontDescription
                    {
                        Height = 20,
                        FaceName = "Arial",
                        Italic = false,
                        Width = 0,
                        MipLevels = 1,
                        CharacterSet = FontCharacterSet.Default,
                        OutputPrecision = FontPrecision.Default,
                        Quality = FontQuality.ClearTypeNatural,
                        PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.DontCare,
                        Weight = FontWeight.Bold
                    });
                }

                _framesPerSecondFont.DrawText(null, $"{_lastFrameRate:N0} FPS", 0, 0, new ColorBGRA(244, 66, 86, 255));*/
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }
    }
}
