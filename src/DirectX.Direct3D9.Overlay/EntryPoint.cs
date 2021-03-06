﻿using CoreHook;
using DirectX.Direct3D.Core;

namespace DirectX.Direct3D9.Overlay
{
    public class EntryPoint : IEntryPoint
    {
        private Direct3DHook _direct3DHook;

        public EntryPoint(IContext context) { }

        public void Run(IContext context)
        {
            InitializeDeviceHook();
            while (true)
            {
                System.Threading.Thread.Sleep(30000);
            }
        }

        public void InitializeDeviceHook()
        {
            _direct3DHook = new Direct3DHookModule();
            _direct3DHook.CreateHooks();
        }
    }
}
