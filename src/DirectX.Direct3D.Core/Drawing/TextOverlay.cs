using System;
using System.Collections.Generic;
using System.Text;

namespace DirectX.Direct3D.Core.Drawing
{
    public class TextOverlay : OverlayElement
    {
        public virtual string Text { get; set; }

        public virtual System.Drawing.Font Font { get; set; } = System.Drawing.SystemFonts.DefaultFont;

        public virtual System.Drawing.Color Color { get; set; } = System.Drawing.Color.Black;

        public virtual System.Drawing.Point Location { get; set; }

        public virtual bool AntiAliased { get; set; } = false;

        public TextOverlay()
        {
        }

        public TextOverlay(System.Drawing.Font font)
        {
            Font = font;
        }
    }
}
