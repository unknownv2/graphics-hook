using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using CoreHook;
using DirectX.Direct3D.Core.Drawing;
using DirectX.Direct3D.Core;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using System.Threading;
using System.IO;

namespace DirectX.Direct3D11.Overlay
{
    internal class Direct3DHookModule : Direct3DHook
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int DXGISwapChain_PresentDelegate(IntPtr swapChain, int syncInterval, PresentFlags flags);

        private IHook<DXGISwapChain_PresentDelegate> _d3DPresentHook;
        private List<IntPtr> _d3DDeviceFunctions = new List<IntPtr>();
        private OverlayRenderer _overlayRenderer;

        public const int DXGI_SWAPCHAIN_METHOD_COUNT = 18;
        Device _device;
        SwapChain _swapChain;

        private IntPtr _swapChainPtr;

        private static SwapChainDescription CreateSwapChainDescription(IntPtr windowHandle)
        {
            return new SwapChainDescription
            {
                BufferCount = 1,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                OutputHandle = windowHandle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };
        }

        public override void CreateHooks()
        {
            var renderForm = new SharpDX.Windows.RenderForm();
            Device.CreateWithSwapChain(
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                CreateSwapChainDescription(renderForm.Handle),
                out _device,
                out _swapChain);

            if (_swapChain != null)
            {
                _d3DDeviceFunctions.AddRange(ReadVTableAddresses(_swapChain.NativePointer, DXGI_SWAPCHAIN_METHOD_COUNT));
            }

            _d3DPresentHook = HookFactory.CreateHook<DXGISwapChain_PresentDelegate>(
                _d3DDeviceFunctions[(int)FunctionOrdinals.Present],
                Detour_Present,
                this);

            Overlays = new List<IOverlay>
            {
                // Add the Frames Per Second overlay
                new Direct3D.Core.Drawing.Overlay
                {
                    Elements =
                    {
                        new FramesPerSecondOverlay(new Font("Arial", 16, FontStyle.Bold))
                        {
                            Location = new Point(5, 25),
                            Color = Color.Red,
                            AntiAliased = true,
                            Text = "{0:N0} FPS, First Overlay Test!"
                        },
                        new FramesPerSecondOverlay(new Font("Arial", 16, FontStyle.Bold))
                        {
                            Location = new Point(5, 50),
                            Color = Color.DarkBlue,
                            AntiAliased = true,
                            Text = "{0:N0} FPS, Multiple Overlay Test!"
                        }
                    },
                    Hidden = false
                }
            };

            _d3DPresentHook.Enabled = true;
        }

        private static IEnumerable<IntPtr> ReadVTableAddresses(IntPtr vTableAddress, int vTableFunctionCount)
        {
            IntPtr[] addresses = new IntPtr[vTableFunctionCount];
            IntPtr vTable = Marshal.ReadIntPtr(vTableAddress);
            for (var i = 0; i < vTableFunctionCount; ++i)
            {
                addresses[i] = Marshal.ReadIntPtr(vTable, i * IntPtr.Size);
            }
            return addresses;
        }

        private int Detour_Present(IntPtr swapChainPtr, int syncInterval, SharpDX.DXGI.PresentFlags flags)
        {
            SwapChain swapChain = (SwapChain)swapChainPtr;

            DrawFramesPerSecond(swapChain);

            return _d3DPresentHook.Original(swapChainPtr, syncInterval, flags);
        }

        private void DrawFramesPerSecond(SwapChain swapChain)
        {
            Capture(swapChain);
        }

        private SharpDX.DXGI.KeyedMutex _resolvedTextureMutex;
        private SharpDX.DXGI.KeyedMutex _resolvedTextureMutex2;
        private Texture2D _resolvedTexture;
        private Texture2D _resolvedTextureShared;
        private Texture2D _resizedTexture;
        
        private Texture2D _finalTexture;
        private Query _captureQuery;
        private bool _captureQueryIssued;
        private ShaderResourceView _shaderResourceView;
        private object _captureRequestLock = new object();
        private CaptureRequest _captureRequestCopy;
        private RenderTargetView _renderTargetView;
        private bool _finalTextureMapped;

