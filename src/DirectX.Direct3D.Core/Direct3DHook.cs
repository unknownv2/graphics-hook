using System.Collections.Generic;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core.Memory;

namespace DirectX.Direct3D.Core
{
    public abstract class Direct3DHook : DisposableComponent, IDirect3DHook
    {
        protected List<IOverlay> Overlays { get; set; }

        protected bool PendingUpdate;

        public abstract void CreateHooks();
    }
}
