using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
            public float AlphatestReference { get; internal set; }
            public Action<string> LogFunc { get; internal set; }
        }

        private static async Task GenerateInFolder(string path, BatchProperties props, string startPath, Action<string, List<string>> progressFunc)
        {
            //before we do files, we have to first look in other folders, we can't run the tool
            //more than once or we are gonna eat a lot of memory
            string[] folders = Directory.GetDirectories(path);

            foreach (string folder in folders)
            {
                string folderNameOnly = Path.GetFileName(folder);

                if (string.Equals(folderNameOnly, "output", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(folderNameOnly, "temp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await GenerateInFolder(folder, props, startPath, progressFunc);
            }

            //after the recursion is done we can hopefully do images
            string[] files = Directory.GetFiles(path);
            List<string> sanitizedFiles = new List<string>();

            foreach (string file in files)
            {
                if (DdsLoader.IsDds(file))
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

            if (sanitizedFiles.Count <= 0)
            {
                return;
            }

            string folderName = Path.GetFileName(path);
            progressFunc(folderName, sanitizedFiles);
            props.LogFunc?.Invoke("Processing " + folderName + " (" + sanitizedFiles.Count + " source textures)");

            MaterialManipulation.SourceTextureSet textures = await MaterialManipulation.GenerateTextures(sanitizedFiles, props.GenerateProps);

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

            string tempPath = Path.Combine(path, "temp");
            string outputPath = Path.Combine(path, "output");

            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(outputPath);

            Dictionary<string, object> vmtValues = new Dictionary<string, object>();

            string outputName;
            string movePath = string.Empty;
            string detailName = string.Empty;

            if (props.bMoveOutput && props.VmtRootPath != string.Empty)
            {
                movePath = props.VmtRootPath;

                if (props.bIncludeFolders)
                {
                    movePath = CombineWithRelativeFolder(movePath, startPath, path);
                }
            }

            if (textures.Albedo != null)
            {
                outputName = folderName + "_rgb";

                string albedoPng = Path.Combine(tempPath, outputName + ".png");

                textures.Albedo?.Save(albedoPng, ImageFormat.Png);
                vmtValues.Add("ALBEDONAME", outputName);

                await VtfCmdInterface.ExportFile(albedoPng, outputPath, effectiveAlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
            }

            if (textures.Exponent != null)
            {
                outputName = folderName + "_e";

                string exponentPng = Path.Combine(tempPath, outputName + ".png");

                textures.Exponent.Save(exponentPng, ImageFormat.Png);
                vmtValues.Add("EXPONENTNAME", outputName);

                await VtfCmdInterface.ExportFile(exponentPng, outputPath, props.ExponentCompression, !props.bExponentMipMaps, movePath, props.LogFunc);
            }

            if (textures.Normal != null)
            {
                outputName = folderName + "_n";

                string normalPng = Path.Combine(tempPath, outputName + ".png");

                textures.Normal?.Save(normalPng, ImageFormat.Png);
                vmtValues.Add("NORMALNAME", outputName);

                await VtfCmdInterface.ExportFile(normalPng, outputPath, props.NormalCompression, !props.bNormalMipMaps, movePath, props.LogFunc);
            }

            if (textures.Emissive != null)
            {
                outputName = folderName + "_emissive";

                string emissivePng = Path.Combine(tempPath, outputName + ".png");

                textures.Emissive.Save(emissivePng, ImageFormat.Png);
                detailName = outputName;

                await VtfCmdInterface.ExportFile(emissivePng, outputPath, props.AlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
            }

            Color averageMetallicColor = textures.AverageMetallicColor;
            double averageRoughness = textures.AverageRoughness;

            textures.Dispose();

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
            VmtGenerator.Generate(outputPath, folderName, vmtValues, movePath);
            Directory.Delete(tempPath);
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