        private void Capture(SwapChain swapChain)
        {
            try
            {
                // Capture a frame
                if(ScreenshotRequest.Request != null)
                {
                    using (Texture2D texture = Texture2D.FromSwapChain<Texture2D>(swapChain, 0))
                    {
                        var captureRegion = new Rectangle(0, 0, texture.Description.Width, texture.Description.Height);
                        var captureRequest = ScreenshotRequest.Request;
                        if(captureRequest.Region.Width > 0)
                        {
                            captureRegion = new Rectangle(captureRequest.Region.Left, captureRequest.Region.Top,
                                captureRequest.Region.Right, captureRequest.Region.Bottom);
                        }
                        else if(captureRequest.RegionSize.HasValue)
                        {
                            captureRegion = new Rectangle(0, 0, captureRequest.RegionSize.Value.Width, captureRequest.RegionSize.Value.Height);
                        }
                        InitializeCaptureResources(texture.Device, texture.Description, captureRegion, captureRequest);

                        Texture2D sourceTexture = null;

                        if(texture.Description.SampleDescription.Count > 1 || captureRequest.RegionSize.HasValue)
                        {
                            _resolvedTextureMutex?.Acquire(0, int.MaxValue);

                            texture.Device.ImmediateContext.ResolveSubresource(texture, 0, _resolvedTexture, 0, _resolvedTexture.Description.Format);
                            _resolvedTextureMutex?.Release(1);

                            if(captureRequest.RegionSize.HasValue)
                            {
                                lock(_captureRequestLock)
                                {
                                    _resolvedTextureMutex2?.Acquire(1, int.MaxValue);
                                    // TO DO: ScreenAlignedRender code for resized textures
                                }
                                sourceTexture = _resizedTexture;
                            }
                            else
                            {
                                sourceTexture = _resolvedTextureShared != null ? _resolvedTextureShared : _resolvedTexture;
                            }
                        }
                        else
                        {
                            _resolvedTextureMutex?.Acquire(0, int.MaxValue);
                            texture.Device.ImmediateContext.CopySubresourceRegion(texture, 0, null, _resolvedTexture, 0);
                            _resolvedTextureMutex?.Release(1);

                            sourceTexture = _resolvedTextureShared != null ? _resolvedTextureShared : _resolvedTexture;
                        }


                        _captureRequestCopy = captureRequest.Clone();
                        ScreenshotRequest.Request = null;

                        bool shouldAcquireLock = sourceTexture == _resolvedTextureShared;

                        ThreadPool.QueueUserWorkItem(new WaitCallback((callback) =>
                        {
                            if(shouldAcquireLock && _resolvedTextureMutex2 != null)
                            {
                                _resolvedTextureMutex2.Acquire(1, int.MaxValue);
                            }

                            lock(_captureRequestLock)
                            {
                                sourceTexture.Device.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, new ResourceRegion()
                                {
                                    Top = captureRegion.Top,
                                    Bottom = captureRegion.Bottom,
                                    Left = captureRegion.Left,
                                    Right = captureRegion.Right,
                                    Front = 0,
                                    Back = 1,
                                }, _finalTexture, 0, 0, 0, 0);

                                if (shouldAcquireLock && _resolvedTextureMutex2 != null)
                                {
                                    _resolvedTextureMutex2.Release(0);
                                }

                                _finalTexture.Device.ImmediateContext.End(_captureQuery);

                                _captureQueryIssued = true;
                                while(_finalTexture.Device.ImmediateContext.GetData(_captureQuery).ReadByte() != 1)
                                {
                                    // Spin and wait
                                }
                                try
                                {
                                    var dataBox = default(SharpDX.DataBox);

                                    if(_captureRequestCopy.ImageFormat == ImageFormat.PixelData)
                                    {
                                        dataBox = _finalTexture.Device.ImmediateContext.MapSubresource(_finalTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.DoNotWait);
                                        _finalTextureMapped = true;
                                    }
                                    _captureQueryIssued = false;

                                    try
                                    {
                                        using (var memoryStream = new MemoryStream())
                                        {
                                            switch (_captureRequestCopy.ImageFormat)
                                            {
                                                case ImageFormat.Bitmap:
                                                case ImageFormat.Jpeg:
                                                case ImageFormat.Png:
                                                    WriteTextureToStream(_finalTexture.Device.ImmediateContext, _finalTexture,
                                                        _captureRequestCopy.ImageFormat, memoryStream);
                                                    break;
                                                case ImageFormat.PixelData:
                                                    if (dataBox.DataPointer != IntPtr.Zero)
                                                    {
                                                        CaptureSurfaceData(dataBox.DataPointer, dataBox.RowPitch,
                                                            _finalTexture.Description.Width, _finalTexture.Description.Height,
                                                            System.Drawing.Imaging.PixelFormat.Format32bppArgb, _captureRequestCopy);
                                                    }
                                                    return;
                                            
                                            }
                                            memoryStream.Position = 0;
                                            CaptureSurfaceData(memoryStream, _captureRequestCopy);
                                        }
                                    }
                                    finally
                                    {
                                        if(_finalTextureMapped)
                                        {
                                            lock(_captureRequestLock)
                                            {
                                                _finalTexture.Device.ImmediateContext.UnmapSubresource(_finalTexture, 0);
                                                _finalTextureMapped = false;
                                            }
                                        }
                                    }
                                }
                                catch (SharpDX.SharpDXException e)
                                {

                                }
                            }

                        }));
                    }
                }

                // Draw any overlays
                var displayOverlays = Overlays;
                
                if (_overlayRenderer == null ||
                    _swapChainPtr != swapChain.NativePointer ||
                    PendingUpdate)
                {
                    if (_overlayRenderer != null)
                    {
                        _overlayRenderer.Dispose();
                    }

                    _swapChainPtr = swapChain.NativePointer;

                    _overlayRenderer = ToDispose((new OverlayRenderer()));
                    _overlayRenderer.Overlays.AddRange(displayOverlays);
                    _overlayRenderer.Initialize(swapChain);
                    PendingUpdate = false;
                }
                
                if (_overlayRenderer != null)
                {
                    foreach (var overlay in _overlayRenderer.Overlays)
                    {
                        overlay.OnFrame();
                    }

                    _overlayRenderer.DrawFrame();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{e}");
            }
        }

