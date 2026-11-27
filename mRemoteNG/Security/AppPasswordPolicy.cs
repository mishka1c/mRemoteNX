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
    /// Політика пароля програми (mRemotePass).
    ///
    /// Один пароль захищає і програму, і файл підключень: це майстер-пароль підключень,
    /// тому ключ AES-256-GCM, яким шифрується файл, виводиться саме з нього (PBKDF2).
    /// Коли <see cref="Enabled"/> увімкнено:
    /// <list type="bullet">
    /// <item>програмою не можна користуватися, доки не введено пароль;</item>
    /// <item>файл підключень без майстер-пароля отримує його при першому запуску
    /// (користувач задає пароль, інакше програма закривається), і весь файл шифрується;</item>
    /// <item>після <see cref="MaxAttempts"/> невдалих спроб поспіль програма закривається;</item>
    /// <item>головне вікно блокується після <see cref="IdleLockMinutes"/> хвилин бездіяльності.</item>
    /// </list>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class AppPasswordPolicy
    {
        public const int DefaultMaxAttempts = 15;
        public const int DefaultIdleLockMinutes = 15;

        private static int _failedAttempts;
        private static bool _unlockedOnce;

        /// <summary>Показує діалог пароля; можна підмінити в тестах.</summary>
        internal static Func<string?, bool, Optional<SecureString>> PasswordPrompt { get; set; } =
            (label, newPassword) => MiscTools.PasswordDialog(label, newPassword);

        /// <summary>Завершує процес; можна підмінити в тестах.</summary>
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

        /// <summary>Хвилини бездіяльності до блокування вікна; 0 вимикає блокування.</summary>
        public static int IdleLockMinutes => Math.Max(0, Properties.OptionsSecurityPage.Default.AutoLockIdleMinutes);

        public static int FailedAttempts => _failedAttempts;

        /// <summary>
        /// Запит пароля під час розшифрування файлу підключень. Кожен запит після першого
        /// означає, що попередню відповідь відхилено, тому він рахується як невдала спроба.
        /// Якщо політика увімкнена, скасування до першого успішного входу закриває програму,
        /// а досягнення ліміту спроб закриває її завжди.
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
        /// Ліміт спроб для <see cref="Authentication.PasswordAuthenticator"/>: коли політика
        /// увімкнена, ліміт контролює запит пароля вище, тому сам автентифікатор
        /// не повинен здаватися раніше.
        /// </summary>
        public static int AuthenticatorMaxAttempts(int defaultValue) => Enabled ? int.MaxValue : defaultValue;

        /// <summary>Рахує невірний пароль і закриває програму, коли досягнуто ліміту.</summary>
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
        /// Викликається після завантаження при старті. Перевіряє, що підключення захищені
        /// майстер-паролем і весь файл шифрується; якщо пароля ще немає — просить його створити.
        /// Якщо користувач відмовляється, програма закривається.
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
                _ = 0; // Інтерфейсу немає — все одно виходимо.
            }

            Environment.Exit(2);
        }
    }
}
