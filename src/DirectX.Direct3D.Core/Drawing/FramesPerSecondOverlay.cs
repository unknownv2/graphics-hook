using System;
using System.Drawing;

namespace DirectX.Direct3D.Core.Drawing
{
    public class FramesPerSecondOverlay : TextOverlay
    {
        private string _fpsText = "{0:N0} FPS";

        private int _frameCount;
        private int _lastTickCount;
        private float _lastFrameRate;
        public override string Text
        {
            get => string.Format(_fpsText, GetFramesPerSecond());
            set => _fpsText = value;
        }
        public FramesPerSecondOverlay(Font font) : base(font)
        {

        }

        public override void OnFrame()
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

        public float GetFramesPerSecond()
        {
            return _lastFrameRate;
        }
    }
}
