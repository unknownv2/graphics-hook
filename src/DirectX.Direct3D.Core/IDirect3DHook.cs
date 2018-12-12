using System;
using System.Collections.Generic;
using System.Text;

namespace DirectX.Direct3D.Core
{
    public interface IDirect3DHook : IDisposable
    {
        void CreateHooks();
    }
}
