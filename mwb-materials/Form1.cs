using Microsoft.WindowsAPICodePack.Dialogs;
using mwb_materials.MwbMats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mwb_materials
{
    public partial class Form1 : Form
    {
        private TextBox ConsoleTextBox;
        private TrackBar AoStrengthTrackBar;
        private Label AoStrengthValueLabel;

        private ToolTip ToolTip = new ToolTip()
        {
            InitialDelay = 100,
            ReshowDelay = 100,
            ShowAlways = true,
            UseAnimation = false,
            UseFading = false
        };

        public Form1()
        {
            InitializeComponent();
            ModernizeLayout();
            AddConsole();
            UiTheme.Apply(this);
        }

        private void ModernizeLayout()
        {
            AutoSize = false;
            ClientSize = new Size(520, 620);
            Padding = new Padding(12);

            GroupBox settingsGroup = GetGroupBox("Settings");
            GroupBox vtfsGroup = GetGroupBox("VTFs");
            GroupBox batchGroup = GetGroupBox("Batch");

            LayoutGroup(settingsGroup, 12, 12, 496, 204);
            LayoutGroup(vtfsGroup, 12, 226, 496, 130);
            LayoutGroup(batchGroup, 12, 368, 496, 86);

            AoCheck.SetBounds(8, 19, 220, 20);
            OpenGlNormalCheck.SetBounds(8, 43, 240, 20);
            AddAoStrengthControls(settingsGroup);
            SetGroupLabel(settingsGroup, "Envmaps folder", 8, 99, 120);
            ResizeTextBox(EnvMapsDestination, 8, 116, 480);
            SetGroupLabel(settingsGroup, "Output destination", 8, 141, 130);
            ResizeTextBox(VmtDestinationPath, 8, 158, 480);
            label2.SetBounds(8, 184, 92, 16);
            ClampComboBox.SetBounds(108, 180, 120, 21);

            LayoutVtfControls(vtfsGroup);
            BatchIncludeFoldersCheck.SetBounds(10, 21, 225, 20);
            BatchMoveOutputCheck.SetBounds(10, 45, 225, 20);
            FolderButton.Dock = DockStyle.None;
            FolderButton.SetBounds(252, 28, 236, 42);
        }

        private void AddAoStrengthControls(GroupBox settingsGroup)
        {
            Label label = new Label()
            {
                AutoSize = false,
                Text = "AO albedo",
                TextAlign = ContentAlignment.MiddleLeft
            };
            label.SetBounds(8, 70, 92, 18);

            AoStrengthTrackBar = new TrackBar()
            {
                AutoSize = false,
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 25,
                Value = 100
            };
            AoStrengthTrackBar.SetBounds(108, 63, 280, 30);

            AoStrengthValueLabel = new Label()
            {
                AutoSize = false,
                Text = "100%",
                TextAlign = ContentAlignment.MiddleRight
            };
            AoStrengthValueLabel.SetBounds(398, 70, 90, 18);

            AoStrengthTrackBar.Scroll += (sender, args) =>
            {
                AoStrengthValueLabel.Text = AoStrengthTrackBar.Value + "%";
            };

            settingsGroup.Controls.Add(label);
            settingsGroup.Controls.Add(AoStrengthTrackBar);
            settingsGroup.Controls.Add(AoStrengthValueLabel);
        }

        private GroupBox GetGroupBox(string text)
        {
            return Controls.OfType<GroupBox>().FirstOrDefault(group => group.Text == text);
        }

        private void LayoutGroup(GroupBox group, int x, int y, int width, int height)
        {
            if (group == null)
            {
                return;
            }

            group.Dock = DockStyle.None;
            group.SetBounds(x, y, width, height);
        }

        private void ResizeTextBox(TextBox textBox, int x, int y, int width)
        {
            textBox.SetBounds(x, y, width, textBox.Height);
        }

        private void SetGroupLabel(GroupBox group, string text, int x, int y, int width)
        {
            Label label = group.Controls.OfType<Label>().FirstOrDefault(control => control.Text.Trim().StartsWith(text));

            if (label != null)
            {
                label.SetBounds(x, y, width, 16);
            }
        }

        private void LayoutVtfControls(GroupBox vtfsGroup)
        {
            TableLayoutPanel table = vtfsGroup.Controls.OfType<TableLayoutPanel>().FirstOrDefault();

            if (table == null)
            {
                return;
            }

            table.Dock = DockStyle.None;
            table.SetBounds(8, 38, 238, 84);
            table.ColumnStyles.Clear();
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));

            SetVtfHeader(vtfsGroup, "Texture", 8, 18, 62);
            SetVtfHeader(vtfsGroup, "Compression", 70, 18, 146);
            SetVtfHeader(vtfsGroup, "Mips", 216, 18, 30);
        }

        private void SetVtfHeader(GroupBox vtfsGroup, string text, int x, int y, int width)
        {
            Label label = vtfsGroup.Controls.OfType<Label>().FirstOrDefault(control => control.Text == text);

            if (label != null)
            {
                label.SetBounds(x, y, width, 16);
            }
        }

        private void AddConsole()
        {
            GroupBox consoleGroup = new GroupBox()
            {
                Dock = DockStyle.None,
                Text = "Console",
                Padding = new Padding(8)
            };
            consoleGroup.SetBounds(12, 466, 496, 142);

            ConsoleTextBox = new TextBox()
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.None,
                WordWrap = true
            };

            consoleGroup.Controls.Add(ConsoleTextBox);
            Controls.Add(consoleGroup);
        }

        private void AppendConsoleLine(string message)
        {
            if (ConsoleTextBox == null || ConsoleTextBox.IsDisposed)
            {
                return;
            }

            if (ConsoleTextBox.InvokeRequired)
            {
                ConsoleTextBox.BeginInvoke(new Action(() => AppendConsoleLine(message)));
                return;
            }

            ConsoleTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private void SetBatchControlsEnabled(bool enabled)
        {
            foreach (Control control in Controls)
            {
                if (!ContainsConsole(control))
                {
                    control.Enabled = enabled;
                }
            }
        }

        private bool ContainsConsole(Control control)
        {
            if (control == ConsoleTextBox)
            {
                return true;
            }

            foreach (Control child in control.Controls)
            {
                if (ContainsConsole(child))
                {
                    return true;
                }
            }

            return false;
        }

        private async void FolderButton_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog folderDialog = new CommonOpenFileDialog())
            {
                folderDialog.IsFolderPicker = true;
                CommonFileDialogResult result = folderDialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    Stopwatch timer = new Stopwatch();
                    MaterialManipulation.GenerateProperties props = new MaterialManipulation.GenerateProperties()
                    {
                        bAoMasks = AoCheck.Checked,
                        bOpenGlNormal = OpenGlNormalCheck.Checked,
                        ClampSize = int.Parse(ClampComboBox.Text),
                        AoAlbedoStrength = AoStrengthTrackBar.Value / 100.0f
                    };

                    BatchExporter.BatchProperties bProps = new BatchExporter.BatchProperties()
                    {
                        VmtRootPath = VmtDestinationPath.Text,
                        EnvRootPath = EnvMapsDestination.Text,
                        bMoveOutput = BatchMoveOutputCheck.Checked,
                        bIncludeFolders = BatchIncludeFoldersCheck.Checked,
                        AlbedoCompression = AlbedoCompression.Text,
                        NormalCompression = NormalCompression.Text,
                        ExponentCompression = ExponentCompression.Text,
                        bAlbedoMipMaps = AlbedoMipMapsCheck.Checked,
                        bNormalMipMaps = NormalMipMapsCheck.Checked,
                        bExponentMipMaps = ExponentMipMapsCheck.Checked,
                        GenerateProps = props,
                        LogFunc = AppendConsoleLine
                    };

                    timer.Start();
                    SetBatchControlsEnabled(false);
                    ConsoleTextBox.Clear();
                    AppendConsoleLine("Starting batch: " + folderDialog.FileName);

                    BatchProgressForm bpForm = new BatchProgressForm();
                    bpForm.Show(this);
                    Exception batchError = null;

                    try
                    {
                        string batchPath = folderDialog.FileName;
                        await Task.Run(async () =>
                        {
                            await BatchExporter.StartBatch(batchPath, bProps, (string folder, List<string> files) =>
                            {
                                if (bpForm.IsDisposed || !bpForm.IsHandleCreated)
                                {
                                    return;
                                }

                                try
                                {
                                    bpForm.BeginInvoke(new Action(() =>
                                    {
                                        if (bpForm.IsDisposed)
                                        {
                                            return;
                                        }

                                        bpForm.SetFolderName(folder);
                                        bpForm.SetTextures(files);
                                    }));
                                }
                                catch (InvalidOperationException)
                                {
                                }
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        batchError = ex;
                    }
                    finally
                    {
                        timer.Stop();
                        SetBatchControlsEnabled(true);
                        if (!bpForm.IsDisposed)
                        {
                            bpForm.Close();
                        }
                    }

                    if (batchError == null)
                    {
                        AppendConsoleLine("Batch complete.");
                        MessageBox.Show("Generated textures! (" + timer.Elapsed.ToString(@"m\:ss\.fff") + ")", "MWB Mats", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendConsoleLine("Batch failed: " + batchError.Message);
                        MessageBox.Show("Batch export failed:\n\n" + batchError.Message, "MWB Mats", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] compressionFormats = new string[] { VtfCmdInterface.FormatDXT5, VtfCmdInterface.FormatRGBA8888, VtfCmdInterface.FormatDXT1 };

            AlbedoCompression.Items.AddRange(compressionFormats);
            AlbedoCompression.SelectedIndex = 0;

            NormalCompression.Items.AddRange(compressionFormats);
            NormalCompression.SelectedIndex = 1;

            ExponentCompression.Items.AddRange(compressionFormats);
            ExponentCompression.SelectedIndex = 0;

            VmtDestinationPath.Text = Properties.Settings.Default.DestinationFolder;

            ClampComboBox.Items.AddRange(new string[] { "4096", "2048", "1024", "512" });
            ClampComboBox.SelectedIndex = 0;

            EnvMapsDestination.Text = Properties.Settings.Default.EnvMapsFolder;
            ToolTip.SetToolTip(EnvMapsDestination, EnvMapsDestination.Text);

            VmtDestinationPath.Text = Properties.Settings.Default.DestinationFolder;
            ToolTip.SetToolTip(VmtDestinationPath, VmtDestinationPath.Text);

            HelpButtonClicked += Form1_HelpButtonClicked;

            ToolTip.SetToolTip(AlbedoLabel, "basetexture");
            ToolTip.SetToolTip(NormalLabel, "bumpmap");
            ToolTip.SetToolTip(ExponentLabel, "phongexponent");
        }

        private void Form1_HelpButtonClicked(object sender, EventArgs e)
        {
            Process.Start("https://github.com/mushroom-guy/mwb-materials/blob/main/help.md");
        }

        private void EnvMapsDestination_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.EnvMapsFolder = EnvMapsDestination.Text;
            ToolTip.SetToolTip(EnvMapsDestination, EnvMapsDestination.Text);
            Properties.Settings.Default.Save();
        }

        private void VmtDestinationPath_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DestinationFolder = VmtDestinationPath.Text;
            ToolTip.SetToolTip(VmtDestinationPath, VmtDestinationPath.Text);
            Properties.Settings.Default.Save();
        }
    }
}
