using mwb_materials.MwbMats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mwb_materials
{
    /*
    normal map alpha - mask for phong intensity (this will be the roughness converted to RGB)
    basetexture alpha - metalness of course :))
    exponent - red channel roughness converted to RGB. green channel metalness, blue channel 100% white (or black, or even a bentley image if you feel so devious)
    */

    class MaterialManipulation
    {
        private static readonly string AlbedoNomenclature = "_rgb";
        private static readonly string AlbedoAltNomenclature = "_c";
        private static readonly string AlbedoMetalnessNomenclature = "_rgbm";
        private static readonly string CodAlbedoSpecNomenclature = "_s~";
        private static readonly string AmbientOcclusionNomenclature = "_o";
        private static readonly string AmbientOcclusionAltNomenclature = "_ao";
        private static readonly string RoughnessNomenclature = "_r";
        private static readonly string GlossNomenclature = "_g";
        private static readonly string MetalnessNomenclature = "_alpha";
        private static readonly string MetalnessAltNomenclature = "_m";
        private static readonly string NormalNomenclature = "_n";
        private static readonly string EmissiveNomenclature = "_e";
        private static readonly string AlphatestNomenclature = "_t";
        private static readonly string TranslucentNomenclature = "_opacity";
        private static readonly string PackedOrmNomenclature = "_orm";
        private static readonly string PackedRmaNomenclature = "_rma";
        private static readonly string PackedMraoNomenclature = "_mrao";
        private static readonly string CodNogPackedNgNomenclature = "packed_ng";
        private static readonly string CodNogPackedNogNomenclature = "packed_nog";
        private static readonly string CodNogNomenclature = "_nog";
        private static readonly string CodNogNormalNomenclature = "_n&";
        private static readonly string CodNogGlossNomenclature = "_g~";

        public enum TextureChannel
        {
            Blue,
            Green,
            Red,
            Alpha
        }

        public enum TextureOperation
        {
            Replace,
            Add,
            Subtract,
            Multiply,
            Divide
        }

        public enum OpacityMode
        {
            None,
            Alphatest,
            Translucent
        }

        public struct SourceTextureSet : IDisposable
        {
            public SourceTextureSet(Bitmap albedo, Bitmap exponent, Bitmap normal, Bitmap emissive, Color metallicColor, double averageRoughness, OpacityMode opacityMode, IntermediateTextureSet intermediates)
            {
                Albedo = albedo;
                Exponent = exponent;
                Normal = normal;
                Emissive = emissive;
                AverageMetallicColor = metallicColor;
                AverageRoughness = averageRoughness;
                OpacityMode = opacityMode;
                Intermediates = intermediates;
            }

            public Bitmap Albedo { get; }
            public Bitmap Exponent { get; }
            public Bitmap Normal { get; }
            public Bitmap Emissive { get; }
            public Color AverageMetallicColor { get; }
            public double AverageRoughness { get; }
            public OpacityMode OpacityMode { get; }
            public IntermediateTextureSet Intermediates { get; }

            public void Dispose()
            {
                Albedo?.Dispose();
                Exponent?.Dispose();
                Normal?.Dispose();
                Emissive?.Dispose();
                Intermediates?.Dispose();
            }
        }

        public sealed class IntermediateTextureSet : IDisposable
        {
            public IntermediateTextureSet(Bitmap ambientOcclusion, Bitmap gloss, Bitmap metalness)
            {
                AmbientOcclusion = ambientOcclusion;
                Gloss = gloss;
                Metalness = metalness;
            }

            public Bitmap AmbientOcclusion { get; }
            public Bitmap Gloss { get; }
            public Bitmap Metalness { get; }

            public void Dispose()
            {
                AmbientOcclusion?.Dispose();
                Gloss?.Dispose();
                Metalness?.Dispose();
            }
        }

        private static void DumpGrayscaleInChannel(FastBitmap src, FastBitmap grayscale, TextureChannel channel, TextureOperation operation = TextureOperation.Replace)
        {
            if (src == null || grayscale == null)
            {
                return;
            }

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                switch (operation)
                {
                    case TextureOperation.Replace:
                        src.Bytes[cursor + (int)channel] = (byte)grayscale.ReadGrayscale(cursor);
                        break;
                    case TextureOperation.Add:
                        src.Bytes[cursor + (int)channel] = (byte)Math.Min(src.Bytes[cursor + (int)channel] + (byte)grayscale.ReadGrayscale(cursor), 255);
                        break;
                    case TextureOperation.Subtract:
                        src.Bytes[cursor + (int)channel] = (byte)Math.Max(src.Bytes[cursor + (int)channel] - (byte)grayscale.ReadGrayscale(cursor), 0);
                        break;
                    case TextureOperation.Multiply:
                        float mul = grayscale.ReadGrayscale(cursor) / 255.0f;
                        float mulValue = src.Bytes[cursor + (int)channel] * mul;
                        src.Bytes[cursor + (int)channel] = (byte)Math.Min(mulValue, 255.0f);
                        break;
                    case TextureOperation.Divide:
                        float div = grayscale.ReadGrayscale(cursor) / 255.0f;
                        float divValue = src.Bytes[cursor + (int)channel] * (1.0f - div);
                        src.Bytes[cursor + (int)channel] = (byte)Math.Min(divValue, 255.0f);
                        break;
                }
            }
        }

        private static void DumpColorInChannel(FastBitmap src, byte color, TextureChannel channel)
        {
            if (src == null)
            {
                return;
            }

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                src.Bytes[cursor + (int)channel] = color;
            }
        }

        private static void Invert(FastBitmap src)
        {
            if (src == null)
            {
                return;
            }

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                src.Bytes[cursor] = (byte)(255 - src.Bytes[cursor]);
                src.Bytes[cursor + 1] = (byte)(255 - src.Bytes[cursor + 1]);
                src.Bytes[cursor + 2] = (byte)(255 - src.Bytes[cursor + 2]);
                src.Bytes[cursor + 3] = (byte)(255 - src.Bytes[cursor + 3]);
            }
        }

        private static void Invert(FastBitmap src, TextureChannel channel)
        {
            if (src == null)
            {
                return;
            }

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                src.Bytes[cursor + (int)channel] = (byte)(255 - src.Bytes[cursor + (int)channel]);
            }
        }

        private static void ApplyAmbientOcclusion(FastBitmap src, FastBitmap ao, float strength)
        {
            if (src == null || ao == null)
            {
                return;
            }

            strength = Math.Min(Math.Max(strength, 0.0f), 1.0f);

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                float gsValue = ao.ReadGrayscale(cursor);
                gsValue /= 255.0f;
                gsValue = 1.0f.Lerp(gsValue, strength);

                src.Bytes[cursor] = (byte)Math.Min(src.Bytes[cursor] * gsValue, 255.0f);
                src.Bytes[cursor + 1] = (byte)Math.Min(src.Bytes[cursor + 1] * gsValue, 255.0f);
                src.Bytes[cursor + 2] = (byte)Math.Min(src.Bytes[cursor + 2] * gsValue, 255.0f);
            }
        }

        private static FastBitmap CreateSourceAlbedo(FastBitmap albedo, FastBitmap ambientOcclusion, FastBitmap metalness, FastBitmap roughness, FastBitmap opacity, ref GenerateProperties props)
        {
            if (albedo == null)
            {
                return null;
            }

            FastBitmap sourceAlbedo = new FastBitmap(new Bitmap(albedo.Source.Width, albedo.Source.Height));
            sourceAlbedo.Start(ImageLockMode.ReadWrite);

            albedo.DumpInto(sourceAlbedo);

            if (ambientOcclusion != null)
            {
                ApplyAmbientOcclusion(sourceAlbedo, ambientOcclusion, props.AoAlbedoStrength);
            }

            if (opacity != null)
            {
                //opacity mask -> basetexture alpha (white = opaque, black = transparent)
                DumpGrayscaleInChannel(sourceAlbedo, opacity, TextureChannel.Alpha);
            }
            else if (metalness != null)
            {
                //color2
                for (int cursor = 0; cursor < sourceAlbedo.Bytes.Length; cursor += 4)
                {
                    float metal = metalness.ReadGrayscale(cursor);
                    metal /= 255.0f;

                    float metallic = 255f;
                    float nonMetallic = 0f;

                    float result = nonMetallic.Lerp(metallic, metal);
                    sourceAlbedo.Bytes[cursor + (int)TextureChannel.Alpha] = (byte)result;
                }
            }
            else
            {
                DumpColorInChannel(sourceAlbedo, 0, TextureChannel.Alpha);
            }

            sourceAlbedo.Stop();
            return sourceAlbedo;
        }

        private static FastBitmap CreateSourceNormal(FastBitmap normal, FastBitmap albedo, FastBitmap roughness, FastBitmap metalness, FastBitmap ambientOcclusion, ref GenerateProperties props)
        {
            if (normal == null)
            {
                return null;
            }

            FastBitmap sourceNormal = new FastBitmap(new Bitmap(normal.Source.Width, normal.Source.Height));
            sourceNormal.Start(ImageLockMode.ReadWrite);

            normal.DumpInto(sourceNormal);

            if (roughness != null)
            {
                //phong
                DumpGrayscaleInChannel(sourceNormal, roughness, TextureChannel.Alpha);

                for (int cursor = 0; cursor < sourceNormal.Bytes.Length; cursor += 4)
                {
                    double delta = sourceNormal.Bytes[cursor + (int)TextureChannel.Alpha] / 255.0;
                    delta = Math.Pow(delta, 2.5);
                    sourceNormal.Bytes[cursor + (int)TextureChannel.Alpha] = (byte)Math.Min((delta * 255.0) + 1.0, 255.0);
                }

                if (props.bAoMasks)
                {
                    DumpGrayscaleInChannel(sourceNormal, ambientOcclusion, TextureChannel.Alpha, TextureOperation.Multiply);
                }
            }

            sourceNormal.Stop();
            return sourceNormal;
        }

        private static FastBitmap CreateSourceExponent(FastBitmap roughness, FastBitmap metalness, FastBitmap ambientOcclusion, ref GenerateProperties props)
        {
            if (roughness == null && metalness == null)
            {
                return null;
            }

            Bitmap target = (roughness != null) ? roughness.Source : metalness.Source;

            FastBitmap sourceExponent = new FastBitmap(new Bitmap(target.Width, target.Height));
            sourceExponent.Start(ImageLockMode.ReadWrite);

            if (roughness != null)
            {
                //phong exponent
                DumpGrayscaleInChannel(sourceExponent, roughness, TextureChannel.Red);

                for (int cursor = 0; cursor < sourceExponent.Bytes.Length; cursor += 4)
                {
                    double delta = sourceExponent.Bytes[cursor + (int)TextureChannel.Red] / 255.0;
                    delta = Math.Pow(delta, 4.0);

                    if (metalness != null)
                    {
                        delta *= 1.0f.Lerp(0.5f, metalness.ReadGrayscale(cursor) / 255.0f);
                    }

                    sourceExponent.Bytes[cursor + (int)TextureChannel.Red] = (byte)Math.Min((delta * 255.0) + 1.0, 255.0);
                }

                //rimlight
                DumpGrayscaleInChannel(sourceExponent, roughness, TextureChannel.Alpha);

                if (props.bAoMasks)
                {
                    DumpGrayscaleInChannel(sourceExponent, ambientOcclusion, TextureChannel.Alpha, TextureOperation.Multiply);
                }
            }

            if (metalness != null)
            {
                //phong albedo tint
                DumpGrayscaleInChannel(sourceExponent, metalness, TextureChannel.Green); 
            }

            sourceExponent.Stop();
            return sourceExponent;
        }

        private static FastBitmap LoadImage(string file)
        {
            if (DdsLoader.IsPfimSupportedSource(file))
            {
                return new FastBitmap(DdsLoader.Load(file));
            }

            using (Image image = Image.FromFile(file))
            {
                return new FastBitmap(new Bitmap(image));
            }
        }

        public struct GenerateProperties
        {
            public bool bAoMasks { get; internal set; }
            public bool bOpenGlNormal { get; internal set; }
            public bool bInvertNormalBlue { get; internal set; }
            public bool bInvertOpacity { get; internal set; }
            public bool bKeepIntermediates { get; internal set; }
            public int ClampSize { get; internal set; }
            public float AoAlbedoStrength { get; internal set; }
            public Action<string> LogFunc { get; internal set; }
        }

        private struct TextureStats
        {
            public int Min;
            public int Max;
            public double Average;

            public override string ToString()
            {
                return Min.ToString(CultureInfo.InvariantCulture) + "/" +
                    Average.ToString("0.0", CultureInfo.InvariantCulture) + "/" +
                    Max.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static TextureStats GetChannelStats(FastBitmap bmp, TextureChannel channel)
        {
            TextureStats stats = new TextureStats()
            {
                Min = 255,
                Max = 0,
                Average = 0.0
            };

            int count = 0;

            for (int cursor = 0; cursor < bmp.Bytes.Length; cursor += 4)
            {
                int value = bmp.Bytes[cursor + (int)channel];
                stats.Min = Math.Min(stats.Min, value);
                stats.Max = Math.Max(stats.Max, value);
                stats.Average += value;
                count++;
            }

            if (count > 0)
            {
                stats.Average /= count;
            }

            return stats;
        }

        private static TextureStats GetGrayscaleStats(FastBitmap bmp)
        {
            TextureStats stats = new TextureStats()
            {
                Min = 255,
                Max = 0,
                Average = 0.0
            };

            int count = 0;

            for (int cursor = 0; cursor < bmp.Bytes.Length; cursor += 4)
            {
                int value = bmp.ReadGrayscale(cursor);
                stats.Min = Math.Min(stats.Min, value);
                stats.Max = Math.Max(stats.Max, value);
                stats.Average += value;
                count++;
            }

            if (count > 0)
            {
                stats.Average /= count;
            }

            return stats;
        }

        private static void LogSourceReport(string file, string role, string channelsUsed, FastBitmap bmp, Action<string> logFunc)
        {
            if (logFunc == null || bmp == null)
            {
                return;
            }

            bmp.Start(ImageLockMode.ReadOnly);

            try
            {
                TextureStats grayscale = GetGrayscaleStats(bmp);
                TextureStats alpha = GetChannelStats(bmp, TextureChannel.Alpha);

                logFunc("Source " + Path.GetFileName(file) +
                    ": role=" + role +
                    ", size=" + bmp.Source.Width + "x" + bmp.Source.Height +
                    ", channels=" + channelsUsed +
                    ", gray min/avg/max=" + grayscale +
                    ", alpha min/avg/max=" + alpha);
            }
            finally
            {
                bmp.Stop();
            }
        }

        private static void LogSourceReportFromFile(string file, string role, string channelsUsed, Action<string> logFunc)
        {
            if (logFunc == null)
            {
                return;
            }

            FastBitmap bmp = LoadImage(file);

            try
            {
                LogSourceReport(file, role, channelsUsed, bmp, logFunc);
            }
            finally
            {
                bmp.Dispose();
            }
        }

        private static bool TryAssignTexture(ref FastBitmap current, ref string currentSource, ref int currentPriority,
            FastBitmap candidate, string role, string candidateSource, int candidatePriority, Action<string> logFunc)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                current = candidate;
                currentSource = candidateSource;
                currentPriority = candidatePriority;
                return true;
            }

            if (candidatePriority > currentPriority)
            {
                logFunc?.Invoke("Precedence: " + role + " using " + candidateSource + " over " + currentSource + ".");
                current.Dispose();
                current = candidate;
                currentSource = candidateSource;
                currentPriority = candidatePriority;
                return true;
            }

            logFunc?.Invoke("Precedence: " + role + " keeping " + currentSource + "; ignoring " + candidateSource + ".");
            candidate.Dispose();
            return false;
        }

        private static string DescribeTextureSource(string source)
        {
            return string.IsNullOrEmpty(source) ? "none" : source;
        }

        private static Bitmap CloneFastBitmap(FastBitmap src)
        {
            if (src == null)
            {
                return null;
            }

            FastBitmap clone = new FastBitmap(new Bitmap(src.Source.Width, src.Source.Height));
            clone.Start(ImageLockMode.ReadWrite);
            src.DumpInto(clone);
            clone.Stop();
            return clone.Source;
        }

        private static FastBitmap ExtractChannel(FastBitmap src, TextureChannel channel)
        {
            if (src == null)
            {
                return null;
            }

            FastBitmap result = new FastBitmap(new Bitmap(src.Source.Width, src.Source.Height));
            result.Start(ImageLockMode.ReadWrite);

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                byte value = src.Bytes[cursor + (int)channel];
                result.Bytes[cursor] = value;
                result.Bytes[cursor + 1] = value;
                result.Bytes[cursor + 2] = value;
                result.Bytes[cursor + 3] = 255;
            }

            result.Stop();
            return result;
        }

        private static void SplitPackedTexture(FastBitmap packed, string nomenclature,
            ref FastBitmap ao, ref FastBitmap roughness, ref FastBitmap metalness)
        {
            packed.Start(ImageLockMode.ReadOnly);

            if (nomenclature == PackedOrmNomenclature)
            {
                ao = ExtractChannel(packed, TextureChannel.Red);
                roughness = ExtractChannel(packed, TextureChannel.Green);
                metalness = ExtractChannel(packed, TextureChannel.Blue);
            }
            else if (nomenclature == PackedRmaNomenclature)
            {
                roughness = ExtractChannel(packed, TextureChannel.Red);
                metalness = ExtractChannel(packed, TextureChannel.Green);
                ao = ExtractChannel(packed, TextureChannel.Blue);
            }
            else if (nomenclature == PackedMraoNomenclature)
            {
                metalness = ExtractChannel(packed, TextureChannel.Red);
                roughness = ExtractChannel(packed, TextureChannel.Green);
                ao = ExtractChannel(packed, TextureChannel.Blue);
            }

            packed.StopAndDispose();
        }

        private static bool IsCodNogTextureName(string name)
        {
            return name.Contains(CodNogPackedNgNomenclature) ||
                name.Contains(CodNogPackedNogNomenclature) ||
                name.EndsWith(CodNogNomenclature) ||
                name.Contains(CodNogNormalNomenclature) ||
                name.Contains(CodNogGlossNomenclature);
        }

        private static string GetCodNogRole(string name)
        {
            if (name.Contains(CodNogPackedNgNomenclature))
            {
                return CodNogPackedNgNomenclature;
            }

            if (name.Contains(CodNogPackedNogNomenclature))
            {
                return CodNogPackedNogNomenclature;
            }

            if (name.EndsWith(CodNogNomenclature))
            {
                return CodNogNomenclature;
            }

            if (name.Contains(CodNogNormalNomenclature))
            {
                return CodNogNormalNomenclature;
            }

            return CodNogGlossNomenclature;
        }

        private static bool IsRgbmTextureName(string name)
        {
            return name.EndsWith(AlbedoMetalnessNomenclature) ||
                name.Contains(CodAlbedoSpecNomenclature);
        }

        private static byte EncodeNormalComponent(float value)
        {
            value = (value * 0.5f) + 0.5f;
            value = Math.Min(Math.Max(value, 0.0f), 1.0f);
            return (byte)Math.Min((value * 255.0f) + 0.5f, 255.0f);
        }

        private static FastBitmap CreateCodNogNormal(FastBitmap src)
        {
            FastBitmap result = new FastBitmap(new Bitmap(src.Source.Width, src.Source.Height));
            result.Start(ImageLockMode.ReadWrite);

            for (int cursor = 0; cursor < src.Bytes.Length; cursor += 4)
            {
                float normalX = (src.Bytes[cursor + (int)TextureChannel.Green] / 255.0f * 2.0f) - 1.0f;
                float normalY = (src.Bytes[cursor + (int)TextureChannel.Alpha] / 255.0f * 2.0f) - 1.0f;

                float x = (normalX + normalY) * 0.5f;
                float y = (normalX - normalY) * 0.5f;
                float z = 1.0f - Math.Abs(x) - Math.Abs(y);
                float length = (float)Math.Sqrt((x * x) + (y * y) + (z * z));

                if (length > 0.0f)
                {
                    x /= length;
                    y /= length;
                    z /= length;
                }

                result.Bytes[cursor] = EncodeNormalComponent(z);
                result.Bytes[cursor + 1] = EncodeNormalComponent(y);
                result.Bytes[cursor + 2] = EncodeNormalComponent(x);
                result.Bytes[cursor + 3] = 255;
            }

            result.Stop();
            return result;
        }

        private static void SplitCodNogTexture(FastBitmap packed,
            ref FastBitmap ao, ref FastBitmap gloss, ref FastBitmap normal)
        {
            packed.Start(ImageLockMode.ReadOnly);

            gloss = ExtractChannel(packed, TextureChannel.Red);
            ao = ExtractChannel(packed, TextureChannel.Blue);
            normal = CreateCodNogNormal(packed);

            packed.StopAndDispose();
        }

        private static string GetPackedChannelDescription(string nomenclature)
        {
            if (nomenclature == PackedOrmNomenclature)
            {
                return "R=AO, G=roughness, B=metalness";
            }

            if (nomenclature == PackedRmaNomenclature)
            {
                return "R=roughness, G=metalness, B=AO";
            }

            return "R=metalness, G=roughness, B=AO";
        }

        private static string GetPackedSourceDescription(string nomenclature, string role, string fileName)
        {
            if (nomenclature == PackedOrmNomenclature)
            {
                if (role == "ao")
                {
                    return "red(" + fileName + ")";
                }

                if (role == "roughness")
                {
                    return "green(" + fileName + ")";
                }

                return "blue(" + fileName + ")";
            }

            if (nomenclature == PackedRmaNomenclature)
            {
                if (role == "roughness")
                {
                    return "red(" + fileName + ")";
                }

                if (role == "metalness")
                {
                    return "green(" + fileName + ")";
                }

                return "blue(" + fileName + ")";
            }

            if (role == "metalness")
            {
                return "red(" + fileName + ")";
            }

            if (role == "roughness")
            {
                return "green(" + fileName + ")";
            }

            return "blue(" + fileName + ")";
        }

        private static void LoadRgbmTexture(string file, ref FastBitmap albedo, ref FastBitmap metalness)
        {
            Bitmap albedoBitmap;
            Bitmap metalnessBitmap;

            if (DdsLoader.IsPfimSupportedSource(file))
            {
                DdsLoader.LoadRgbm(file, out albedoBitmap, out metalnessBitmap);
            }
            else
            {
                using (Image image = Image.FromFile(file))
                using (Bitmap source = new Bitmap(image))
                {
                    bool sourceHasAlpha = Image.IsAlphaPixelFormat(source.PixelFormat);
                    DdsLoader.SplitRgbmBitmap(source, sourceHasAlpha, out albedoBitmap, out metalnessBitmap);
                }
            }

            albedo = new FastBitmap(albedoBitmap);
            metalness = new FastBitmap(metalnessBitmap);
        }

        private static void SetBiggestWidthAndHeight(ref int width, ref int height, FastBitmap bmp)
        {
            width = bmp.Source.Width > width ? bmp.Source.Width : width;
            height = bmp.Source.Height > height ? bmp.Source.Height : height;
        }

        private static void ResizeIfSmaller(FastBitmap bmp, int width, int height)
        {
            if (bmp == null)
            {
                return;
            }

            //technically shouldn't be bigger :D :(
            if (bmp.Source.Width >= width && bmp.Source.Height >= height)
            {
                return;
            }

            bmp.Resize(width, height);
        }

        private static void ResizeToClampSize(int clampSize, FastBitmap[] bitmaps)
        {
            foreach (FastBitmap bmp in bitmaps)
            {
                if (bmp == null)
                {
                    continue;
                }

                double ratioWidth = (double)clampSize / (double)bmp.Source.Width;
                double ratioHeight = (double)clampSize / (double)bmp.Source.Height;
                double ratio = ratioWidth < ratioHeight ? ratioWidth : ratioHeight;

                if (ratio < 1.0)
                {
                    bmp.Resize((int)(bmp.Source.Width * ratio), (int)(bmp.Source.Height * ratio));
                }
            }
        }

        private static Color GetAverageMetallicColor(FastBitmap albedo, FastBitmap metalness)
        {
            if (metalness == null || albedo == null)
            {
                return Color.FromArgb(25, 25, 25);
            }

            double red = 0.0;
            double green = 0.0;
            double blue = 0.0;

            for (int cursor = 0; cursor < albedo.Bytes.Length; cursor += 4)
            {
                double metal = metalness.ReadGrayscale(cursor) / 255.0;

                if (metal > 0.5)
                {
                    Color col = albedo.ReadColor(cursor);
                    red += col.R * metal;
                    green += col.G * metal;
                    blue += col.B * metal;
                }
                else
                {
                    red += 25.0;
                    green += 25.0;
                    blue += 25.0;
                }
            }

            red /= (albedo.Source.Width * albedo.Source.Height);
            green /= (albedo.Source.Width * albedo.Source.Height);
            blue /= (albedo.Source.Width * albedo.Source.Height);

            return Color.FromArgb((int)red, (int)green, (int)blue);
        }

        private static double GetAverageRoughness(FastBitmap roughness)
        {
            if (roughness == null)
            {
                return 0.5;
            }

            double avgRoughness = 0.0;

            for (int cursor = 0; cursor < roughness.Bytes.Length; cursor += 4)
            {
                avgRoughness += Math.Max(roughness.ReadGrayscale(cursor) / 255.0, 0.5);
            }

            avgRoughness /= (roughness.Source.Width * roughness.Source.Height);
            return avgRoughness;
        }

        public static async Task<SourceTextureSet> GenerateTextures(List<string> files, GenerateProperties props)
        {
            const int PackedPriority = 10;
            const int DerivedPriority = 20;
            const int ExplicitPriority = 30;

            FastBitmap albedo = null;
            FastBitmap ambientOcclusion = null;
            FastBitmap roughness = null;
            FastBitmap gloss = null;
            FastBitmap metalness = null;
            FastBitmap normal = null;
            FastBitmap emissive = null;
            FastBitmap alphatestOpacity = null;
            FastBitmap translucentOpacity = null;

            string albedoSource = null;
            string ambientOcclusionSource = null;
            string roughnessSource = null;
            string glossSource = null;
            string metalnessSource = null;
            string normalSource = null;
            string emissiveSource = null;
            string alphatestOpacitySource = null;
            string translucentOpacitySource = null;

            int albedoPriority = 0;
            int ambientOcclusionPriority = 0;
            int roughnessPriority = 0;
            int glossPriority = 0;
            int metalnessPriority = 0;
            int normalPriority = 0;
            int emissivePriority = 0;
            int alphatestOpacityPriority = 0;
            int translucentOpacityPriority = 0;

            int biggestWidth = 0;
            int biggestHeight = 0;

            foreach (string file in files.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                name = name.ToLower();
                string fileName = Path.GetFileName(file);

                if (IsCodNogTextureName(name))
                {
                    FastBitmap packed = LoadImage(file);
                    LogSourceReport(file, GetCodNogRole(name), "R=gloss, B=AO, G/A=normal", packed, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, packed);
                    FastBitmap packedAo = null;
                    FastBitmap packedGloss = null;
                    FastBitmap packedNormal = null;
                    SplitCodNogTexture(packed, ref packedAo, ref packedGloss, ref packedNormal);
                    TryAssignTexture(ref ambientOcclusion, ref ambientOcclusionSource, ref ambientOcclusionPriority, packedAo, "AO", "blue(" + fileName + ")", PackedPriority, props.LogFunc);
                    TryAssignTexture(ref gloss, ref glossSource, ref glossPriority, packedGloss, "gloss", "red(" + fileName + ")", PackedPriority, props.LogFunc);
                    TryAssignTexture(ref normal, ref normalSource, ref normalPriority, packedNormal, "normal", fileName + " (decoded NOG)", PackedPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(PackedOrmNomenclature) || name.EndsWith(PackedRmaNomenclature) || name.EndsWith(PackedMraoNomenclature))
                {
                    string packedType = name.EndsWith(PackedOrmNomenclature) ? PackedOrmNomenclature
                        : name.EndsWith(PackedRmaNomenclature) ? PackedRmaNomenclature
                        : PackedMraoNomenclature;

                    FastBitmap packed = LoadImage(file);
                    LogSourceReport(file, packedType + " packed", GetPackedChannelDescription(packedType), packed, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, packed);
                    FastBitmap packedAo = null;
                    FastBitmap packedRoughness = null;
                    FastBitmap packedMetalness = null;
                    SplitPackedTexture(packed, packedType, ref packedAo, ref packedRoughness, ref packedMetalness);
                    TryAssignTexture(ref ambientOcclusion, ref ambientOcclusionSource, ref ambientOcclusionPriority, packedAo, "AO", GetPackedSourceDescription(packedType, "ao", fileName), PackedPriority, props.LogFunc);
                    TryAssignTexture(ref roughness, ref roughnessSource, ref roughnessPriority, packedRoughness, "roughness", GetPackedSourceDescription(packedType, "roughness", fileName), PackedPriority, props.LogFunc);
                    TryAssignTexture(ref metalness, ref metalnessSource, ref metalnessPriority, packedMetalness, "metalness", GetPackedSourceDescription(packedType, "metalness", fileName), PackedPriority, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, ambientOcclusion);
                    continue;
                }

                if (IsRgbmTextureName(name))
                {
                    string rgbmRole = name.EndsWith(AlbedoMetalnessNomenclature) ? AlbedoMetalnessNomenclature : CodAlbedoSpecNomenclature;
                    LogSourceReportFromFile(file, rgbmRole, "RGB=albedo, A=metalness", props.LogFunc);
                    FastBitmap rgbmAlbedo = null;
                    FastBitmap rgbmMetalness = null;
                    LoadRgbmTexture(file, ref rgbmAlbedo, ref rgbmMetalness);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, rgbmAlbedo);
                    TryAssignTexture(ref albedo, ref albedoSource, ref albedoPriority, rgbmAlbedo, "albedo", "rgb(" + fileName + ")", DerivedPriority, props.LogFunc);
                    TryAssignTexture(ref metalness, ref metalnessSource, ref metalnessPriority, rgbmMetalness, "metalness", "alpha(" + fileName + ")", DerivedPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(AlbedoNomenclature) || name.EndsWith(AlbedoAltNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, name.EndsWith(AlbedoNomenclature) ? AlbedoNomenclature : AlbedoAltNomenclature, "RGB=albedo", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref albedo, ref albedoSource, ref albedoPriority, candidate, "albedo", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(AmbientOcclusionNomenclature) || name.EndsWith(AmbientOcclusionAltNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, name.EndsWith(AmbientOcclusionNomenclature) ? AmbientOcclusionNomenclature : AmbientOcclusionAltNomenclature, "grayscale/RGB=AO", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref ambientOcclusion, ref ambientOcclusionSource, ref ambientOcclusionPriority, candidate, "AO", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(RoughnessNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, RoughnessNomenclature, "grayscale/RGB=roughness, inverted to gloss", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref roughness, ref roughnessSource, ref roughnessPriority, candidate, "roughness", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(GlossNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, GlossNomenclature, "grayscale/RGB=gloss", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref gloss, ref glossSource, ref glossPriority, candidate, "gloss", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(MetalnessNomenclature) || name.EndsWith(MetalnessAltNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, name.EndsWith(MetalnessNomenclature) ? MetalnessNomenclature : MetalnessAltNomenclature, "grayscale/RGB=metalness", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref metalness, ref metalnessSource, ref metalnessPriority, candidate, "metalness", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(NormalNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, NormalNomenclature, "RGB=normal", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref normal, ref normalSource, ref normalPriority, candidate, "normal", fileName, ExplicitPriority, props.LogFunc);
                    continue;
                }

                if (name.EndsWith(EmissiveNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, EmissiveNomenclature, "RGB=emissive", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref emissive, ref emissiveSource, ref emissivePriority, candidate, "emissive", fileName, ExplicitPriority, props.LogFunc);
                }

                if (name.EndsWith(AlphatestNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, AlphatestNomenclature, "grayscale/RGB=alphatest opacity", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref alphatestOpacity, ref alphatestOpacitySource, ref alphatestOpacityPriority, candidate, "alphatest opacity", fileName, ExplicitPriority, props.LogFunc);
                }

                if (name.EndsWith(TranslucentNomenclature))
                {
                    FastBitmap candidate = LoadImage(file);
                    LogSourceReport(file, TranslucentNomenclature, "grayscale/RGB=translucent opacity", candidate, props.LogFunc);
                    SetBiggestWidthAndHeight(ref biggestWidth, ref biggestHeight, candidate);
                    TryAssignTexture(ref translucentOpacity, ref translucentOpacitySource, ref translucentOpacityPriority, candidate, "translucent opacity", fileName, ExplicitPriority, props.LogFunc);
                }
            }

            if (gloss != null && roughness != null)
            {
                props.LogFunc?.Invoke("Precedence: gloss using " + glossSource + "; roughness " + roughnessSource + " is ignored because a gloss map is present.");
            }

            //resolve opacity mode (prefer alphatest if both are present)
            FastBitmap opacity = null;
            OpacityMode opacityMode = OpacityMode.None;

            if (alphatestOpacity != null)
            {
                opacity = alphatestOpacity;
                opacityMode = OpacityMode.Alphatest;

                if (translucentOpacity != null)
                {
                    props.LogFunc?.Invoke("Precedence: opacity using alphatest " + alphatestOpacitySource + "; ignoring translucent " + translucentOpacitySource + ".");
                    translucentOpacity.Dispose();
                }
            }
            else if (translucentOpacity != null)
            {
                opacity = translucentOpacity;
                opacityMode = OpacityMode.Translucent;
            }

            string glossSummary = gloss != null
                ? glossSource
                : roughness != null ? "inverted roughness(" + roughnessSource + ")" : null;
            string opacitySummary = opacityMode == OpacityMode.Alphatest
                ? "alphatest " + alphatestOpacitySource
                : opacityMode == OpacityMode.Translucent ? "translucent " + translucentOpacitySource : null;

            props.LogFunc?.Invoke("Texture summary: albedo: " + DescribeTextureSource(albedoSource) +
                ", metalness: " + DescribeTextureSource(metalnessSource) +
                ", normal: " + DescribeTextureSource(normalSource) +
                ", gloss: " + DescribeTextureSource(glossSummary) +
                ", AO: " + DescribeTextureSource(ambientOcclusionSource) +
                ", opacity: " + DescribeTextureSource(opacitySummary) +
                ", emissive: " + DescribeTextureSource(emissiveSource));

            //resize textures
            biggestWidth = Math.Min(props.ClampSize, biggestWidth);
            biggestHeight = Math.Min(props.ClampSize, biggestHeight);

            ResizeIfSmaller(albedo, biggestWidth, biggestHeight);
            ResizeIfSmaller(ambientOcclusion, biggestWidth, biggestHeight);
            ResizeIfSmaller(roughness, biggestWidth, biggestHeight);
            ResizeIfSmaller(gloss, biggestWidth, biggestHeight);
            ResizeIfSmaller(metalness, biggestWidth, biggestHeight);
            ResizeIfSmaller(normal, biggestWidth, biggestHeight);
            ResizeIfSmaller(emissive, biggestWidth, biggestHeight);
            ResizeIfSmaller(opacity, biggestWidth, biggestHeight);

            ResizeToClampSize(props.ClampSize, new FastBitmap[] { albedo, ambientOcclusion, roughness, gloss, metalness, normal, emissive, opacity });

            //invert roughness
            Task roughnessTask = Task.Run(() =>
            {
                roughness?.Start(ImageLockMode.ReadWrite);
                Invert(roughness);
                roughness?.Stop();
            });

            Task normalOpenGlTask = Task.CompletedTask;

            if (props.bOpenGlNormal || props.bInvertNormalBlue)
            {
                normalOpenGlTask = Task.Run(() =>
                {
                    normal?.Start(ImageLockMode.ReadWrite);

                    if (props.bOpenGlNormal)
                    {
                        Invert(normal, TextureChannel.Green);
                    }

                    if (props.bInvertNormalBlue)
                    {
                        Invert(normal, TextureChannel.Blue);
                    }

                    normal?.Stop();
                });  
            }

            Task opacityInvertTask = Task.CompletedTask;

            if (props.bInvertOpacity)
            {
                opacityInvertTask = Task.Run(() =>
                {
                    opacity?.Start(ImageLockMode.ReadWrite);
                    Invert(opacity);
                    opacity?.Stop();
                });
            }

            await normalOpenGlTask; await roughnessTask; await opacityInvertTask;

            //start edits
            albedo?.Start(ImageLockMode.ReadOnly);
            ambientOcclusion?.Start(ImageLockMode.ReadOnly);
            roughness?.Start(ImageLockMode.ReadOnly);
            gloss?.Start(ImageLockMode.ReadOnly);
            metalness?.Start(ImageLockMode.ReadOnly);
            normal?.Start(ImageLockMode.ReadOnly);
            opacity?.Start(ImageLockMode.ReadOnly);

            Task<FastBitmap> albedoTask = Task.Run(() =>
            {
                return CreateSourceAlbedo(albedo, ambientOcclusion, metalness, (gloss != null) ? gloss : roughness, opacity, ref props);
            });

            Task<FastBitmap> normalTask = Task.Run(() =>
            {
                return CreateSourceNormal(normal, albedo, (gloss != null) ? gloss : roughness, metalness, ambientOcclusion, ref props);
            });

            Task<FastBitmap> exponentTask = Task.Run(() =>
            {
                return CreateSourceExponent((gloss != null) ? gloss : roughness, metalness, ambientOcclusion, ref props);
            });

            Task<Color> getMetallicColor = Task.Run(() =>
            {
                return GetAverageMetallicColor(albedo, metalness);
            });

            Task<double> getAverageRoughness = Task.Run(() =>
            {
                return GetAverageRoughness((gloss != null) ? gloss : roughness);
            });

            FastBitmap sourceAlbedo = await albedoTask;
            FastBitmap sourceNormal = await normalTask;
            FastBitmap sourceExponent = await exponentTask;
            Color averageMetallicColor = await getMetallicColor;
            double averageRoughness = await getAverageRoughness;
            IntermediateTextureSet intermediates = null;

            if (props.bKeepIntermediates)
            {
                intermediates = new IntermediateTextureSet(
                    CloneFastBitmap(ambientOcclusion),
                    CloneFastBitmap((gloss != null) ? gloss : roughness),
                    CloneFastBitmap(metalness));
            }

            //stop edits
            albedo?.StopAndDispose();
            ambientOcclusion?.StopAndDispose();
            roughness?.StopAndDispose();
            gloss?.StopAndDispose();
            metalness?.StopAndDispose();
            normal?.StopAndDispose();
            opacity?.StopAndDispose();

            return new SourceTextureSet(sourceAlbedo?.Source, sourceExponent?.Source, sourceNormal?.Source, emissive?.Source, averageMetallicColor, averageRoughness, opacityMode, intermediates);
        }
    }
}
