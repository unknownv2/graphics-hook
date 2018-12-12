using System;

namespace DirectX.Direct3D.Core.Drawing
{
    public class OverlayElement : IOverlayElement, IDisposable
    {
        public virtual bool Hidden { get; set; }

        public object Clone() => MemberwiseClone();

        public virtual void OnFrame()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void SafeDispose(IDisposable disposableObject) => disposableObject?.Dispose();
    }
}
