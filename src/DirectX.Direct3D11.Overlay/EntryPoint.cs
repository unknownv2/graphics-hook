using CoreHook;
using DirectX.Direct3D.Core;

namespace DirectX.Direct3D11.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        private Direct3DHook _direct3DHook;

        public EntryPoint(IContext context) { }

        public void Run(IContext context)
        {
            _direct3DHook = new Direct3DHookModule();
            _direct3DHook.CreateHooks();
        }
    }
}
