using Pfim;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace mwb_materials.MwbMats
{
    class DdsLoader
    {
        public static Bitmap Load(string path)
        {
            IImage image = Pfimage.FromFile(path);

            try
            {
                PixelFormat pixelFormat;

                switch (image.Format)
                {
                    case Pfim.ImageFormat.Rgba32:
                        pixelFormat = PixelFormat.Format32bppArgb;
                        break;
                    case Pfim.ImageFormat.Rgb24:
                        pixelFormat = PixelFormat.Format24bppRgb;
                        break;
                    case Pfim.ImageFormat.Rgb8:
                        return LoadRgb8(image, path);
                    case Pfim.ImageFormat.R5g6b5:
                        pixelFormat = PixelFormat.Format16bppRgb565;
                        break;
                    case Pfim.ImageFormat.R5g5b5:
                        pixelFormat = PixelFormat.Format16bppRgb555;
                        break;
                    case Pfim.ImageFormat.R5g5b5a1:
                        pixelFormat = PixelFormat.Format16bppArgb1555;
                        break;
                    default:
                        throw new NotSupportedException(
                            "Unsupported source texture pixel format " + image.Format + " in " + Path.GetFileName(path) +
                            ". HDR/float formats (BC6H, R16F, R32F) and Rgba16 are not supported for material source textures.");
                }

                GCHandle handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);

                try
                {
                    using (Bitmap pinnedBitmap = new Bitmap(image.Width, image.Height, image.Stride, pixelFormat, handle.AddrOfPinnedObject()))
                    {
                        return new Bitmap(pinnedBitmap);
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
            finally
            {
                image.Dispose();
            }
        }

        public static void LoadRgbm(string path, out Bitmap albedo, out Bitmap metalness)
        {
            IImage image = Pfimage.FromFile(path);

            try
            {
                PixelFormat pixelFormat;
                bool sourceHasAlpha;

                switch (image.Format)
                {
                    case Pfim.ImageFormat.Rgba32:
                        pixelFormat = PixelFormat.Format32bppArgb;
                        sourceHasAlpha = true;
                        break;
                    case Pfim.ImageFormat.Rgb24:
                        pixelFormat = PixelFormat.Format24bppRgb;
                        sourceHasAlpha = false;
                        break;
                    case Pfim.ImageFormat.Rgb8:
                        using (Bitmap indexedBitmap = LoadRgb8(image, path))
                        {
                            SplitRgbmBitmap(indexedBitmap, false, out albedo, out metalness);
                        }
                        return;
                    case Pfim.ImageFormat.R5g6b5:
                        pixelFormat = PixelFormat.Format16bppRgb565;
                        sourceHasAlpha = false;
                        break;
                    case Pfim.ImageFormat.R5g5b5:
                        pixelFormat = PixelFormat.Format16bppRgb555;
                        sourceHasAlpha = false;
                        break;
                    case Pfim.ImageFormat.R5g5b5a1:
                        pixelFormat = PixelFormat.Format16bppArgb1555;
                        sourceHasAlpha = true;
                        break;
                    default:
                        throw new NotSupportedException(
                            "Unsupported source texture pixel format " + image.Format + " in " + Path.GetFileName(path) +
                            ". HDR/float formats (BC6H, R16F, R32F) and Rgba16 are not supported for material source textures.");
                }

                GCHandle handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);

                try
                {
                    using (Bitmap pinnedBitmap = new Bitmap(image.Width, image.Height, image.Stride, pixelFormat, handle.AddrOfPinnedObject()))
                    {
                        SplitRgbmBitmap(pinnedBitmap, sourceHasAlpha, out albedo, out metalness);
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
            finally
            {
                image.Dispose();
            }
        }

        public static void SplitRgbmBitmap(Bitmap source, bool sourceHasAlpha, out Bitmap albedo, out Bitmap metalness)
        {
            albedo = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            metalness = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color color = source.GetPixel(x, y);
                    byte metal = sourceHasAlpha ? color.A : (byte)0;

                    albedo.SetPixel(x, y, Color.FromArgb(255, color.R, color.G, color.B));
                    metalness.SetPixel(x, y, Color.FromArgb(255, metal, metal, metal));
                }
            }
        }

        private static Bitmap LoadRgb8(IImage image, string path)
        {
            GCHandle handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);

            try
            {
                using (Bitmap pinnedBitmap = new Bitmap(image.Width, image.Height, image.Stride, PixelFormat.Format8bppIndexed, handle.AddrOfPinnedObject()))
                {
                    ColorPalette palette = pinnedBitmap.Palette;

                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }

                    pinnedBitmap.Palette = palette;

                    return new Bitmap(pinnedBitmap);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        public static bool IsDds(string path)
        {
            return Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTga(string path)
        {
            return Path.GetExtension(path).Equals(".tga", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPfimSupportedSource(string path)
        {
            return IsDds(path) || IsTga(path);
        }
    }
}
