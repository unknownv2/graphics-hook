using System.Collections.Generic;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core.Memory;
using SharpDX;
using SharpDX.Direct3D9;

namespace DirectX.Direct3D9.Overlay
{
    internal class OverlayRenderer : DisposableComponent
    {
        public List<IOverlay> Overlays { get; set; } = new List<IOverlay>();
        private Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

        private Device _device;
        private Sprite _sprite;

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

        internal bool Initialize(Device device)
        {
            if (_isInitializing)
            {
                return false;
            }

            _isInitializing = true;

            try
            {
                _device = device;

                _sprite = ToDispose(new Sprite(device));
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
                    if(overlayElement is TextOverlay textOverlay)
                    {
                        GetOverlayFont(textOverlay);
                    }
                }
            }
        }

        public void DrawFrame()
        {
            EnsureInitialized();

            BeginFrame();

            foreach (var overlay in Overlays)
            {
                foreach (var overlayElement in overlay.Elements)
                {
                    if (overlayElement is TextOverlay textOverlay)
                    {
                        var font = GetOverlayFont(textOverlay);
                        if (font != null && !string.IsNullOrEmpty(textOverlay.Text))
                        {
                            font.DrawText(_sprite, textOverlay.Text, textOverlay.Location.X, textOverlay.Location.Y,
                                new ColorBGRA(textOverlay.Color.R, textOverlay.Color.G, textOverlay.Color.B,
                                    textOverlay.Color.A));
                        }
                    }
                }
            }

            EndFrame();
        }

        private void BeginFrame()
        {

        }

        private void EndFrame()
        {

        }

        private Font GetOverlayFont(TextOverlay textOverlay)
        {
            string fontKey =
                $"{textOverlay.Font.Name}{textOverlay.Font.Size}{textOverlay.Font.Style}{textOverlay.AntiAliased}";

            if (_fontCache.TryGetValue(fontKey, out Font overlayFont))
            {
                overlayFont = ToDispose(new Font(_device, new FontDescription()
                {
                    FaceName = textOverlay.Font.Name,
                    Italic = (textOverlay.Font.Style & System.Drawing.FontStyle.Italic) == System.Drawing.FontStyle.Italic,
                    Quality = (textOverlay.AntiAliased ? FontQuality.Antialiased : FontQuality.Default),
                    Weight = ((textOverlay.Font.Style & System.Drawing.FontStyle.Bold) == System.Drawing.FontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal,
                    Height = (int)textOverlay.Font.SizeInPoints
                }));
                _fontCache[fontKey] = overlayFont;
            }
            return overlayFont;
        }
    }
}
