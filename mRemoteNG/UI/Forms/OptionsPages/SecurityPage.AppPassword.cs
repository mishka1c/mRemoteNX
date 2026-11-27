using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Resources.Language;
using mRemoteNG.Security;
using mRemoteNG.UI.Controls;

namespace mRemoteNG.UI.Forms.OptionsPages
{
    // mRemoteNX: application password settings, built in code so the designer file stays upstream's.
    public sealed partial class SecurityPage
    {
        private MrngGroupBox groupAppPassword = null!;
        private MrngCheckBox chkRequireAppPassword = null!;
        private MrngNumericUpDown numberBoxMaxAttempts = null!;
        private MrngNumericUpDown numberBoxIdleLockMinutes = null!;

        [SupportedOSPlatform("windows")]
        private void BuildAppPasswordGroup()
        {
            chkRequireAppPassword = new MrngCheckBox
            {
                AutoSize = true,
                Text = Language.AppPassword_Require,
                Margin = new Padding(4),
            };
            chkRequireAppPassword.CheckedChanged += (_, _) => UpdateAppPasswordControls();

            numberBoxMaxAttempts = new MrngNumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = AppPasswordPolicy.DefaultMaxAttempts,
                Width = 80,
                Margin = new Padding(4),
            };

            numberBoxIdleLockMinutes = new MrngNumericUpDown
            {
                Minimum = 0,
                Maximum = 1440,
                Value = AppPasswordPolicy.DefaultIdleLockMinutes,
                Width = 80,
                Margin = new Padding(4),
            };

            TableLayoutPanel layout = new()
            {
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
            };
            layout.Controls.Add(chkRequireAppPassword, 0, 0);
            layout.SetColumnSpan(chkRequireAppPassword, 2);
            layout.Controls.Add(new MrngLabel
            {
                AutoSize = true,
                Text = Language.AppPassword_MaxAttempts,
                Anchor = AnchorStyles.Left,
            }, 0, 1);
            layout.Controls.Add(numberBoxMaxAttempts, 1, 1);
            layout.Controls.Add(new MrngLabel
            {
                AutoSize = true,
                Text = Language.AppPassword_IdleLock,
                Anchor = AnchorStyles.Left,
            }, 0, 2);
            layout.Controls.Add(numberBoxIdleLockMinutes, 1, 2);

            groupAppPassword = new MrngGroupBox
            {
                Text = Language.AppPassword_GroupTitle,
                Dock = DockStyle.Top,
                Height = 120,
            };
            groupAppPassword.Controls.Add(layout);

            // Dock order follows the reverse z-order: index 0 docks last, i.e. at the bottom.
            Controls.Add(groupAppPassword);
            Controls.SetChildIndex(groupAppPassword, 0);
        }

        private void UpdateAppPasswordControls()
        {
            bool required = chkRequireAppPassword.Checked;
            numberBoxMaxAttempts.Enabled = required;
            numberBoxIdleLockMinutes.Enabled = required;

            // The policy always writes a fully encrypted file.
            if (required)
                chkEncryptCompleteFile.Checked = true;
            chkEncryptCompleteFile.Enabled = !required && !(pageRegSettingsInstance?.EncryptCompleteConnectionsFile.IsSet ?? false);
        }

        private void LoadAppPasswordSettings()
        {
            Properties.OptionsSecurityPage settings = Properties.OptionsSecurityPage.Default;
            chkRequireAppPassword.Checked = settings.RequireAppPassword;
            numberBoxMaxAttempts.Value = System.Math.Clamp(AppPasswordPolicy.MaxAttempts, (int)numberBoxMaxAttempts.Minimum, (int)numberBoxMaxAttempts.Maximum);
            numberBoxIdleLockMinutes.Value = System.Math.Clamp(AppPasswordPolicy.IdleLockMinutes, (int)numberBoxIdleLockMinutes.Minimum, (int)numberBoxIdleLockMinutes.Maximum);
            UpdateAppPasswordControls();
        }

        private void SaveAppPasswordSettings()
        {
            Properties.OptionsSecurityPage settings = Properties.OptionsSecurityPage.Default;
            settings.RequireAppPassword = chkRequireAppPassword.Checked;
            settings.AppPasswordMaxAttempts = (int)numberBoxMaxAttempts.Value;
            settings.AutoLockIdleMinutes = (int)numberBoxIdleLockMinutes.Value;
            if (settings.RequireAppPassword)
                settings.EncryptCompleteConnectionsFile = true;
        }
    }
}
