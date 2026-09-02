using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Viewer.Loaders;

// Native C# port of the user's Python SAI2 decoder (sai2layers.py + sai2dpcm.py,
// itself a port of libsai's UnpackDeltaRLE16/DeltaUnpackRow16Bpc), decoding the
// file's own pre-flattened "intg" chunk directly to a BGRA buffer in memory - no
// subprocess, no temp PNG file, no Python/numpy startup cost. Only handles the
// flattened composite (matches what the previous Python-bridge loader showed);
// full per-layer decoding stays out of scope here, same as before.
public static class Sai2NativeDecoder
{
    public readonly struct DecodedImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Bgra; // 4 bytes/pixel, B,G,R,A - matches PixelFormats.Bgra32 directly
        public DecodedImage(int width, int height, byte[] bgra) { Width = width; Height = height; Bgra = bgra; }
    }

    public static DecodedImage DecodeFlattened(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 0x40 || Encoding.ASCII.GetString(data, 0, 15) != "SAI-CANVAS-TYPE")
            throw new InvalidDataException("Not a recognized .sai2 file.");

        int width = BitConverter.ToInt32(data, 20);
        int height = BitConverter.ToInt32(data, 24);
        int entryCount = BitConverter.ToInt32(data, 32);

        int o = 0x40;
        var types = new string[entryCount];
        var offsets = new int[entryCount];
        for (var i = 0; i < entryCount; i++)
        {
            types[i] = Encoding.ASCII.GetString(data, o, 4);
            offsets[i] = BitConverter.ToInt32(data, o + 8);
            o += 16;
        }

        // Entry sizes are implicit: each entry runs until the next distinct offset.
        var sortedOffsets = new System.Collections.Generic.SortedSet<int>(offsets) { data.Length };
        var offsetList = new System.Collections.Generic.List<int>(sortedOffsets);

        var intgIndex = Array.IndexOf(types, "intg");
        if (intgIndex < 0)
            throw new InvalidDataException("This .sai2 file has no 'intg' (merged/flattened) chunk.");

        var intgOff = offsets[intgIndex];
        var idx = offsetList.IndexOf(intgOff);
        var intgSize = offsetList[idx + 1] - intgOff;

        var bgFlags = data[17];
        var bgra = DecodeFullDpcm(data, intgOff, intgSize, width, height, bgFlags);
        return new DecodedImage(width, height, bgra);
    }

    private static byte[] DecodeFullDpcm(byte[] data, int blobOffset, int blobSize, int width, int height, byte bgFlags)
    {
        if (blobSize < 4 || Encoding.ASCII.GetString(data, blobOffset, 4) != "dpcm")
            throw new InvalidDataException("Expected a 'dpcm'-prefixed blob.");

        var p = blobOffset + 4;
        var inChannels = ((bgFlags & 7) == 0 ? 1 : 0) + 3;
        const int TS = 256;
        var tx = (width + TS - 1) / TS;
        var ty = (height + TS - 1) / TS;
        var ntiles = tx * ty;

        var tileSizes = new int[ntiles];
        for (var i = 0; i < ntiles; i++)
        {
            tileSizes[i] = BitConverter.ToInt32(data, p);
            p += 4;
        }

        var img = new byte[width * height * 4];
        var pos = p;

        for (var tyi = 0; tyi < ty; tyi++)
        {
            var begY = tyi * TS;
            var endY = Math.Min(begY + TS, height);
            var sy = endY - begY;

            for (var txi = 0; txi < tx; txi++)
            {
                var tsize = tileSizes[tyi * tx + txi];
                var tileStart = pos;
                pos += tsize;

                var q = tileStart + 2; // skip the leading u16 checksum
                var begX = txi * TS;
                var endX = Math.Min(begX + TS, width);
                var sx = endX - begX;

                var prevRow = NewPixelRow(sx);

                for (var ry = 0; ry < sy; ry++)
                {
                    var (delta, consumed) = UnpackDeltaRle16(data, q, tileStart + tsize, sx, 4, inChannels);
                    if (consumed == 0) break;

                    var dest = NewPixelRow(sx);
                    DeltaUnpackRow(dest, prevRow, delta, sx);

                    var baseIdx = ((begY + ry) * width + begX) * 4;
                    for (var x = 0; x < sx; x++)
                    {
                        var b = dest[x][0]; var g = dest[x][1]; var r = dest[x][2]; var a = dest[x][3];
                        var o2 = baseIdx + x * 4;
                        // Already B,G,R,A - matches WPF's Bgra32 byte order directly, no swap needed.
                        img[o2] = (byte)b; img[o2 + 1] = (byte)g; img[o2 + 2] = (byte)r;
                        img[o2 + 3] = inChannels == 3 ? (byte)0xFF : (byte)a;
                    }

                    prevRow = dest;
                    q += consumed;
                }
            }

            pos += 2; // trailing per-band checksum
        }

        return img;
    }

    private static int[][] NewPixelRow(int count)
    {
        var row = new int[count][];
        for (var i = 0; i < count; i++) row[i] = new int[4];
        return row;
    }

    // Port of DeltaUnpackRow16Bpc.
    private static void DeltaUnpackRow(int[][] dest, int[][] prevRow, short[] delta, int pixelCount, int outChannels = 4)
    {
        var sumC = new int[4];
        var prevPix = new int[4];

        for (var i = 0; i < pixelCount; i++)
        {
            var pr = prevRow[i];
            var dOff = i * outChannels;
            var outpix = new int[4];

            for (var c = 0; c < 4; c++)
            {
                var curPrev = pr[c] & 0xFFFF;
                var t = (sumC[c] + curPrev) & 0xFFFF;              // add16
                t = t > prevPix[c] ? t - prevPix[c] : 0;            // subsat16
                var s = t + 0xFF00;
                t = s < 0xFFFF ? s : 0xFFFF;                        // addsat16(FF00)
                t = t > 0xFF00 ? t - 0xFF00 : 0;                    // subsat16(FF00)
                var dVal = unchecked((ushort)delta[dOff + c]);      // sign-preserving reinterpret, like Python's `d & 0xFFFF`
                t = (t + dVal) & 0xFFFF;                            // add16
                sumC[c] = t;
                outpix[c] = t > 0xFF ? 0xFF : t;
            }

            dest[i] = outpix;
            prevPix = new[] { pr[0] & 0xFFFF, pr[1] & 0xFFFF, pr[2] & 0xFFFF, pr[3] & 0xFFFF };
        }
    }

    // Port of UnpackDeltaRLE16. Reads from data[start..end) instead of a fresh
    // slice per call (the Python version re-slices per tile-row); consumed is
    // relative to `start`, matching the original's "bytes consumed" contract.
    private static (short[] delta, int consumed) UnpackDeltaRle16(byte[] data, int start, int end, int pixelCount, int outChannels, int inChannels)
    {
        var n = end - start;
        var outArr = new short[(pixelCount + 135) * outChannels];
        var p = 0;
        var remaining = 0;
        ulong buf = 0;

        for (var ch = 0; ch < inChannels; ch++)
        {
            var cnt = 0;
            var widx = ch;

            while (true)
            {
                while (remaining < 32 && p < n)
                {
                    var rem = n - p;
                    ulong word;
                    int nb;
                    if (rem >= 4) { word = BitConverter.ToUInt32(data, start + p); p += 4; nb = 32; }
                    else if (rem >= 2) { word = BitConverter.ToUInt16(data, start + p); p += 2; nb = 16; }
                    else { word = data[start + p]; p += 1; nb = 8; }
                    buf |= word << remaining;
                    remaining += nb;
                }

                if (buf == 0) return (outArr, 0);

                var fsb = BitOperations.TrailingZeroCount(buf);
                var nextmask = buf >> (fsb + 1);
                var opcode = (2 * fsb) | (int)(nextmask & 1);
                remaining -= 2 + fsb;
                buf = nextmask >> 1;

                if (opcode == 0)
                {
                    outArr[widx] = 0;
                    cnt += 1; widx += outChannels;
                }
                else if (opcode <= 0xE)
                {
                    var bitValue = buf & ((1UL << opcode) - 1);
                    var sign = (buf >> opcode) & 1;
                    var x = (long)((1UL << opcode) | bitValue) - 1;
                    outArr[widx] = (short)(sign != 0 ? -x : x);
                    remaining -= opcode + 1;
                    buf >>= opcode + 1;
                    cnt += 1; widx += outChannels;
                }
                else // 0xF: zero-fill run
                {
                    var zfc = (int)(buf & 0x7F) + 8;
                    remaining -= 7;
                    buf >>= 7;
                    for (var i = 0; i < zfc; i++)
                        outArr[widx + i * outChannels] = 0;
                    cnt += zfc; widx += outChannels * zfc;
                }

                if (cnt >= pixelCount) break;
            }
        }

        var totalRead = p;
        var remainingBytes = remaining / 8;
        return (outArr, totalRead - remainingBytes);
    }
}
