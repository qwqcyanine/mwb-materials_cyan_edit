using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace mwb_materials.MwbMats
{
    class BatchExporter
    { 
        public struct BatchProperties
        {
            public string VmtRootPath { get; internal set; }
            public string EnvRootPath { get; internal set; }
            public bool bMoveOutput { get; internal set; }
            public bool bIncludeFolders { get; internal set; }
            public MaterialManipulation.GenerateProperties GenerateProps { get; set; }
            public string AlbedoCompression { get; internal set; }
            public string NormalCompression { get; internal set; }
            public string ExponentCompression { get; internal set; }
            public bool bAlbedoMipMaps { get; internal set; }
            public bool bNormalMipMaps { get; internal set; }
            public bool bExponentMipMaps { get; internal set; }
            public bool bKeepIntermediates { get; internal set; }
            public bool bUseModelMaterialNames { get; internal set; }
            public float AlphatestReference { get; internal set; }
            public VmtPreset VmtPreset { get; internal set; }
            public Action<string> LogFunc { get; internal set; }
        }

        private static async Task GenerateInFolder(string path, BatchProperties props, string startPath, Action<string, List<string>> progressFunc)
        {
            string[] folders = Directory.GetDirectories(path)
                .Where(folder => !IsBatchIgnoredFolder(folder))
                .ToArray();

            List<TextureGenerationJob> generatedJobs = null;
            bool generateBeforeChildren = props.bUseModelMaterialNames && HasGltfFile(path);

            if (generateBeforeChildren)
            {
                generatedJobs = await GenerateCurrentFolder(path, props, startPath, progressFunc);
            }

            //before we do files, we have to first look in other folders, we can't run the tool
            //more than once or we are gonna eat a lot of memory
            foreach (string folder in folders)
            {
                if (IsTextureFolderClaimedByGeneratedJobs(folder, generatedJobs))
                {
                    props.LogFunc?.Invoke("Skipping " + folder + " because its textures were claimed by a model material binding.");
                    continue;
                }

                await GenerateInFolder(folder, props, startPath, progressFunc);
            }

            if (!generateBeforeChildren)
            {
                await GenerateCurrentFolder(path, props, startPath, progressFunc);
            }
        }

        private static async Task<List<TextureGenerationJob>> GenerateCurrentFolder(string path, BatchProperties props, string startPath, Action<string, List<string>> progressFunc)
        {
            //after the recursion is done we can hopefully do images
            string[] files = Directory.GetFiles(path);
            List<string> sanitizedFiles = new List<string>();

            foreach (string file in files)
            {
                if (DdsLoader.IsPfimSupportedSource(file))
                {
                    sanitizedFiles.Add(file);
                    continue;
                }

                try
                {
                    using (Image.FromFile(file))
                    {
                    }
                }
                catch (OutOfMemoryException)
                {
                    continue;
                }

                sanitizedFiles.Add(file);
            }

            if (sanitizedFiles.Count <= 0 && !props.bUseModelMaterialNames)
            {
                return new List<TextureGenerationJob>();
            }

            string folderName = Path.GetFileName(path);
            List<TextureGenerationJob> jobs = props.bUseModelMaterialNames
                ? ModelMaterialResolver.Resolve(path, startPath, sanitizedFiles, props.LogFunc)
                : new List<TextureGenerationJob>() { new TextureGenerationJob(folderName, string.Empty, folderName + ".vmt", folderName, sanitizedFiles) };

            jobs = jobs.Where(job => job.Files.Count > 0).ToList();

            if (jobs.Count <= 0)
            {
                return new List<TextureGenerationJob>();
            }

            string tempPath = Path.Combine(path, props.bKeepIntermediates ? "temp_debug" : "temp");
            string outputPath = Path.Combine(path, "output");

            PrepareTempDirectory(tempPath, props.LogFunc);
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(outputPath);

            try
            {
                foreach (TextureGenerationJob job in jobs)
                {
                    progressFunc(job.DisplayName, job.Files);
                    props.LogFunc?.Invoke("Processing " + job.DisplayName + " (" + job.Files.Count + " source textures)");
                    await GenerateJob(path, startPath, outputPath, tempPath, job, props);
                }
            }
            finally
            {
                if (props.bKeepIntermediates)
                {
                    props.LogFunc?.Invoke("Kept temp files in " + tempPath);
                }
                else
                {
                    TryDeleteTempDirectory(tempPath, props.LogFunc);
                }
            }

            return jobs;
        }

        private static bool IsBatchIgnoredFolder(string folder)
        {
            string folderNameOnly = Path.GetFileName(folder);

            return string.Equals(folderNameOnly, "output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderNameOnly, "temp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderNameOnly, "temp_debug", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasGltfFile(string folder)
        {
            return Directory.GetFiles(folder).Any(file =>
                string.Equals(Path.GetExtension(file), ".gltf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(file), ".glb", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTextureFolderClaimedByGeneratedJobs(string folder, List<TextureGenerationJob> generatedJobs)
        {
            if (generatedJobs == null || generatedJobs.Count == 0)
            {
                return false;
            }

            string folderRoot = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return generatedJobs
                .SelectMany(job => job.Files)
                .Any(file => Path.GetFullPath(file).StartsWith(folderRoot, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task GenerateJob(string path, string startPath, string outputRootPath, string tempPath, TextureGenerationJob job, BatchProperties props)
        {
            MaterialManipulation.SourceTextureSet textures = await MaterialManipulation.GenerateTextures(job.Files, props.GenerateProps);

            try
            {
                //resolve opacity-related settings
                MaterialManipulation.OpacityMode opacityMode = textures.OpacityMode;
                string effectiveAlbedoCompression = props.AlbedoCompression;

                if (opacityMode != MaterialManipulation.OpacityMode.None && textures.Albedo == null)
                {
                    props.LogFunc?.Invoke("Warning: opacity mask provided without albedo texture; opacity will be ignored.");
                    opacityMode = MaterialManipulation.OpacityMode.None;
                }

                if (opacityMode != MaterialManipulation.OpacityMode.None)
                {
                    if (effectiveAlbedoCompression == VtfCmdInterface.FormatDXT1)
                    {
                        effectiveAlbedoCompression = VtfCmdInterface.FormatDXT5;
                        props.LogFunc?.Invoke("Albedo compression upgraded from DXT1 to DXT5 (opacity requires alpha channel)");
                    }
                }

                if (props.bKeepIntermediates)
                {
                    SaveDebugTextures(textures, tempPath, job.TextureBaseName, props.LogFunc);
                }

                Dictionary<string, object> vmtValues = new Dictionary<string, object>();

                string outputName;
                string movePath = string.Empty;
                string detailName = string.Empty;
                string jobOutputPath = string.IsNullOrEmpty(job.RelativeFolder) ? outputRootPath : Path.Combine(outputRootPath, job.RelativeFolder);
                string qcMaterialDirectory = QcMaterialResolver.ResolveMaterialDirectory(path, startPath, props.LogFunc);

                Directory.CreateDirectory(jobOutputPath);

                if (!string.IsNullOrEmpty(qcMaterialDirectory))
                {
                    movePath = qcMaterialDirectory;

                    if (!string.IsNullOrEmpty(job.RelativeFolder))
                    {
                        movePath = Path.Combine(movePath, job.RelativeFolder);
                    }
                }
                else if (props.bMoveOutput && props.VmtRootPath != string.Empty)
                {
                    movePath = props.VmtRootPath;

                    if (props.bIncludeFolders)
                    {
                        movePath = CombineWithRelativeFolder(movePath, startPath, path);
                    }

                    if (!string.IsNullOrEmpty(job.RelativeFolder))
                    {
                        movePath = Path.Combine(movePath, job.RelativeFolder);
                    }
                }

                if (textures.Albedo != null)
                {
                    outputName = job.TextureBaseName + "_rgb";

                    string albedoPath = SaveTempTexture(textures.Albedo, tempPath, outputName);

                    vmtValues.Add("ALBEDONAME", outputName);

                    await ExportTempTexture(albedoPath, textures.Albedo, tempPath, outputName, jobOutputPath, effectiveAlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
                }

                if (textures.Exponent != null)
                {
                    outputName = job.TextureBaseName + "_e";

                    string exponentPath = SaveTempTexture(textures.Exponent, tempPath, outputName);

                    vmtValues.Add("EXPONENTNAME", outputName);

                    await ExportTempTexture(exponentPath, textures.Exponent, tempPath, outputName, jobOutputPath, props.ExponentCompression, !props.bExponentMipMaps, movePath, props.LogFunc);
                }

                if (textures.Normal != null)
                {
                    outputName = job.TextureBaseName + "_n";

                    string normalPath = SaveTempTexture(textures.Normal, tempPath, outputName);

                    vmtValues.Add("NORMALNAME", outputName);

                    await ExportTempTexture(normalPath, textures.Normal, tempPath, outputName, jobOutputPath, props.NormalCompression, !props.bNormalMipMaps, movePath, props.LogFunc);
                }

                if (textures.Emissive != null)
                {
                    outputName = job.TextureBaseName + "_emissive";

                    string emissivePath = SaveTempTexture(textures.Emissive, tempPath, outputName);

                    detailName = outputName;

                    await ExportTempTexture(emissivePath, textures.Emissive, tempPath, outputName, jobOutputPath, props.AlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
                }

                Color averageMetallicColor = textures.AverageMetallicColor;
                double averageRoughness = textures.AverageRoughness;

                string exportPath = VmtUtils.GetVMTPath(movePath);
                vmtValues.Add("EXPORTPATH", exportPath);
                vmtValues.Add("DETAILBLOCK", GetDetailBlock(exportPath, detailName));

                //opacity
                if (opacityMode == MaterialManipulation.OpacityMode.Alphatest)
                {
                    vmtValues.Add("BLENDTINTBYBASEALPHA", "0");
                    vmtValues.Add("OPACITYBLOCK", GetOpacityBlock(true, props.AlphatestReference));
                }
                else if (opacityMode == MaterialManipulation.OpacityMode.Translucent)
                {
                    vmtValues.Add("BLENDTINTBYBASEALPHA", "0");
                    vmtValues.Add("OPACITYBLOCK", GetOpacityBlock(false, 0.0f));
                }
                else
                {
                    vmtValues.Add("BLENDTINTBYBASEALPHA", "1");
                    vmtValues.Add("OPACITYBLOCK", string.Empty);
                }

                //envmap
                VmtUtils.EnvMapFile envmapTexture = VmtUtils.GetEnvMapTextureFromRoughness(averageRoughness);
                vmtValues.Add("ENVMAP", envmapTexture.Name);
                vmtValues.Add("ENVMAPTINT", VmtUtils.GetVMTVector(averageMetallicColor));

                string envPath = props.EnvRootPath != string.Empty ? props.EnvRootPath : movePath;
                vmtValues.Add("ENVMAPPATH", VmtUtils.GetVMTPath(envPath));

                if (envPath != string.Empty)
                {
                    Directory.CreateDirectory(envPath);

                    using (StreamWriter sw = File.CreateText(Path.Combine(envPath, envmapTexture.Name + ".vtf")))
                    {
                        sw.BaseStream.Write(envmapTexture.Content, 0, envmapTexture.Content.Length);
                    }
                }

                //generate vmt
                VmtGenerator.Generate(jobOutputPath, job.VmtFileName, vmtValues, movePath, props.VmtPreset, props.LogFunc);
            }
            finally
            {
                textures.Dispose();
            }
        }

        public static async Task StartBatch(string path, BatchProperties props, Action<string, List<string>> folderFunc)
        {
            await GenerateInFolder(path, props, path, folderFunc);
        }

        private static string GetDetailBlock(string exportPath, string detailName)
        {
            if (string.IsNullOrEmpty(detailName))
            {
                return string.Empty;
            }

            string detailPath = string.IsNullOrEmpty(exportPath) ? detailName : exportPath + "\\" + detailName;

            return
                "    \"$detail\" \"" + detailPath + "\"\r\n" +
                "    \"$detailscale\" \"1\"\r\n" +
                "    \"$detailblendmode\" \"5\"";
        }

        private static string GetOpacityBlock(bool bAlphatest, float alphatestReference)
        {
            if (bAlphatest)
            {
                string refValue = Math.Round(alphatestReference, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);

                return
                    "    \"$alphatest\" \"1\"\r\n" +
                    "    \"$alphatestreference\" \"" + refValue + "\"\r\n" +
                    "    \"$allowalphatocoverage\" \"1\"";
            }

            return "    \"$translucent\" \"1\"";
        }

        private static void PrepareTempDirectory(string tempPath, Action<string> logFunc)
        {
            if (!Directory.Exists(tempPath))
            {
                return;
            }

            TryDeleteTempDirectory(tempPath, logFunc);
        }

        private static void TryDeleteTempDirectory(string tempPath, Action<string> logFunc)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            }
            catch (Exception ex)
            {
                logFunc?.Invoke("Warning: could not delete temp folder " + tempPath + ": " + ex.Message);
            }
        }

        private static void SaveDebugTextures(MaterialManipulation.SourceTextureSet textures, string debugPath, string folderName, Action<string> logFunc)
        {
            SaveDebugTexture(textures.Albedo, debugPath, folderName + "_debug_final_albedo_before_vtfcmd.png", logFunc);
            SaveDebugTexture(textures.Normal, debugPath, folderName + "_debug_final_normal_before_vtfcmd.png", logFunc);
            SaveDebugTexture(textures.Exponent, debugPath, folderName + "_debug_exponent_phong_mask.png", logFunc);

            if (textures.Intermediates != null)
            {
                SaveDebugTexture(textures.Intermediates.Metalness, debugPath, folderName + "_debug_extracted_metalness.png", logFunc);
                SaveDebugTexture(textures.Intermediates.AmbientOcclusion, debugPath, folderName + "_debug_extracted_ao.png", logFunc);
                SaveDebugTexture(textures.Intermediates.Gloss, debugPath, folderName + "_debug_extracted_gloss.png", logFunc);
            }
        }

        private static void SaveDebugTexture(Bitmap bitmap, string debugPath, string fileName, Action<string> logFunc)
        {
            if (bitmap == null)
            {
                return;
            }

            string path = Path.Combine(debugPath, fileName);
            bitmap.Save(path, ImageFormat.Png);
            logFunc?.Invoke("Saved debug texture " + Path.GetFileName(path));
        }

        private static string SaveTempTexture(Bitmap bitmap, string tempPath, string outputName)
        {
            string tgaPath = Path.Combine(tempPath, outputName + ".tga");
            SaveTga(bitmap, tgaPath);
            return tgaPath;
        }

        private static async Task ExportTempTexture(string filePath, Bitmap bitmap, string tempPath, string outputName, string outputPath, string compression, bool noMips, string movePath, Action<string> logFunc)
        {
            try
            {
                await VtfCmdInterface.ExportFile(filePath, outputPath, compression, noMips, movePath, logFunc);
            }
            catch
            {
                string pngPath = Path.Combine(tempPath, outputName + ".png");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                bitmap.Save(pngPath, ImageFormat.Png);
                logFunc?.Invoke("TGA export failed; retrying " + outputName + " as PNG.");
                await VtfCmdInterface.ExportFile(pngPath, outputPath, compression, noMips, movePath, logFunc);
            }
        }

        private static void SaveTga(Bitmap bitmap, string path)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                byte[] pixels = new byte[Math.Abs(data.Stride) * data.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                using (FileStream stream = File.Create(path))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(new byte[]
                    {
                        0, 0, 2,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0,
                        (byte)(bitmap.Width & 0xFF), (byte)((bitmap.Width >> 8) & 0xFF),
                        (byte)(bitmap.Height & 0xFF), (byte)((bitmap.Height >> 8) & 0xFF),
                        32, 0x28
                    });

                    int stride = Math.Abs(data.Stride);

                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        int row = data.Stride > 0 ? y * stride : (bitmap.Height - 1 - y) * stride;
                        writer.Write(pixels, row, bitmap.Width * 4);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static string CombineWithRelativeFolder(string rootPath, string startPath, string currentPath)
        {
            string relativePath = currentPath.Substring(startPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(relativePath))
            {
                return rootPath;
            }

            return Path.Combine(rootPath, relativePath);
        }
    }
}
