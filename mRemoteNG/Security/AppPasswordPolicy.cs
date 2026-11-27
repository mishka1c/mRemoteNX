using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Messages;
using mRemoteNG.Tools;
using mRemoteNG.Tree.Root;

namespace mRemoteNG.Security
{
    /// <summary>
    /// mRemotePass application password policy.
    ///
    /// One password protects both the application and the connections file: it is the
    /// connections master password, so the AES-256-GCM key that encrypts the file is derived
    /// from it (PBKDF2). When <see cref="Enabled"/> is set:
    /// <list type="bullet">
    /// <item>the application cannot be used until that password is entered;</item>
    /// <item>a connections file without a master password gets one on first start (the user
    /// sets it, or the application exits), and the whole file is encrypted;</item>
    /// <item>after <see cref="MaxAttempts"/> wrong passwords in a row the application exits;</item>
    /// <item>the main window locks after <see cref="IdleLockMinutes"/> minutes without input.</item>
    /// </list>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class AppPasswordPolicy
    {
        public const int DefaultMaxAttempts = 15;
        public const int DefaultIdleLockMinutes = 15;

        private static int _failedAttempts;
        private static bool _unlockedOnce;

        /// <summary>Replaceable for tests; shows the password dialog.</summary>
        internal static Func<string?, bool, Optional<SecureString>> PasswordPrompt { get; set; } =
            (label, newPassword) => MiscTools.PasswordDialog(label, newPassword);

        /// <summary>Replaceable for tests; terminates the process.</summary>
        internal static Action<string> Terminate { get; set; } = TerminateProcess;

        public static bool Enabled => Properties.OptionsSecurityPage.Default.RequireAppPassword;

        public static int MaxAttempts
        {
            get
            {
                int value = Properties.OptionsSecurityPage.Default.AppPasswordMaxAttempts;
                return value > 0 ? value : DefaultMaxAttempts;
            }
        }

        /// <summary>Idle minutes before the window locks; 0 disables the idle lock.</summary>
        public static int IdleLockMinutes => Math.Max(0, Properties.OptionsSecurityPage.Default.AutoLockIdleMinutes);

        public static int FailedAttempts => _failedAttempts;

        /// <summary>
        /// Prompt used while the connections file is decrypted. Every prompt after the first
        /// means the previous answer was rejected, so it is counted as a failed attempt. Under
        /// the policy, cancelling before the first successful unlock exits the application,
        /// and reaching the attempt limit always does.
        /// </summary>
        public static Func<Optional<SecureString>> CreateConnectionsPasswordRequestor(Func<string?> label)
        {
            bool previousPromptShown = false;
            return () =>
            {
                if (!Enabled)
                    return PasswordPrompt(label(), false);

                if (previousPromptShown)
                    RegisterFailedAttempt();

                previousPromptShown = true;
                Optional<SecureString> password = PasswordPrompt(label(), false);
                if ((!password.Any() || password.First().Length == 0) && !_unlockedOnce)
                    Terminate("The application password was not entered.");

                return password;
            };
        }

        /// <summary>
        /// Attempt limit for a <see cref="Authentication.PasswordAuthenticator"/>: under the
        /// policy the limit is enforced by the requestor above, so the authenticator itself
        /// must not give up first.
        /// </summary>
        public static int AuthenticatorMaxAttempts(int defaultValue) => Enabled ? int.MaxValue : defaultValue;

        /// <summary>Records a rejected password and exits once the limit is reached.</summary>
        public static void RegisterFailedAttempt()
        {
            if (!Enabled)
                return;

            _failedAttempts++;
            Runtime.MessageCollector?.AddMessage(MessageClass.WarningMsg,
                $"Wrong application password ({_failedAttempts} of {MaxAttempts}).");

            if (_failedAttempts >= MaxAttempts)
                Terminate($"The application password was entered wrong {_failedAttempts} times.");
        }

        public static void RegisterSuccessfulUnlock()
        {
            _failedAttempts = 0;
            _unlockedOnce = true;
        }

        /// <summary>
        /// Called after the startup load. Makes sure the loaded connections are protected by a
        /// master password and that the whole file is encrypted, asking the user to create the
        /// password if there is none yet. Exits the application if the user refuses.
        /// </summary>
        public static void EnsureConnectionsProtected()
        {
            if (!Enabled)
                return;

            ConnectionsService service = Runtime.ConnectionsService;
            RootNodeInfo? root = service.ConnectionTreeModel?.RootNodes
                .OfType<RootNodeInfo>()
                .FirstOrDefault(node => node.Type == RootNodeType.Connection);

            if (root is null)
            {
                Terminate("The connections could not be loaded, so the application password cannot be checked.");
                return;
            }

            bool changed = false;

            if (!root.Password)
            {
                string label = Properties.OptionsDBsPage.Default.UseSQLServer
                    ? "SQL Server"
                    : Path.GetFileName(ConnectionsService.GetStartupConnectionFileName());

                MessageBox.Show(
                    "mRemotePass protects the application and all connections with one password.\n\n" +
                    "Create the application password now. It encrypts the connections file " +
                    "(AES-256-GCM) and is asked for at every start and after the idle lock.\n\n" +
                    "There is no way to recover it: if it is lost, the saved connections cannot be decrypted.",
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

                Optional<SecureString> password = PasswordPrompt(label, true);
                if (!password.Any() || password.First().Length == 0)
                {
                    Terminate("No application password was created.");
                    return;
                }

                root.PasswordString = password.First().ConvertToUnsecureString();
                changed = true;
            }

            if (!Properties.OptionsSecurityPage.Default.EncryptCompleteConnectionsFile)
            {
                Properties.OptionsSecurityPage.Default.EncryptCompleteConnectionsFile = true;
                Properties.OptionsSecurityPage.Default.Save();
                changed = true;
            }

            RegisterSuccessfulUnlock();

            if (changed)
                service.SaveConnections();
        }

        private static void TerminateProcess(string reason)
        {
            Runtime.MessageCollector?.AddMessage(MessageClass.ErrorMsg, $"Application password: {reason} Exiting.");
            try
            {
                MessageBox.Show(reason + "\n\nThe application will now close.",
                    Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            catch (InvalidOperationException)
            {
                _ = 0; // No UI available; exit anyway.
            }

            Environment.Exit(2);
        }
    }
}