        private void InitializeCaptureResources(Device device, Texture2DDescription textureDescription,
            Rectangle captureRegion, CaptureRequest captureRequest, bool useSameDeviceForResize = false)
        {
            var resizeDevice = useSameDeviceForResize ? device : _device;

            if(_finalTexture != null && (_finalTexture.Device.NativePointer == device.NativePointer || _finalTexture.Device.NativePointer == _device.NativePointer) &&
                _finalTexture.Description.Height == captureRegion.Height && _finalTexture.Description.Width == captureRegion.Width &&
                _resolvedTexture != null && _resolvedTexture.Description.Height == textureDescription.Height && _resolvedTexture.Description.Width == textureDescription.Width &&
                (_resolvedTexture.Device.NativePointer == device.NativePointer || _resolvedTexture.Device.NativePointer == _device.NativePointer) &&
                _resolvedTexture.Description.Format == textureDescription.Format)
            {

            }
            else
            {
                RemoveAndDispose(ref _captureQuery);
                RemoveAndDispose(ref _resolvedTexture);
                RemoveAndDispose(ref _finalTexture);
                RemoveAndDispose(ref _resolvedTextureShared);
                RemoveAndDispose(ref _shaderResourceView);
                RemoveAndDispose(ref _resolvedTextureMutex);
                RemoveAndDispose(ref _resolvedTextureMutex2);
                _captureQuery = new Query(resizeDevice, new QueryDescription()
                {
                    Flags = QueryFlags.None,
                    Type = QueryType.Event
                });

                _captureQueryIssued = false;

                try
                {
                    ResourceOptionFlags optionFlags = ResourceOptionFlags.None;
                    if(device != resizeDevice)
                    {
                        optionFlags |= ResourceOptionFlags.SharedKeyedmutex;
                    }

                    _resolvedTexture = ToDispose(new Texture2D(device, new Texture2DDescription()
                    {
                        CpuAccessFlags = CpuAccessFlags.None,
                        Format = textureDescription.Format,
                        Height = textureDescription.Height,
                        Usage = ResourceUsage.Default,
                        Width = textureDescription.Width,
                        ArraySize = 1,
                        SampleDescription = new SampleDescription(1, 0),
                        BindFlags = BindFlags.ShaderResource,
                        MipLevels = 1,
                        OptionFlags = optionFlags
                    }));
                }
                catch
                {
                    InitializeCaptureResources(device, textureDescription, captureRegion, captureRequest, true);
                    return;
                }

                _resolvedTextureMutex = ToDispose(_resolvedTexture.QueryInterfaceOrNull<SharpDX.DXGI.KeyedMutex>());

                if(_resolvedTextureMutex != null)
                {
                    using (var resource = _resolvedTexture.QueryInterface<SharpDX.DXGI.Resource>())
                    {
                        _resolvedTextureShared = ToDispose(resizeDevice.OpenSharedResource<Texture2D>(resource.SharedHandle));
                        _resolvedTextureMutex2 = ToDispose(_resolvedTextureShared.QueryInterfaceOrNull<SharpDX.DXGI.KeyedMutex>());
                    }

                    _shaderResourceView = ToDispose(new ShaderResourceView(resizeDevice, _resolvedTextureShared));
                }
                else
                {
                    _shaderResourceView = ToDispose(new ShaderResourceView(resizeDevice, _resolvedTexture));
                }

                _finalTexture = ToDispose(new Texture2D(resizeDevice, new Texture2DDescription()
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    Format = textureDescription.Format,
                    Height = captureRegion.Height,
                    Usage = ResourceUsage.Staging,
                    Width = captureRegion.Width,
                    ArraySize = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    BindFlags = BindFlags.None,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None
                }));

                _finalTextureMapped = false;
            }

