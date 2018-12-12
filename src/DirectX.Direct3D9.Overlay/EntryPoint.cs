using System;
using CoreHook;
using DirectX.Direct3D.Core;
using SharpDX.Direct3D9;

namespace DirectX.Direct3D9.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        public EntryPoint(IContext context) { }

        public void Run(IContext context)
        {
            InitializeDeviceHook();
            while (true)
            {
                System.Threading.Thread.Sleep(30000);
            }
        }

        private Direct3DHook _direct3DHook;

        public void InitializeDeviceHook()
        {
            _direct3DHook = new Direct3DHookModule();
            _direct3DHook.CreateHooks();
        }


        private void DrawFramesPerSecond(Device device)
        {
            try
            {
             
                /*
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

                _framesPerSecondFont.DrawText(null, $"{_lastFrameRate:N0} FPS", 0, 0, new ColorBGRA(244, 66, 86, 255));*/
            }
            catch (Exception)
            {
            }
        }
    }
}
