using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mwb_materials.MwbMats
{
    class VmtGenerator
    {
        private static readonly byte[] VmtBytes = Properties.Resources.default_vmt;

        private static void SanitizeName(ref string name)
        {
            name = name.Trim().Replace(".vmt", string.Empty);
        }

        private static string GetVmtContent()
        {
            return Encoding.UTF8.GetString(VmtBytes);
        }

        public static void Generate(string path, string name, Dictionary<string, object> values, string movePath, VmtPreset preset = null, Action<string> logFunc = null)
        {
            SanitizeName(ref name);
            string content = GetVmtContent();

            foreach (KeyValuePair<string, object> pair in values)
            {
                content = content.Replace("${" + pair.Key + "}", pair.Value.ToString());
            }

            content = VmtPresetApplier.Apply(content, preset, logFunc);

            byte[] newBytes = Encoding.UTF8.GetBytes(content);
            string vmtName = Path.GetFileNameWithoutExtension(name) + ".vmt";
            string vmtPath = Path.Combine(path, vmtName);

            using (StreamWriter sw = File.CreateText(vmtPath))
            {
                sw.BaseStream.Write(newBytes, 0, newBytes.Length);
            }

            if (movePath != string.Empty)
            {
                string fileDest = Path.Combine(movePath, vmtName);

                Directory.CreateDirectory(movePath);

                if (File.Exists(fileDest))
                {
                    File.Delete(fileDest);
                }

                File.Move(vmtPath, fileDest);
            }
        }

        public static void Generate(string path, string name)
        {
            SanitizeName(ref name);

            using (StreamWriter sw = File.CreateText(Path.Combine(path, Path.GetFileNameWithoutExtension(name) + ".vmt")))
            {
                sw.BaseStream.Write(VmtBytes, 0, VmtBytes.Length);
            }
        }
    }
}
