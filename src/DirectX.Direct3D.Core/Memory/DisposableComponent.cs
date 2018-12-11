using System;
using System.Collections.Generic;
using System.Text;
using SharpDX;

namespace DirectX.Direct3D.Core.Memory
{
    public class DisposableComponent : DisposableBase
    {
        protected DisposeCollector DisposeCollector { get; set; }

        protected override void Dispose(bool disposeManagedResources)
        {

        }

        protected internal T ToDispose<T>(T disposable)
        {
            if (!ReferenceEquals(disposable, null))
            {
                if (DisposeCollector == null)
                {
                    DisposeCollector = new DisposeCollector();
                }

                return DisposeCollector.Collect(disposable);
            }

            return default(T);
        }
    }
}
