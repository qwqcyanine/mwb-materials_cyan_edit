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
            public Action<string> LogFunc { get; internal set; }
        }

        private static async Task GenerateInFolder(string path, BatchProperties props, string startPath, Action<string, List<string>> progressFunc)
        {
            //before we do files, we have to first look in other folders, we can't run the tool
            //more than once or we are gonna eat a lot of memory
            string[] folders = Directory.GetDirectories(path);

            foreach (string folder in folders)
            {
                if (folder.ToLower().EndsWith("output") || folder.ToLower().EndsWith("temp"))
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

            string tempPath = path + "\\temp\\";
            string outputPath = path + "\\output\\";

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
                    movePath += path.Replace(startPath, string.Empty);
                }
            }

            if (textures.Albedo != null)
            {
                outputName = folderName + "_rgb";

                textures.Albedo?.Save(tempPath + outputName + ".png", ImageFormat.Png);
                vmtValues.Add("ALBEDONAME", outputName);

                await VtfCmdInterface.ExportFile(tempPath + outputName + ".png", outputPath, props.AlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
            }

            if (textures.Exponent != null)
            {
                outputName = folderName + "_e";

                textures.Exponent.Save(tempPath + outputName + ".png", ImageFormat.Png);
                vmtValues.Add("EXPONENTNAME", outputName);

                await VtfCmdInterface.ExportFile(tempPath + outputName + ".png", outputPath, props.ExponentCompression, !props.bExponentMipMaps, movePath, props.LogFunc);
            }

            if (textures.Normal != null)
            {
                outputName = folderName + "_n";

                textures.Normal?.Save(tempPath + outputName + ".png", ImageFormat.Png);
                vmtValues.Add("NORMALNAME", outputName);

                await VtfCmdInterface.ExportFile(tempPath + outputName + ".png", outputPath, props.NormalCompression, !props.bNormalMipMaps, movePath, props.LogFunc);
            }

            if (textures.Emissive != null)
            {
                outputName = folderName + "_emissive";

                textures.Emissive.Save(tempPath + outputName + ".png", ImageFormat.Png);
                detailName = outputName;

                await VtfCmdInterface.ExportFile(tempPath + outputName + ".png", outputPath, props.AlbedoCompression, !props.bAlbedoMipMaps, movePath, props.LogFunc);
            }

            Color averageMetallicColor = textures.AverageMetallicColor;
            double averageRoughness = textures.AverageRoughness;

            textures.Dispose();

            string exportPath = VmtUtils.GetVMTPath(movePath);
            vmtValues.Add("EXPORTPATH", exportPath);
            vmtValues.Add("DETAILBLOCK", GetDetailBlock(exportPath, detailName));

            //envmap
            VmtUtils.EnvMapFile envmapTexture = VmtUtils.GetEnvMapTextureFromRoughness(averageRoughness);
            vmtValues.Add("ENVMAP", envmapTexture.Name);
            vmtValues.Add("ENVMAPTINT", VmtUtils.GetVMTVector(averageMetallicColor));

            string envPath = props.EnvRootPath != string.Empty ? props.EnvRootPath : movePath;
            vmtValues.Add("ENVMAPPATH", VmtUtils.GetVMTPath(envPath));

            if (envPath != string.Empty)
            {
                using (StreamWriter sw = File.CreateText(envPath + "\\" + envmapTexture.Name + ".vtf"))
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
    }
}
