# Як зібрати mRemoteNX і долучитися до розробки

## Що потрібно

- **Windows 10/11** — mRemoteNX працює лише на Windows (WinForms).
- **Visual Studio 2026** (або 2022) чи Build Tools з навантаженнями:
  - .NET desktop development;
  - Desktop development with C++ (для COM-посилань).
- **.NET 10 SDK**.
- **Git**.

## Збірка

> `dotnet build` не підходить: він не вміє обробляти COM-посилання на RDP-компонент (`MSB4803`).
> Потрібен повний MSBuild із Visual Studio.

```powershell
git clone https://gitea.mykino.uk/mishka1c/mRemoteNX.git
cd mRemoteNX
pwsh -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

Результат — у `mRemoteNG/bin/x64/Release/`. Скрипт сам знаходить Visual Studio.

Без Visual Studio можна зібрати на GitHub: **Actions → Nightly Build → Run workflow**. Готові zip і MSI
з'являться в релізі **nightly**.

## Тести

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File run-tests.ps1 -Headless
```

Окрему групу тестів можна запустити так:

```powershell
dotnet test "mRemoteNGTests\bin\x64\Release\mRemoteNGTests.dll" --verbosity normal --filter "FullyQualifiedName~mRemoteNGTests.Security"
```

Правила:

- запускайте тести з DLL, а не з `.csproj`;
- `--verbosity normal` обов'язково (інакше тестовий процес падає на .NET 10);
- тести не повинні показувати вікна чи чекати дій користувача.

## Як надсилати зміни

1. Зробіть форк репозиторію на Gitea або GitHub.
2. Створіть гілку від `main`.
3. Внесіть зміни, зберіть програму і запустіть тести.
4. Відкрийте запит на злиття (pull request) у `main`.

Прохання:

- один запит — одна зміна;
- не послаблюйте захист: шифрування, перевірку сертифікатів і ключів хостів, роботу з паролями;
- коментарі в коді й повідомлення комітів — англійською, як у решті коду;
- нові параметри інтерфейсу додавайте в [docs/UI-MAP.md](docs/UI-MAP.md).

## Будова програми

Докладно — у [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Основні папки:

| Папка | Що там |
|---|---|
| `mRemoteNG/Connection/` | модель підключень і протоколи |
| `mRemoteNG/Config/` | збереження й завантаження підключень і налаштувань |
| `mRemoteNG/Security/` | шифрування, виведення ключа, пароль програми (`AppPasswordPolicy.cs`) |
| `mRemoteNG/UI/` | вікна, меню, панелі, теми |
| `mRemoteNG/Tree/` | дерево підключень |
| `ExternalConnectors/` | інтеграції зі сховищами паролів |
| `mRemoteNGInstaller/` | інсталятор MSI (WiX) |

## Повідомлення про помилки

Пишіть у [Issues на GitHub](https://github.com/mishka1c/mRemoteNX/issues) або
[на Gitea](https://gitea.mykino.uk/mishka1c/mRemoteNX/issues). Вкажіть версію mRemoteNX і Windows,
кроки для відтворення і, якщо можна, додайте архів з **Help → Generate Debug Bundle**.

Про вразливості повідомляйте приватно — див. [SECURITY.md](SECURITY.md).
