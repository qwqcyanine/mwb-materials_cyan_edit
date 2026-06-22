using System.Drawing;
using System.Windows.Forms;

namespace mwb_materials
{
    internal static class UiTheme
    {
        private static readonly Color Window = Color.FromArgb(30, 32, 36);
        private static readonly Color Surface = Color.FromArgb(39, 42, 48);
        private static readonly Color ControlSurface = Color.FromArgb(48, 52, 60);
        private static readonly Color Text = Color.FromArgb(232, 235, 239);
        private static readonly Color MutedText = Color.FromArgb(160, 167, 176);
        private static readonly Color Border = Color.FromArgb(76, 82, 92);

        public static void Apply(Form form)
        {
            form.BackColor = Window;
            form.ForeColor = Text;
            ApplyToControls(form.Controls);
        }

        private static void ApplyToControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                ApplyToControl(control);
                ApplyToControls(control.Controls);
            }
        }

        private static void ApplyToControl(Control control)
        {
            if (control is TextBox || control is ComboBox)
            {
                control.BackColor = ControlSurface;
                control.ForeColor = Text;
            }
            else if (control is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.BackColor = ControlSurface;
                button.ForeColor = Text;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.UseVisualStyleBackColor = false;
                checkBox.BackColor = Window;
                checkBox.ForeColor = Text;
            }
            else if (control is GroupBox || control is TableLayoutPanel)
            {
                control.BackColor = Window;
                control.ForeColor = Text;
            }
            else if (control is Label)
            {
                control.BackColor = Color.Transparent;

                if (control.ForeColor.IsSystemColor)
                {
                    control.ForeColor = MutedText;
                }
            }
            else
            {
                control.BackColor = Surface;

                if (control.ForeColor.IsSystemColor)
                {
                    control.ForeColor = Text;
                }
            }
        }
    }
}
