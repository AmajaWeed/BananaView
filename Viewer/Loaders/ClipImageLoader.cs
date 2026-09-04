using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;

namespace Viewer.Loaders;

// CLIP STUDIO PAINT's .clip format: a custom "CSFCHUNK" TLV container (8-byte
// ASCII type tag + 8-byte big-endian length, repeated) with no relation to
// ZIP. One chunk, "CHNKSQLi", is itself a complete embedded SQLite database
// whose CanvasPreview table holds a full PNG preview in its ImageData
// column - this is the same approach used by the open-source clipthumb tool
// (https://github.com/jercos/clipthumb), reimplemented natively here rather
// than shelling out to a C binary. We don't parse the actual layer/canvas
// data at all, only pull this one preview blob out.
public sealed class ClipImageLoader : IImageLoader
{
    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".clip", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        var png = ExtractPreviewPng(path)
            ?? throw new NotSupportedException("This .clip file has no CanvasPreview to display.");

        var bmp = new BitmapImage();
        using (var ms = new MemoryStream(png))
        {
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
        }
        bmp.Freeze();
        return new LoadedImage(bmp);
    });

    public static byte[]? ExtractPreviewPng(string path)
    {
        var sqliteBytes = ExtractSqliteChunk(path);
        if (sqliteBytes == null) return null;

        var tempDbPath = Path.Combine(Path.GetTempPath(), $"bananaview_clip_{Guid.NewGuid():N}.db");
        try
        {
            File.WriteAllBytes(tempDbPath, sqliteBytes);

            using var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ImageData FROM CanvasPreview LIMIT 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            return (byte[])reader["ImageData"];
        }
        finally
        {
            try { File.Delete(tempDbPath); } catch { /* best-effort cleanup */ }
        }
    }

    // Reads the CSFCHUNK header, then walks its top-level chunks looking for
    // "CHNKSQLi" and returns that chunk's raw bytes (a complete SQLite file).
    private static byte[]? ExtractSqliteChunk(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 24) return null;
        var magic = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != "CSFCHUNK") return null;
        // Two 8-byte fields follow the magic before the first real chunk
        // header - confirmed against clipthumb.c's exact read sequence
        // (an initial combined type+len readv, THEN a separate 8-byte read,
        // both discarded) and verified against a real .clip file's bytes;
        // treating this as a single 8-byte field (as clipthumb's own
        // struct layout misleadingly suggests) misaligns every chunk after it.
        reader.ReadBytes(8);
        reader.ReadBytes(8);

        while (stream.Position + 16 <= stream.Length)
        {
            var type = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8));
            var length = ReadBigEndianUInt64(reader);
            if (length > (ulong)(stream.Length - stream.Position)) return null; // corrupt/truncated

            if (type == "CHNKSQLi")
                return reader.ReadBytes((int)length);

            stream.Seek((long)length, SeekOrigin.Current);
        }

        return null;
    }

    private static ulong ReadBigEndianUInt64(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        Array.Reverse(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
}
