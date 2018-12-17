using System.Collections.Generic;

namespace DirectX.Direct3D.Core.Drawing
{
    public interface IOverlay : IOverlayElement
    {
        List<IOverlayElement> Elements { get; }
    }
}
