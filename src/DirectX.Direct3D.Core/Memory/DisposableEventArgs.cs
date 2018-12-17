using System;

namespace DirectX.Direct3D.Core.Memory
{
    public class DisposableEventArgs : EventArgs
    {
        public static readonly DisposableEventArgs DisposingEventArgs = new DisposableEventArgs(true);

        public static readonly DisposableEventArgs NotDisposingEventArgs = new DisposableEventArgs(false);

        public readonly bool Disposing;

        private DisposableEventArgs(bool disposing)
        {
            Disposing = disposing;
        }

        public static DisposableEventArgs Get(bool disposing)
        {
            return disposing ? DisposingEventArgs : NotDisposingEventArgs;
        }
    }
}
