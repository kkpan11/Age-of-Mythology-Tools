﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace AoMEngineLibrary.Graphics.Ddt
{
    public class DdtImageConverter
    {
        public Texture Convert(DdtFile ddt)
        {
            var numFaces = ddt.Properties.HasFlag(DdtProperty.CubeMap) ? 6 : 1;
            var images = new Image[numFaces][];

            if (ddt.Format == DdtFormat.Dxt1 && ddt.AlphaBits == 0)
            {
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt1(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgb24>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.Dxt1 && ddt.AlphaBits == 1)
            {
                // AoMTT does not support DXT1 textures with 1-bit alpha
                // AoMEE supports DXT1 textures with 1-bit alpha
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt1Alpha(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.Dxt1Alpha && ddt.AlphaBits == 1)
            {
                // AoMTT uses this custom DXT1 based format for 1-bit alpha
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt1AlphaCustom(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.BC2) // aka DXT3
            {
                // AoMEE only, supports 4-bit alpha, but some ddt have 1 for ddt.AlphaBits by mistake
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt3(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.Dxt3Swizzled && ddt.AlphaBits == 4) // DXT3 using 444 colors
            {
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt3Swizzled(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.BC3) // aka DXT5
            {
                // AoMEE only, supports 4-bit alpha, but some ddt have 0/1 for ddt.AlphaBits by mistake
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecompressDxt5(ddt.Data[face][mip], width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.BT8 && ddt.AlphaBits == 0)
            {
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecodeBt8As565(ddt.Data[face][mip], ddt.Bt8PaletteBuffer, width, height);
                        var image = Image.LoadPixelData<Rgb24>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.BT8 && ddt.AlphaBits == 1)
            {
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecodeBt8As5551(ddt.Data[face][mip], ddt.Bt8PaletteBuffer, width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else if (ddt.Format == DdtFormat.BT8 && ddt.AlphaBits == 4)
            {
                for (int face = 0; face < numFaces; ++face)
                {
                    var faceImages = new Image[ddt.MipMapLevels];
                    images[face] = faceImages;
                    for (int mip = 0; mip < ddt.MipMapLevels; ++mip)
                    {
                        var width = Math.Max(1, ddt.Width >> mip);
                        var height = Math.Max(1, ddt.Height >> mip);
                        byte[] decompressedData = DecodeBt8As4444(ddt.Data[face][mip], ddt.Bt8PaletteBuffer, width, height);
                        var image = Image.LoadPixelData<Rgba32>(decompressedData, width, height);
                        image.Mutate(p => p.Flip(FlipMode.Vertical));
                        faceImages[mip] = image;
                    }
                }
            }
            else
            {
                throw new NotImplementedException($"Converting ddt format {ddt.Format} with {ddt.AlphaBits} alpha bits to image not implemented.");
            }

            return new Texture(images, !ddt.Properties.HasFlag(DdtProperty.NoAlphaTest));
        }

        private byte[] DecodeBt8As565(byte[] source, ushort[] palette, int width, int height)
        {
            // 3 bytes per pixel output
            byte[] dest = new byte[width * height * 3];

            int sourceIndex = 0, destIndex = 0;
            for (int row = 0; row < height; ++row)
            {
                for (int col = 0; col < width; ++col)
                {
                    var paletteIndex = source[sourceIndex++];
                    var color = palette[paletteIndex];

                    // Extract B5G6R5 (in that order)
                    byte b = (byte)((color & 0x1Fu));
                    byte g = (byte)((color & 0x7E0u) >> 5);
                    byte r = (byte)((color & 0xF800u) >> 11);
                    r = (byte)(r << 3 | r >> 2);
                    g = (byte)(g << 2 | g >> 3);
                    b = (byte)(b << 3 | b >> 2);

                    dest[destIndex++] = r;
                    dest[destIndex++] = g;
                    dest[destIndex++] = b;
                }
            }

            return dest;
        }

        private byte[] DecodeBt8As5551(byte[] source, ushort[] palette, int width, int height)
        {
            // 4 bytes per pixel output
            byte[] dest = new byte[width * height * 4];

            int sourceIndex = 0, destIndex = 0;
            for (int row = 0; row < height; ++row)
            {
                for (int col = 0; col < width; ++col)
                {
                    var paletteIndex = source[sourceIndex++];
                    var color = palette[paletteIndex];

                    // Extract B5G5R5A1 (in that order)
                    byte b = (byte)((color & 0x1Fu));
                    byte g = (byte)((color & 0x3E0u) >> 5);
                    byte r = (byte)((color & 0x7C00u) >> 10);
                    byte a = (byte)((color & 0x8000u) >> 15);
                    r = (byte)(r << 3 | r >> 2);
                    g = (byte)(g << 3 | g >> 2);
                    b = (byte)(b << 3 | b >> 2);
                    a *= byte.MaxValue;

                    dest[destIndex++] = r;
                    dest[destIndex++] = g;
                    dest[destIndex++] = b;
                    dest[destIndex++] = a;
                }
            }

            return dest;
        }

        private byte[] DecodeBt8As4444(ReadOnlySpan<byte> source, ushort[] palette, int width, int height)
        {
            // 4 bytes per pixel output
            byte[] dest = new byte[width * height * 4];

            int sourceIndex = 0, destIndex = 0;
            for (int row = 0; row < height; ++row)
            {
                for (int col = 0; col < width; col += 4)
                {
                    ushort alpha = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(sourceIndex));
                    sourceIndex += 2;

                    Decode4444((ushort)(palette[source[sourceIndex++]] | ((alpha & 0x00F0u) << 8)), dest, ref destIndex);
                    Decode4444((ushort)(palette[source[sourceIndex++]] | ((alpha & 0x000Fu) << 12)), dest, ref destIndex);
                    Decode4444((ushort)(palette[source[sourceIndex++]] | ((alpha & 0xF000u))), dest, ref destIndex);
                    Decode4444((ushort)(palette[source[sourceIndex++]] | ((alpha & 0x0F00u) << 4)), dest, ref destIndex);
                }
            }

            return dest;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void Decode4444(ushort color, byte[] dest, ref int destIndex)
            {
                // Extract B4G4R4A4 (in that order)
                byte b = (byte)((color & 0x0Fu));
                byte g = (byte)((color & 0xF0u) >> 4);
                byte r = (byte)((color & 0xF00u) >> 8);
                byte a = (byte)((color & 0xF000u) >> 12);
                r = (byte)(r << 4 | r);
                g = (byte)(g << 4 | g);
                b = (byte)(b << 4 | b);
                a = (byte)(a << 4 | a);

                dest[destIndex++] = r;
                dest[destIndex++] = g;
                dest[destIndex++] = b;
                dest[destIndex++] = a;
            }
        }

        private static int CalcBlocks(int pixels) => Math.Max(1, (pixels + 3) / 4);

        private delegate int DecodeDelegate(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride);

        private byte[] Decode(byte[] memBuffer, int width, int height, int blockDivSize, int blockPixelDepthBytes, DecodeDelegate decode)
        {
            int HeightBlocks = CalcBlocks(height);
            int WidthBlocks = CalcBlocks(width);
            int StridePixels = WidthBlocks * blockDivSize;
            int DeflatedStrideBytes = StridePixels * blockPixelDepthBytes;
            int DataLen = HeightBlocks * blockDivSize * DeflatedStrideBytes;
            byte[] data = new byte[DataLen];

            int pixelsLeft = (int)data.Length;
            int dataIndex = 0;
            int bIndex = 0;


            int stridePixels = WidthBlocks * blockDivSize;
            int stride = stridePixels * blockPixelDepthBytes;
            int blocksPerStride = WidthBlocks;
            int indexPixelsLeft = HeightBlocks * blockDivSize * stride;


            while (indexPixelsLeft > 0)
            {
                int origDataIndex = dataIndex;

                for (int i = 0; i < blocksPerStride; i++)
                {
                    bIndex = decode.Invoke(memBuffer, data, bIndex, (int)dataIndex, (int)stridePixels);
                    dataIndex += (int)(blockDivSize * blockPixelDepthBytes);
                }

                int filled = stride * blockDivSize;
                pixelsLeft -= filled;
                indexPixelsLeft -= filled;

                // Jump down to the block that is exactly (divSize - 1)
                // below the current row we are on
                dataIndex = origDataIndex + filled;
            }

            return data;
        }

        private byte[] DecompressDxt1(byte[] blockData, int width, int height)
        {
            const byte PixelDepthBytes = 3;
            const byte DivSize = 4;
            var colors = new Rgb24[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt1);

            int DecodeDxt1(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B5G6R5 (in that order)
                colors[0].B = (byte)((color0 & 0x1fu));
                colors[0].G = (byte)((color0 & 0x7E0u) >> 5);
                colors[0].R = (byte)((color0 & 0xF800u) >> 11);
                colors[0].R = (byte)(colors[0].R << 3 | colors[0].R >> 2);
                colors[0].G = (byte)(colors[0].G << 2 | colors[0].G >> 3);
                colors[0].B = (byte)(colors[0].B << 3 | colors[0].B >> 2);

                colors[1].B = (byte)((color1 & 0x1fu));
                colors[1].G = (byte)((color1 & 0x7E0u) >> 5);
                colors[1].R = (byte)((color1 & 0xF800u) >> 11);
                colors[1].R = (byte)(colors[1].R << 3 | colors[1].R >> 2);
                colors[1].G = (byte)(colors[1].G << 2 | colors[1].G >> 3);
                colors[1].B = (byte)(colors[1].B << 3 | colors[1].B >> 2);

                // Used the two extracted colors to create two new colors that are
                // slightly different.
                if (color0 > color1)
                {
                    colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                    colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                    colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);

                    colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                    colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                    colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);
                }
                else
                {
                    colors[2].R = (byte)((colors[0].R + colors[1].R) / 2);
                    colors[2].G = (byte)((colors[0].G + colors[1].G) / 2);
                    colors[2].B = (byte)((colors[0].B + colors[1].B) / 2);

                    colors[3].R = 0;
                    colors[3].G = 0;
                    colors[3].B = 0;
                }


                for (int i = 0; i < 4; i++)
                {
                    // Every 2 bit is a code [0-3] and represent what color the
                    // current pixel is

                    // Read in a byte and thus 4 colors
                    byte rowVal = blockData[streamIndex++];
                    for (int j = 0; j < 8; j += 2)
                    {
                        // Extract code by shifting the row byte so that we can
                        // AND it with 3 and get a value [0-3]
                        var col = colors[(rowVal >> j) & 0x03];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                    }

                    // Jump down a row and start at the beginning of the row
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }

        private byte[] DecompressDxt1Alpha(byte[] blockData, int width, int height)
        {
            const byte PixelDepthBytes = 4;
            const byte DivSize = 4;
            var colors = new Rgba32[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt1);

            int DecodeDxt1(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B5G6R5 (in that order)
                colors[0].B = (byte)((color0 & 0x1fu));
                colors[0].G = (byte)((color0 & 0x7E0u) >> 5);
                colors[0].R = (byte)((color0 & 0xF800u) >> 11);
                colors[0].R = (byte)(colors[0].R << 3 | colors[0].R >> 2);
                colors[0].G = (byte)(colors[0].G << 2 | colors[0].G >> 3);
                colors[0].B = (byte)(colors[0].B << 3 | colors[0].B >> 2);
                colors[0].A = byte.MaxValue;

                colors[1].B = (byte)((color1 & 0x1fu));
                colors[1].G = (byte)((color1 & 0x7E0u) >> 5);
                colors[1].R = (byte)((color1 & 0xF800u) >> 11);
                colors[1].R = (byte)(colors[1].R << 3 | colors[1].R >> 2);
                colors[1].G = (byte)(colors[1].G << 2 | colors[1].G >> 3);
                colors[1].B = (byte)(colors[1].B << 3 | colors[1].B >> 2);
                colors[1].A = byte.MaxValue;

                // Used the two extracted colors to create two new colors that are
                // slightly different.
                if (color0 > color1)
                {
                    colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                    colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                    colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);
                    colors[2].A = byte.MaxValue;

                    colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                    colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                    colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);
                    colors[3].A = byte.MaxValue;
                }
                else
                {
                    colors[2].R = (byte)((colors[0].R + colors[1].R) / 2);
                    colors[2].G = (byte)((colors[0].G + colors[1].G) / 2);
                    colors[2].B = (byte)((colors[0].B + colors[1].B) / 2);
                    colors[2].A = byte.MaxValue;

                    colors[3].R = 0;
                    colors[3].G = 0;
                    colors[3].B = 0;
                    colors[3].A = 0;
                }


                for (int i = 0; i < 4; i++)
                {
                    // Every 2 bit is a code [0-3] and represent what color the
                    // current pixel is

                    // Read in a byte and thus 4 colors
                    byte rowVal = blockData[streamIndex++];
                    for (int j = 0; j < 8; j += 2)
                    {
                        // Extract code by shifting the row byte so that we can
                        // AND it with 3 and get a value [0-3]
                        var col = colors[(rowVal >> j) & 0x03];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                        data[dataIndex++] = col.A;
                    }

                    // Jump down a row and start at the beginning of the row
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }

        private byte[] DecompressDxt1AlphaCustom(byte[] blockData, int width, int height)
        {
            // Dxt1, but with output data having alpha
            const byte PixelDepthBytes = 4;
            const byte DivSize = 4;
            var colors = new Rgba32[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt1);

            int DecodeDxt1(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {
                // This is some weird B5G5R5A1 (least to most sig.) custom DXT1 based format
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B5G5R5A1 (in that order)
                colors[0].B = (byte)((color0 & 0x1fu));
                colors[0].G = (byte)((color0 & 0x3E0u) >> 5);
                colors[0].R = (byte)((color0 & 0x7C00u) >> 10);
                colors[0].A = (byte)((color0 & 0x8000u) >> 15);
                colors[0].R = (byte)(colors[0].R << 3 | colors[0].R >> 2);
                colors[0].G = (byte)(colors[0].G << 3 | colors[0].G >> 2);
                colors[0].B = (byte)(colors[0].B << 3 | colors[0].B >> 2);

                colors[1].B = (byte)((color1 & 0x1fu));
                colors[1].G = (byte)((color1 & 0x3E0u) >> 5);
                colors[1].R = (byte)((color1 & 0x7C00u) >> 10);
                colors[1].A = (byte)((color1 & 0x8000u) >> 15);
                colors[1].R = (byte)(colors[1].R << 3 | colors[1].R >> 2);
                colors[1].G = (byte)(colors[1].G << 3 | colors[1].G >> 2);
                colors[1].B = (byte)(colors[1].B << 3 | colors[1].B >> 2);

                // Used the two extracted colors to create two new colors that are
                // slightly different.
                colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);
                colors[2].A = (byte)(colors[0].A | colors[1].A);

                colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);
                colors[3].A = colors[2].A;

                // Alpha bits, 1 bit per pixel
                ushort alphaBits = (blockData[streamIndex++]);
                alphaBits |= (ushort)(blockData[streamIndex++] << 8);

                for (int i = 0; i < 4; i++)
                {
                    // Every 2 bit is a code [0-3] and represent what color the
                    // current pixel is

                    // Read in a byte and thus 4 colors
                    byte rowVal = blockData[streamIndex++];
                    for (int j = 0; j < 8; j += 2)
                    {
                        // Extract code by shifting the row byte so that we can
                        // AND it with 3 and get a value [0-3]
                        var col = colors[(rowVal >> j) & 0x03];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                        // not sure if I should be ORing the col.A bit with alphaBits, don't for now
                        data[dataIndex++] = (byte)(byte.MaxValue * ((alphaBits) & 0b1u));
                        alphaBits = (ushort)((uint)alphaBits >> 1);
                    }

                    // Jump down a row and start at the beginning of the row
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }

        private byte[] DecompressDxt3(byte[] blockData, int width, int height)
        {
            const byte PixelDepthBytes = 4;
            const byte DivSize = 4;
            var colors = new Rgb24[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt3);

            int DecodeDxt3(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {
                /* 
                 * Strategy for decompression:
                 * -We're going to decode both alpha and color at the same time 
                 * to save on space and time as we don't have to allocate an array 
                 * to store values for later use.
                 */

                // Remember where the alpha data is stored so we can decode simultaneously
                int alphaPtr = streamIndex;

                // Jump ahead to the color data
                streamIndex += 8;

                // Colors are stored in a pair of 16 bits
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B5G6R5 (in that order)
                colors[0].B = (byte)((color0 & 0x1fu));
                colors[0].G = (byte)((color0 & 0x7E0u) >> 5);
                colors[0].R = (byte)((color0 & 0xF800u) >> 11);
                colors[0].R = (byte)(colors[0].R << 3 | colors[0].R >> 2);
                colors[0].G = (byte)(colors[0].G << 2 | colors[0].G >> 3);
                colors[0].B = (byte)(colors[0].B << 3 | colors[0].B >> 2);

                colors[1].B = (byte)((color1 & 0x1fu));
                colors[1].G = (byte)((color1 & 0x7E0u) >> 5);
                colors[1].R = (byte)((color1 & 0xF800u) >> 11);
                colors[1].R = (byte)(colors[1].R << 3 | colors[1].R >> 2);
                colors[1].G = (byte)(colors[1].G << 2 | colors[1].G >> 3);
                colors[1].B = (byte)(colors[1].B << 3 | colors[1].B >> 2);

                // Used the two extracted colors to create two new colors
                // that are slightly different.
                colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);

                colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);

                for (int i = 0; i < 4; i++)
                {
                    byte rowVal = blockData[streamIndex++];

                    // Each row of rgb values have 4 alpha values that  are
                    // encoded in 4 bits
                    ushort rowAlpha = blockData[alphaPtr++];
                    rowAlpha |= (ushort)(blockData[alphaPtr++] << 8);

                    for (int j = 0; j < 8; j += 2)
                    {
                        byte currentAlpha = (byte)((rowAlpha >> (j * 2)) & 0x0f);
                        currentAlpha |= (byte)(currentAlpha << 4);
                        var col = colors[((rowVal >> j) & 0x03)];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                        data[dataIndex++] = currentAlpha;
                    }
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }

        private byte[] DecompressDxt3Swizzled(byte[] blockData, int width, int height)
        {
            const byte PixelDepthBytes = 4;
            const byte DivSize = 4;
            var colors = new Rgb24[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt3);

            int DecodeDxt3(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {
                // This is some weird B4G4R4A4 (least to most sig.) custom DXT3 based format with alpha after colors
                /* 
                 * Strategy for decompression:
                 * -We're going to decode both alpha and color at the same time 
                 * to save on space and time as we don't have to allocate an array 
                 * to store values for later use.
                 */

                // Colors are stored in a pair of 16 bits
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B4G4R4 (in that order)
                colors[0].B = (byte)((color0 & 0xFu));
                colors[0].G = (byte)((color0 & 0xF0u) >> 4);
                colors[0].R = (byte)((color0 & 0xF00u) >> 8);
                colors[0].R = (byte)(colors[0].R << 4 | colors[0].R);
                colors[0].G = (byte)(colors[0].G << 4 | colors[0].G);
                colors[0].B = (byte)(colors[0].B << 4 | colors[0].B);

                colors[1].B = (byte)((color1 & 0xFu));
                colors[1].G = (byte)((color1 & 0xF0u) >> 4);
                colors[1].R = (byte)((color1 & 0xF00u) >> 8);
                colors[1].R = (byte)(colors[1].R << 4 | colors[1].R);
                colors[1].G = (byte)(colors[1].G << 4 | colors[1].G);
                colors[1].B = (byte)(colors[1].B << 4 | colors[1].B);

                // Used the two extracted colors to create two new colors
                // that are slightly different.
                colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);

                colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);

                for (int i = 0; i < 4; i++)
                {
                    // Each row of rgb values have 4 alpha values that  are
                    // encoded in 4 bits
                    ushort rowAlpha = blockData[streamIndex++];
                    rowAlpha |= (ushort)(blockData[streamIndex++] << 8);

                    byte rowVal = blockData[streamIndex++];
                    for (int j = 0; j < 8; j += 2)
                    {
                        byte currentAlpha = (byte)((rowAlpha >> (j * 2)) & 0x0f);
                        currentAlpha |= (byte)(currentAlpha << 4);
                        var col = colors[((rowVal >> j) & 0x03)];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                        data[dataIndex++] = currentAlpha;
                    }
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }

        private static int Bc5ExtractGradient(Span<byte> gradient, Span<byte> stream, int bIndex)
        {
            byte endpoint0;
            byte endpoint1;
            gradient[0] = endpoint0 = stream[(int)bIndex++];
            gradient[1] = endpoint1 = stream[(int)bIndex++];

            if (endpoint0 > endpoint1)
            {
                for (int i = 1; i < 7; i++)
                    gradient[1 + i] = (byte)(((7 - i) * endpoint0 + i * endpoint1) / 7);
            }
            else
            {
                for (int i = 1; i < 5; ++i)
                    gradient[1 + i] = (byte)(((5 - i) * endpoint0 + i * endpoint1) / 5);
                gradient[6] = 0;
                gradient[7] = 255;
            }
            return bIndex;
        }
        private byte[] DecompressDxt5(byte[] blockData, int width, int height)
        {
            const byte PixelDepthBytes = 4;
            const byte DivSize = 4;
            var alpha = new byte[8];
            var colors = new Rgb24[4];

            return Decode(blockData, width, height, DivSize, PixelDepthBytes, DecodeDxt5);

            int DecodeDxt5(byte[] stream, byte[] data, int streamIndex, int dataIndex, int stride)
            {

                streamIndex = Bc5ExtractGradient(alpha, blockData, streamIndex);

                ulong alphaCodes = blockData[streamIndex++];
                alphaCodes |= ((ulong)blockData[streamIndex++] << 8);
                alphaCodes |= ((ulong)blockData[streamIndex++] << 16);
                alphaCodes |= ((ulong)blockData[streamIndex++] << 24);
                alphaCodes |= ((ulong)blockData[streamIndex++] << 32);
                alphaCodes |= ((ulong)blockData[streamIndex++] << 40);

                // Colors are stored in a pair of 16 bits
                ushort color0 = blockData[streamIndex++];
                color0 |= (ushort)(blockData[streamIndex++] << 8);

                ushort color1 = (blockData[streamIndex++]);
                color1 |= (ushort)(blockData[streamIndex++] << 8);

                // Extract B5G6R5 (in that order)
                colors[0].B = (byte)((color0 & 0x1fu));
                colors[0].G = (byte)((color0 & 0x7E0u) >> 5);
                colors[0].R = (byte)((color0 & 0xF800u) >> 11);
                colors[0].R = (byte)(colors[0].R << 3 | colors[0].R >> 2);
                colors[0].G = (byte)(colors[0].G << 2 | colors[0].G >> 3);
                colors[0].B = (byte)(colors[0].B << 3 | colors[0].B >> 2);

                colors[1].B = (byte)((color1 & 0x1fu));
                colors[1].G = (byte)((color1 & 0x7E0u) >> 5);
                colors[1].R = (byte)((color1 & 0xF800u) >> 11);
                colors[1].R = (byte)(colors[1].R << 3 | colors[1].R >> 2);
                colors[1].G = (byte)(colors[1].G << 2 | colors[1].G >> 3);
                colors[1].B = (byte)(colors[1].B << 3 | colors[1].B >> 2);

                colors[2].R = (byte)((2 * colors[0].R + colors[1].R) / 3);
                colors[2].G = (byte)((2 * colors[0].G + colors[1].G) / 3);
                colors[2].B = (byte)((2 * colors[0].B + colors[1].B) / 3);

                colors[3].R = (byte)((colors[0].R + 2 * colors[1].R) / 3);
                colors[3].G = (byte)((colors[0].G + 2 * colors[1].G) / 3);
                colors[3].B = (byte)((colors[0].B + 2 * colors[1].B) / 3);

                for (int alphaShift = 0; alphaShift < 48; alphaShift += 12)
                {
                    byte rowVal = blockData[streamIndex++];
                    for (int j = 0; j < 4; j++)
                    {
                        // 3 bits determine alpha index to use
                        byte alphaIndex = (byte)((alphaCodes >> (alphaShift + 3 * j)) & 0x07);
                        var col = colors[((rowVal >> (j * 2)) & 0x03)];
                        data[dataIndex++] = col.R;
                        data[dataIndex++] = col.G;
                        data[dataIndex++] = col.B;
                        data[dataIndex++] = alpha[alphaIndex];
                    }
                    dataIndex += PixelDepthBytes * (stride - DivSize);
                }

                return streamIndex;
            }
        }
    }
}