            if(_resolvedTexture != null && _resolvedTextureMutex2 == null || resizeDevice == _device)
            {
                resizeDevice = device;
            }
            if(resizeDevice != null && captureRequest.RegionSize != null && (_resizedTexture == null ||
                (_resizedTexture.Device.NativePointer != resizeDevice.NativePointer || _resizedTexture.Description.Width != captureRequest.RegionSize.Value.Width
                || _resizedTexture.Description.Height != captureRequest.RegionSize.Value.Height)))
            {
                // RESIZING TODO: ScreenAlignedQuadRenderer and DeviceManager
                RemoveAndDispose(ref _resizedTexture);
                RemoveAndDispose(ref _renderTargetView);
                
            }
        }

        private SharpDX.WIC.ImagingFactory2 _wicImagingFactory;
        private void WriteTextureToStream(DeviceContext context, Texture2D texture, ImageFormat outputFormat, Stream stream)
        {
            if(_wicImagingFactory == null)
            {
                _wicImagingFactory = ToDispose(new SharpDX.WIC.ImagingFactory2());
            }

            SharpDX.DataStream dataStream;
            var dataBox = context.MapSubresource(texture, 0, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out dataStream);

            try
            {
                var dataRectangle = new SharpDX.DataRectangle
                {
                    DataPointer = dataStream.DataPointer,
                    Pitch = dataBox.RowPitch
                };

                var format = PixelFormatFromFormat(texture.Description.Format);
                if(format == Guid.Empty)
                {
                    return;
                }

                using (var bitmap = new SharpDX.WIC.Bitmap(
                    _wicImagingFactory,
                    texture.Description.Width,
                    texture.Description.Height,
                    format,
                    dataRectangle))
                {
                    stream.Position = 0;

                    SharpDX.WIC.BitmapEncoder bitmapEncoder = null;
                    switch(outputFormat)
                    {
                        case ImageFormat.Bitmap:
                            bitmapEncoder = new SharpDX.WIC.BmpBitmapEncoder(_wicImagingFactory, stream);
                            break;
                        case ImageFormat.Jpeg:
                            bitmapEncoder = new SharpDX.WIC.JpegBitmapEncoder(_wicImagingFactory, stream);
                            break;
                        case ImageFormat.Png:
                            bitmapEncoder = new SharpDX.WIC.PngBitmapEncoder(_wicImagingFactory, stream);
                            break;
                        default:
                            return;
                    }

                    try
                    {
                        using (var bitmapFrameEncode = new SharpDX.WIC.BitmapFrameEncode(bitmapEncoder))
                        {
                            bitmapFrameEncode.Initialize();
                            bitmapFrameEncode.SetSize(bitmap.Size.Width, bitmap.Size.Height);
                            var pixelFormat = format;
                            bitmapFrameEncode.SetPixelFormat(ref pixelFormat);

                            if (pixelFormat != format)
                            {
                                using (var formatConverter = new SharpDX.WIC.FormatConverter(_wicImagingFactory))
                                {
                                    if (formatConverter.CanConvert(format, pixelFormat))
                                    {
                                        formatConverter.Initialize(bitmap, SharpDX.WIC.PixelFormat.Format24bppBGR,
                                            SharpDX.WIC.BitmapDitherType.None, null, 0, SharpDX.WIC.BitmapPaletteType.MedianCut);

                                        bitmapFrameEncode.SetPixelFormat(ref pixelFormat);
                                        bitmapFrameEncode.WriteSource(formatConverter);
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                bitmapFrameEncode.WriteSource(bitmap);
                            }
                            bitmapFrameEncode.Commit();
                            bitmapEncoder.Commit();
                        }
                    }
                    finally
                    {
                        bitmapEncoder.Dispose();
                    }
                }
            }
            finally
            {
                context.UnmapSubresource(texture, 0);
            }
        }
        public static Guid PixelFormatFromFormat(SharpDX.DXGI.Format format)
        {
            switch (format)
            {
                case SharpDX.DXGI.Format.R32G32B32A32_Typeless:
                case SharpDX.DXGI.Format.R32G32B32A32_Float:
                    return SharpDX.WIC.PixelFormat.Format128bppRGBAFloat;
                case SharpDX.DXGI.Format.R32G32B32A32_UInt:
                case SharpDX.DXGI.Format.R32G32B32A32_SInt:
                    return SharpDX.WIC.PixelFormat.Format128bppRGBAFixedPoint;
                case SharpDX.DXGI.Format.R32G32B32_Typeless:
                case SharpDX.DXGI.Format.R32G32B32_Float:
                    return SharpDX.WIC.PixelFormat.Format96bppRGBFloat;
                case SharpDX.DXGI.Format.R32G32B32_UInt:
                case SharpDX.DXGI.Format.R32G32B32_SInt:
                    return SharpDX.WIC.PixelFormat.Format96bppRGBFixedPoint;
                case SharpDX.DXGI.Format.R16G16B16A16_Typeless:
                case SharpDX.DXGI.Format.R16G16B16A16_Float:
                case SharpDX.DXGI.Format.R16G16B16A16_UNorm:
                case SharpDX.DXGI.Format.R16G16B16A16_UInt:
                case SharpDX.DXGI.Format.R16G16B16A16_SNorm:
                case SharpDX.DXGI.Format.R16G16B16A16_SInt:
                    return SharpDX.WIC.PixelFormat.Format64bppRGBA;
                case SharpDX.DXGI.Format.R32G32_Typeless:
                case SharpDX.DXGI.Format.R32G32_Float:
                case SharpDX.DXGI.Format.R32G32_UInt:
                case SharpDX.DXGI.Format.R32G32_SInt:
                case SharpDX.DXGI.Format.R32G8X24_Typeless:
                case SharpDX.DXGI.Format.D32_Float_S8X24_UInt:
                case SharpDX.DXGI.Format.R32_Float_X8X24_Typeless:
                case SharpDX.DXGI.Format.X32_Typeless_G8X24_UInt:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.R10G10B10A2_Typeless:
                case SharpDX.DXGI.Format.R10G10B10A2_UNorm:
                case SharpDX.DXGI.Format.R10G10B10A2_UInt:
                    return SharpDX.WIC.PixelFormat.Format32bppRGBA1010102;
                case SharpDX.DXGI.Format.R11G11B10_Float:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.R8G8B8A8_Typeless:
                case SharpDX.DXGI.Format.R8G8B8A8_UNorm:
                case SharpDX.DXGI.Format.R8G8B8A8_UNorm_SRgb:
                case SharpDX.DXGI.Format.R8G8B8A8_UInt:
                case SharpDX.DXGI.Format.R8G8B8A8_SNorm:
                case SharpDX.DXGI.Format.R8G8B8A8_SInt:
                    return SharpDX.WIC.PixelFormat.Format32bppRGBA;
                case SharpDX.DXGI.Format.R16G16_Typeless:
                case SharpDX.DXGI.Format.R16G16_Float:
                case SharpDX.DXGI.Format.R16G16_UNorm:
                case SharpDX.DXGI.Format.R16G16_UInt:
                case SharpDX.DXGI.Format.R16G16_SNorm:
                case SharpDX.DXGI.Format.R16G16_SInt:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.R32_Typeless:
                case SharpDX.DXGI.Format.D32_Float:
                case SharpDX.DXGI.Format.R32_Float:
                case SharpDX.DXGI.Format.R32_UInt:
                case SharpDX.DXGI.Format.R32_SInt:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.R24G8_Typeless:
                case SharpDX.DXGI.Format.D24_UNorm_S8_UInt:
                case SharpDX.DXGI.Format.R24_UNorm_X8_Typeless:
                    return SharpDX.WIC.PixelFormat.Format32bppGrayFloat;
                case SharpDX.DXGI.Format.X24_Typeless_G8_UInt:
                case SharpDX.DXGI.Format.R9G9B9E5_Sharedexp:
                case SharpDX.DXGI.Format.R8G8_B8G8_UNorm:
                case SharpDX.DXGI.Format.G8R8_G8B8_UNorm:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.B8G8R8A8_UNorm:
                case SharpDX.DXGI.Format.B8G8R8X8_UNorm:
                    return SharpDX.WIC.PixelFormat.Format32bppBGRA;
                case SharpDX.DXGI.Format.R10G10B10_Xr_Bias_A2_UNorm:
                    return SharpDX.WIC.PixelFormat.Format32bppBGR101010;
                case SharpDX.DXGI.Format.B8G8R8A8_Typeless:
                case SharpDX.DXGI.Format.B8G8R8A8_UNorm_SRgb:
                case SharpDX.DXGI.Format.B8G8R8X8_Typeless:
                case SharpDX.DXGI.Format.B8G8R8X8_UNorm_SRgb:
                    return SharpDX.WIC.PixelFormat.Format32bppBGRA;
                case SharpDX.DXGI.Format.R8G8_Typeless:
                case SharpDX.DXGI.Format.R8G8_UNorm:
                case SharpDX.DXGI.Format.R8G8_UInt:
                case SharpDX.DXGI.Format.R8G8_SNorm:
                case SharpDX.DXGI.Format.R8G8_SInt:
                    return Guid.Empty;
                case SharpDX.DXGI.Format.R16_Typeless:
                case SharpDX.DXGI.Format.R16_Float:
                case SharpDX.DXGI.Format.D16_UNorm:
                case SharpDX.DXGI.Format.R16_UNorm:
                case SharpDX.DXGI.Format.R16_SNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppGrayHalf;
                case SharpDX.DXGI.Format.R16_UInt:
                case SharpDX.DXGI.Format.R16_SInt:
                    return SharpDX.WIC.PixelFormat.Format16bppGrayFixedPoint;
                case SharpDX.DXGI.Format.B5G6R5_UNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppBGR565;
                case SharpDX.DXGI.Format.B5G5R5A1_UNorm:
                    return SharpDX.WIC.PixelFormat.Format16bppBGRA5551;
                case SharpDX.DXGI.Format.B4G4R4A4_UNorm:
                    return Guid.Empty;

                case SharpDX.DXGI.Format.R8_Typeless:
                case SharpDX.DXGI.Format.R8_UNorm:
                case SharpDX.DXGI.Format.R8_UInt:
                case SharpDX.DXGI.Format.R8_SNorm:
                case SharpDX.DXGI.Format.R8_SInt:
                    return SharpDX.WIC.PixelFormat.Format8bppGray;
                case SharpDX.DXGI.Format.A8_UNorm:
                    return SharpDX.WIC.PixelFormat.Format8bppAlpha;
                case SharpDX.DXGI.Format.R1_UNorm:
                    return SharpDX.WIC.PixelFormat.Format1bppIndexed;

                default:
                    return Guid.Empty;
            }
        }
    }
}
