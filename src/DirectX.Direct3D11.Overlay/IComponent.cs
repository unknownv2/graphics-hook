using System;
using System.Collections.Generic;
using System.Text;

namespace DirectX.Direct3D11.Overlay
{

    public interface IComponent
    {
        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        /// <value>The name.</value>
        string Name { get; set; }
    }
}