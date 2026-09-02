using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Viewer.Loaders;

namespace Viewer.Services;

public sealed class FolderCatalog
{
    private readonly ImageLoaderRegistry _registry;
    private List<string> _files = new();

    public FolderCatalog(ImageLoaderRegistry registry) => _registry = registry;

    public IReadOnlyList<string> Files => _files;
    public int CurrentIndex { get; private set; } = -1;
    public string? CurrentFile => CurrentIndex >= 0 && CurrentIndex < _files.Count ? _files[CurrentIndex] : null;

    public void LoadFolder(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();

        _files = Directory.EnumerateFiles(dir)
            .Where(f => _registry.IsSupported(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f), NaturalStringComparer.Instance)
            .ToList();

        CurrentIndex = _files.FindIndex(f => string.Equals(Path.GetFullPath(f), full, StringComparison.OrdinalIgnoreCase));
        if (CurrentIndex < 0 && _files.Count > 0) CurrentIndex = 0;
    }

    public string? Next()
    {
        if (_files.Count == 0) return null;
        CurrentIndex = (CurrentIndex + 1) % _files.Count;
        return CurrentFile;
    }

    public string? Previous()
    {
        if (_files.Count == 0) return null;
        CurrentIndex = (CurrentIndex - 1 + _files.Count) % _files.Count;
        return CurrentFile;
    }

    public string? JumpTo(int index)
    {
        if (index < 0 || index >= _files.Count) return null;
        CurrentIndex = index;
        return CurrentFile;
    }

    public void RemoveCurrent()
    {
        if (CurrentIndex < 0 || CurrentIndex >= _files.Count) return;
        _files.RemoveAt(CurrentIndex);
        if (CurrentIndex >= _files.Count) CurrentIndex = _files.Count - 1;
    }
}
