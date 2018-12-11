using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CoreHook;
using SharpDX;
using SharpDX.Direct3D9;

namespace DirectX.Direct3D9.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        private List<IntPtr> _d3DDeviceFunctions = new List<IntPtr>();
        private const int D3DDevice9FunctionCount = 119;

        private Font _framesPerSecondFont;

        private int _frameCount;
        private int _lastTickCount;
        private float _lastFrameRate;

        public EntryPoint(IContext context) { }

        public void Run(IContext context)
        {
            InitializeDeviceHook();
            while (true)
            {
                System.Threading.Thread.Sleep(30000);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        unsafe delegate int IDirect3DDevice9_PresentDelegate(IntPtr device, Rectangle* sourceRectangle,
            Rectangle* destRectangle, IntPtr destWindowOverride, IntPtr dirtyRegion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int IDirect3DDevice9_EndSceneDelegate(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int IDirect3DDevice9_ResetDelegate(IntPtr device, ref PresentParameters parameters);

        private IHook<IDirect3DDevice9_PresentDelegate> _d3DPresentHook;
        private IHook<IDirect3DDevice9_EndSceneDelegate> _d3DEndSceneHook;
        private IHook<IDirect3DDevice9_ResetDelegate> _d3dResetHook;

        public unsafe void InitializeDeviceHook()
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
                _d3DDeviceFunctions[(int)FunctionOrdinals.Present],
                Detour_Present,
                this);

            _d3dResetHook = HookFactory.CreateHook<IDirect3DDevice9_ResetDelegate>(
                _d3DDeviceFunctions[(int)FunctionOrdinals.Present],
                Detour_Reset,
                this);

            // Enable the hooks for all threads except the current thread.
            _d3DEndSceneHook.ThreadACL.SetExclusiveACL(new int[1]);
            _d3DPresentHook.ThreadACL.SetExclusiveACL(new int[1]);
            _d3dResetHook.ThreadACL.SetExclusiveACL(new int[1]);
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
            _frameCount++;

            return _d3DPresentHook.Original(device, sourceRectangle, destRectangle, destWindowOverride, dirtyRegion);
        }

        private int Detour_EndScene(IntPtr direct3DDevice)
        {
            Device device = (Device)direct3DDevice;

            DrawFramesPerSecond(device);

            device.EndScene();

            return Result.Ok.Code;
        }

        private int Detour_Reset(IntPtr direct3DDevice, ref PresentParameters parameters)
        {
            return _d3dResetHook.Original(direct3DDevice, ref parameters);
        }

        private void DrawFramesPerSecond(Device device)
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

                _framesPerSecondFont.DrawText(null, $"{_lastFrameRate:N0} FPS", 0, 0, new ColorBGRA(244, 66, 86, 255));
            }
            catch (Exception)
            {
            }
        }
    }
}
