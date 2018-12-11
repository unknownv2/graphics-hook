using System;
using System.Collections.Generic;
using System.Text;

namespace DirectX.Direct3D.Core.Memory
{
    public abstract class DisposableBase : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            CheckAndDispose(true);
        }

        private void CheckAndDispose(bool disposing)
        {
            if (!IsDisposed)
            {

                IsDisposed = true;
            }

        }

        protected abstract void Dispose(bool disposing);
    }
}
