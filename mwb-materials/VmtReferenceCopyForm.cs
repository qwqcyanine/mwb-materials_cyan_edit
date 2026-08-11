using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace mwb_materials
{
    public class VmtReferenceCopyForm : Form
    {
        private class ParameterDef
        {
            public string Label;
            public string[] Keys;
        }

        private static readonly ParameterDef[] ParameterDefs = new ParameterDef[]
        {
            new ParameterDef { Label = "$basetexture (albedo)", Keys = new[] { "$basetexture" } },
            new ParameterDef { Label = "$bumpmap (normal)", Keys = new[] { "$bumpmap" } },
            new ParameterDef { Label = "$phongexponenttexture (exponent)", Keys = new[] { "$phongexponenttexture" } },
            new ParameterDef { Label = "$detail block (emissive)", Keys = new[] { "$detail", "$detailscale", "$detailblendmode" } },
        };

        private class Mapping
        {
            public string SourcePath;
            public List<string> TargetPaths;
            public List<ParameterDef> Parameters;

            public override string ToString()
            {
                string names = string.Join(", ", Parameters.Select(p => p.Label.Split(' ')[0].TrimStart('$')));
                return Path.GetFileName(SourcePath) + " → " + TargetPaths.Count + " target(s) [" + names + "]";
            }
        }

        private TextBox SourceTextBox;
        private Button BrowseSourceButton;
        private CheckedListBox TargetsListBox;
        private Button AddFolderButton;
        private Button AddFilesButton;
        private Button ClearTargetsButton;
        private CheckBox[] ParameterChecks;
        private Button AddMappingButton;
        private ListBox MappingsListBox;
        private Button RemoveMappingButton;
        private Button ApplyButton;
        private Label StatusLabel;

        private readonly List<Mapping> Mappings = new List<Mapping>();

        public VmtReferenceCopyForm()
        {
            BuildLayout();
            UiTheme.Apply(this);
        }

        private void BuildLayout()
        {
            Text = "VMT Reference Copy";
            ClientSize = new Size(560, 640);
            Padding = new Padding(12);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            GroupBox sourceGroup = new GroupBox() { Text = "Source VMT", Padding = new Padding(8) };
            sourceGroup.SetBounds(12, 12, 536, 58);
            Controls.Add(sourceGroup);

            SourceTextBox = new TextBox();
            SourceTextBox.SetBounds(10, 22, 400, 21);
            sourceGroup.Controls.Add(SourceTextBox);

            BrowseSourceButton = new Button() { Text = "Browse...", UseVisualStyleBackColor = true };
            BrowseSourceButton.SetBounds(418, 20, 106, 25);
            BrowseSourceButton.Click += BrowseSourceButton_Click;
            sourceGroup.Controls.Add(BrowseSourceButton);

            GroupBox targetsGroup = new GroupBox() { Text = "Target VMTs", Padding = new Padding(8) };
            targetsGroup.SetBounds(12, 78, 536, 178);
            Controls.Add(targetsGroup);

            TargetsListBox = new CheckedListBox()
            {
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            TargetsListBox.SetBounds(10, 20, 516, 112);
            targetsGroup.Controls.Add(TargetsListBox);

            AddFolderButton = new Button() { Text = "Add Folder...", UseVisualStyleBackColor = true };
            AddFolderButton.SetBounds(10, 140, 160, 26);
            AddFolderButton.Click += AddFolderButton_Click;
            targetsGroup.Controls.Add(AddFolderButton);

            AddFilesButton = new Button() { Text = "Add Files...", UseVisualStyleBackColor = true };
            AddFilesButton.SetBounds(178, 140, 160, 26);
            AddFilesButton.Click += AddFilesButton_Click;
            targetsGroup.Controls.Add(AddFilesButton);

            ClearTargetsButton = new Button() { Text = "Clear", UseVisualStyleBackColor = true };
            ClearTargetsButton.SetBounds(346, 140, 180, 26);
            ClearTargetsButton.Click += (sender, args) => TargetsListBox.Items.Clear();
            targetsGroup.Controls.Add(ClearTargetsButton);

            GroupBox paramsGroup = new GroupBox() { Text = "Parameters to copy", Padding = new Padding(8) };
            paramsGroup.SetBounds(12, 264, 536, 58);
            Controls.Add(paramsGroup);

            ParameterChecks = new CheckBox[ParameterDefs.Length];
            for (int i = 0; i < ParameterDefs.Length; i++)
            {
                CheckBox check = new CheckBox()
                {
                    AutoSize = true,
                    Checked = true,
                    Text = ParameterDefs[i].Label
                };
                int col = i % 2;
                int row = i / 2;
                check.SetBounds(10 + col * 262, 20 + row * 20, 252, 20);
                paramsGroup.Controls.Add(check);
                ParameterChecks[i] = check;
            }

            AddMappingButton = new Button() { Text = "Add Mapping →", UseVisualStyleBackColor = true };
            AddMappingButton.SetBounds(12, 330, 536, 28);
            AddMappingButton.Click += AddMappingButton_Click;
            Controls.Add(AddMappingButton);

            GroupBox mappingsGroup = new GroupBox() { Text = "Mappings", Padding = new Padding(8) };
            mappingsGroup.SetBounds(12, 366, 536, 172);
            Controls.Add(mappingsGroup);

            MappingsListBox = new ListBox() { IntegralHeight = false, HorizontalScrollbar = true };
            MappingsListBox.SetBounds(10, 20, 516, 110);
            MappingsListBox.KeyDown += MappingsListBox_KeyDown;
            mappingsGroup.Controls.Add(MappingsListBox);

            RemoveMappingButton = new Button() { Text = "Remove", UseVisualStyleBackColor = true };
            RemoveMappingButton.SetBounds(10, 136, 516, 26);
            RemoveMappingButton.Click += RemoveMappingButton_Click;
            mappingsGroup.Controls.Add(RemoveMappingButton);

            ApplyButton = new Button() { Text = "Apply", UseVisualStyleBackColor = true };
            ApplyButton.SetBounds(12, 546, 536, 30);
            ApplyButton.Click += ApplyButton_Click;
            Controls.Add(ApplyButton);

            StatusLabel = new Label() { AutoSize = false, Text = "", TextAlign = ContentAlignment.MiddleLeft };
            StatusLabel.SetBounds(12, 582, 536, 20);
            Controls.Add(StatusLabel);

            StatusLabel.Text = "Pick a source VMT, add targets, then add a mapping.";
        }

        private void BrowseSourceButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "VMT files (*.vmt)|*.vmt";
                dialog.Title = "Select source VMT";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SourceTextBox.Text = dialog.FileName;
                }
            }
        }

        private void AddFolderButton_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                dialog.Title = "Select folder with target VMTs";

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string[] files = Directory.GetFiles(dialog.FileName, "*.vmt", SearchOption.AllDirectories);
                    AddTargets(files);
                }
            }
        }

        private void AddFilesButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "VMT files (*.vmt)|*.vmt";
                dialog.Title = "Select target VMTs";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    AddTargets(dialog.FileNames);
                }
            }
        }

        private void AddTargets(IEnumerable<string> files)
        {
            string source = SourceTextBox.Text.Trim();
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (object item in TargetsListBox.Items)
            {
                existing.Add((string)item);
            }

            foreach (string file in files)
            {
                if (existing.Contains(file))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(source) && string.Equals(file, source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                existing.Add(file);
                TargetsListBox.Items.Add(file, true);
            }
        }

        private void AddMappingButton_Click(object sender, EventArgs e)
        {
            string source = SourceTextBox.Text.Trim();

            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                MessageBox.Show("Pick an existing source VMT first.", "VMT Reference Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<string> targets = new List<string>();
            foreach (object item in TargetsListBox.CheckedItems)
            {
                string target = (string)item;

                if (string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                targets.Add(target);
            }

            if (targets.Count == 0)
            {
                MessageBox.Show("Check at least one target VMT.", "VMT Reference Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<ParameterDef> parameters = new List<ParameterDef>();
            for (int i = 0; i < ParameterChecks.Length; i++)
            {
                if (ParameterChecks[i].Checked)
                {
                    parameters.Add(ParameterDefs[i]);
                }
            }

            if (parameters.Count == 0)
            {
                MessageBox.Show("Check at least one parameter to copy.", "VMT Reference Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Mapping mapping = new Mapping()
            {
                SourcePath = source,
                TargetPaths = targets,
                Parameters = parameters
            };

            Mappings.Add(mapping);
            MappingsListBox.Items.Add(mapping);
            StatusLabel.Text = "Mapping added: " + mapping.ToString();
        }

        private void RemoveMappingButton_Click(object sender, EventArgs e)
        {
            RemoveSelectedMapping();
        }

        private void MappingsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedMapping();
                e.Handled = true;
            }
        }

        private void RemoveSelectedMapping()
        {
            int index = MappingsListBox.SelectedIndex;

            if (index < 0 || index >= Mappings.Count)
            {
                return;
            }

            Mappings.RemoveAt(index);
            MappingsListBox.Items.RemoveAt(index);
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            if (Mappings.Count == 0)
            {
                MessageBox.Show("Add at least one mapping first.", "VMT Reference Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int updated = 0;
            int failed = 0;
            int warnings = 0;
            List<string> failureLog = new List<string>();

            foreach (Mapping mapping in Mappings)
            {
                Dictionary<string, string> sourceValues;
                HashSet<string> missingKeys;

                try
                {
                    string sourceText = File.ReadAllText(mapping.SourcePath);
                    sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    missingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (ParameterDef param in mapping.Parameters)
                    {
                        foreach (string key in param.Keys)
                        {
                            string value = FindValue(sourceText, key);

                            if (value == null)
                            {
                                missingKeys.Add(key);
                            }
                            else
                            {
                                sourceValues[key] = value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (string target in mapping.TargetPaths)
                    {
                        failed++;
                        failureLog.Add(Path.GetFileName(target) + ": could not read source (" + ex.Message + ")");
                    }
                    continue;
                }

                warnings += missingKeys.Count;

                foreach (string target in mapping.TargetPaths)
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(target);
                        bool hasBom = rawBytes.Length >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF;
                        string text = File.ReadAllText(target);
                        bool changed = false;

                        foreach (ParameterDef param in mapping.Parameters)
                        {
                            foreach (string key in param.Keys)
                            {
                                string value;

                                if (!sourceValues.TryGetValue(key, out value))
                                {
                                    continue;
                                }

                                if (TryReplaceValue(ref text, key, value))
                                {
                                    changed = true;
                                }
                                else
                                {
                                    text = InsertLine(text, key, value);
                                    changed = true;
                                }
                            }
                        }

                        if (changed)
                        {
                            File.WriteAllText(target, text, new UTF8Encoding(hasBom));
                            updated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failureLog.Add(Path.GetFileName(target) + ": " + ex.Message);
                    }
                }
            }

            string summary = "Updated " + updated + " VMT file(s), " + failed + " failed.";

            if (warnings > 0)
            {
                summary += " " + warnings + " parameter(s) missing from source, skipped.";
            }

            StatusLabel.Text = summary;

            string message = summary;

            if (failureLog.Count > 0)
            {
                message += "\n\nFailures:\n" + string.Join("\n", failureLog.Take(20));

                if (failureLog.Count > 20)
                {
                    message += "\n... and " + (failureLog.Count - 20) + " more.";
                }
            }

            MessageBox.Show(message, "VMT Reference Copy", MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private static string FindValue(string text, string key)
        {
            Match match = Regex.Match(text, "(\"" + Regex.Escape(key) + "\"\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[2].Value : null;
        }

        private static bool TryReplaceValue(ref string text, string key, string value)
        {
            Regex regex = new Regex("(\"" + Regex.Escape(key) + "\"\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase);

            if (!regex.IsMatch(text))
            {
                return false;
            }

            text = regex.Replace(text, "${1}" + value + "${3}", 1);
            return true;
        }

        private static string InsertLine(string text, string key, string value)
        {
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            int closingBrace = text.LastIndexOf('}');

            if (closingBrace < 0)
            {
                return text;
            }

            return text.Insert(closingBrace, "\t\"" + key + "\" \"" + value + "\"" + newline);
        }
    }
}
