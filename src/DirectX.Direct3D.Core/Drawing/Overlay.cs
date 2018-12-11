using System;
using System.Collections.Generic;

namespace DirectX.Direct3D.Core.Drawing
{
    [Serializable]
    public class Overlay : IOverlay
    {
        public List<IOverlayElement> Elements { get; } = new List<IOverlayElement>();

        public bool Hidden { get; set; }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public void OnFrame()
        {
            foreach (var overlayElement in Elements)
            {
                overlayElement.OnFrame();
            }
        }
    }
}
