# Graphics Hook Sample

Example tool for hooking graphics APIs using [CoreHook](https://github.com/unknownv2/CoreHook).


Based on [Justin Stenning's Direct3DHook](https://github.com/spazzarama/Direct3DHook).

## Requirements

**[Building the sample requires the .NET Core 3.0 SDK, which can be downloaded here.](https://dotnet.microsoft.com/download/dotnet-core/3.0)**

The Direct3D10 and Direct3D11 modules require the .NET Windows Form libraries, which are only available with .NET Core 3.0 and above.

The `deps` folder contains the [`SharpDX.Desktop`](https://github.com/unknownv2/SharpDX.Desktop) library targeting .NET Core 3.0, which is used by the Direct3D10 and Direct3D11 sample modules.

## Building

Clone and build the sample with:

```
git clone --recursive git://github.com/unknownv2/graphics-hook.git
cd graphics-hook
dotnet build
```

## References

[Screen Capture and Overlays for Direct3D 9, 10 and 11 using API Hooks](https://spazzarama.com/2011/03/14/c-screen-capture-and-overlays-for-direct3d-9-10-and-11-using-api-hooks/)