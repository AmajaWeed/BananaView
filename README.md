# BananaView

A fast, Picasa Photo Viewer-style image viewer for Windows (C# / WPF, .NET 8).

## Features

- Buttery-smooth zoom, pan, and image-switch transitions
- Borderless translucent fullscreen "Picasa mode" with auto-hiding toolbar and filmstrip, plus a normal resizable windowed mode
- Broad format support: PNG, JPG/JFIF, BMP, animated GIF, TIFF, WEBP, HEIC/HEIF, ICO, ICNS, PSD (flattened), Procreate (`.procreate`, including in-app timelapse playback), and SAI2 (native decoder)
- Copy to clipboard, OCR (Windows' built-in text recognition), rotate, delete to Recycle Bin
- Disk-backed thumbnail cache for fast filmstrip loading

## Building

```
dotnet build Viewer.sln
```

Requires the .NET 8 SDK and the Windows 10 SDK (10.0.19041.0) for the WinRT OCR APIs.

## Installer

An Inno Setup script (`installer/BananaView.iss`) builds a Windows installer that registers BananaView as a selectable default app for supported image formats.
