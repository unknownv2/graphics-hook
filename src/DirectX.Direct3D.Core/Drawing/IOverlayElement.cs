using System;

namespace DirectX.Direct3D.Core.Drawing
{
    public interface IOverlayElement : ICloneable
    {
        bool Hidden { get; }

        void OnFrame();
    }
}
