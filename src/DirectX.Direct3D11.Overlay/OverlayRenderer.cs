using System;
using System.Collections.Generic;
using System.Text;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core.Memory;
using SharpDX;
using SharpDX.Direct3D11;

namespace DirectX.Direct3D11.Overlay
{
    internal class OverlayRenderer : DisposableComponent
    {
        public List<IOverlay> Overlays { get; set; } = new List<IOverlay>();
        private Device _device;
        private Texture2D _renderTarget;
        private RenderTargetView _renderTargetView;
        private DXSprite _spriteEngine;
        Dictionary<string, DXFont> _fontCache = new Dictionary<string, DXFont>();
        private DeviceContext _deviceContext;
        //private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
        public bool DeferredContext
        {
            get => _deviceContext.TypeInfo == DeviceContextType.Deferred;
        }



        public Device Device => _device;
 
        private bool _isInitialized;
        private bool _isInitializing;

        internal OverlayRenderer()
        {
        }

        private void EnsureInitialized()
        {
            System.Diagnostics.Debug.Assert(_isInitialized);
        }

        internal bool Initialize(SharpDX.DXGI.SwapChain swapChain)
        {
            return Initialize(swapChain.GetDevice<Device>(), swapChain.GetBackBuffer<Texture2D>(0));
        }

        internal bool Initialize(Device device, Texture2D renderTarget)
        {
            if (_isInitializing)
            {
                return false;
            }

            _isInitializing = true;

            try
            {
                _device = device;
                _renderTarget = renderTarget;
                try
                {
                    _deviceContext = ToDispose(new DeviceContext(_device));
                }
                catch(SharpDXException)
                {
                    _deviceContext = _device.ImmediateContext;
                }

                _renderTargetView = ToDispose(new RenderTargetView(_device, _renderTarget));
                _spriteEngine = new DXSprite(_device, _deviceContext);
                if(!_spriteEngine.Initialize())
                {
                    return false;
                }

                InitializeResources();

                _isInitialized = true;
            }
            finally
            {
                _isInitializing = true;
            }
            return true;
        }

        private void InitializeResources()
        {
            foreach (var overlay in Overlays)
            {
                foreach (var overlayElement in overlay.Elements)
                {
                    if (overlayElement is TextOverlay textOverlay)
                    {
                        GetOverlayFont(textOverlay);
                    }
                }
            }
        }

        private DXFont GetOverlayFont(TextOverlay textOverlay)
        {
            string fontKey =
              $"{textOverlay.Font.Name}{textOverlay.Font.Size}{textOverlay.Font.Style}{textOverlay.AntiAliased}";

            if (!_fontCache.TryGetValue(fontKey, out DXFont overlayFont))
            {
                overlayFont = ToDispose(new DXFont(_device, _deviceContext));
                overlayFont.Initialize(textOverlay.Font.Name, textOverlay.Font.Size, textOverlay.Font.Style, textOverlay.AntiAliased);
                _fontCache[fontKey] = overlayFont;
            }
            return overlayFont;
        }

        public void DrawFrame()
        {
            EnsureInitialized();

            BeginFrame();

            foreach (var overlay in Overlays)
            {
                foreach (var overlayElement in overlay.Elements)
                {
                    if (overlayElement.Hidden)
                    {
                        continue;
                    }

                    if (overlayElement is TextOverlay textOverlay)
                    {
                        var font = GetOverlayFont(textOverlay);
                        if (font != null && !string.IsNullOrEmpty(textOverlay.Text))
                        {
                            _spriteEngine.DrawString(textOverlay.Location.X, textOverlay.Location.Y, textOverlay.Text,
                                textOverlay.Color, font);
                        }
                    }
                }
            }

            EndFrame();
        }

        private void BeginFrame()
        {
            SharpDX.Mathematics.Interop.RawViewportF[] viewport =
            {
                new ViewportF(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height, 0, 1)
            };
            _deviceContext.Rasterizer.SetViewports(viewport);
            _deviceContext.OutputMerger.SetTargets(_renderTargetView);
        }

        private void EndFrame()
        {
            if(DeferredContext)
            {
                var commands = _deviceContext.FinishCommandList(true);
                _device.ImmediateContext.ExecuteCommandList(commands, true);
                commands.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            _device = null;
        }
    }
}
