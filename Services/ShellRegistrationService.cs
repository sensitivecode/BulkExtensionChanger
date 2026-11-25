using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BulkExtensionChanger.Services;

public sealed record RegistrationStatus(bool IsInstalled, string? RegisteredExePath, bool MatchesCurrentExecutable);

public static class ShellRegistrationService
{
    public const string VerbKeyName = "BulkExtensionChanger";
    public const string MenuTitle = "Массовая смена расширений...";
    public static readonly Guid DropTargetClsid = new("A8E3F4B2-7C91-4D5A-9E12-3B6F8C1D4A90");

    private static string ClsidRegistryPath => $@"Software\Classes\CLSID\{{{DropTargetClsid:D}}}";

    private static readonly string[] DropTargetVerbPaths =
    [
        $@"Software\Classes\*\shell\{VerbKeyName}",
        $@"Software\Classes\Directory\shell\{VerbKeyName}"
    ];

    private static readonly string BackgroundVerbPath =
        $@"Software\Classes\Directory\Background\shell\{VerbKeyName}";

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static void NotifyShellChanged()
        => SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);

    public static RegistrationStatus GetStatus()
    {
        var currentExe = Environment.ProcessPath ?? string.Empty;
        using var localServerKey = Registry.CurrentUser.OpenSubKey($@"{ClsidRegistryPath}\LocalServer32");
        using var fileVerbKey = Registry.CurrentUser.OpenSubKey(DropTargetVerbPaths[0]);

        if (localServerKey is null || fileVerbKey is null)
            return new RegistrationStatus(false, null, false);

        var cmd = localServerKey.GetValue(null) as string;
        var registeredExe = ExtractExePath(cmd);
        var matches = !string.IsNullOrEmpty(registeredExe) &&
                      !string.IsNullOrEmpty(currentExe) &&
                      string.Equals(registeredExe, currentExe, StringComparison.OrdinalIgnoreCase);

        return new RegistrationStatus(true, registeredExe, matches);
    }

    public static void Install(string? exePathOverride = null)
    {
        var exePath = exePathOverride ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

        var clsidBraced = $"{{{DropTargetClsid:D}}}";
        var iconValue = $"\"{exePath}\",0";

        // 1. Register COM LocalServer32 for multi-select IDropTarget
        using (var clsidKey = Registry.CurrentUser.CreateSubKey(ClsidRegistryPath))
        {
            clsidKey.SetValue(null, "BulkExtensionChanger DropTarget");
        }

        using (var localServerKey = Registry.CurrentUser.CreateSubKey($@"{ClsidRegistryPath}\LocalServer32"))
        {
            localServerKey.SetValue(null, $"\"{exePath}\" --com-server");
        }

        // 2. Register context menu for Files (*) and Folders (Directory)
        foreach (var verbPath in DropTargetVerbPaths)
        {
            using var verbKey = Registry.CurrentUser.CreateSubKey(verbPath);
            verbKey.SetValue("MUIVerb", MenuTitle);
            verbKey.SetValue("Icon", iconValue);
            verbKey.SetValue("MultiSelectModel", "Player");

            using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{verbPath}\command");
            cmdKey.SetValue(null, $"\"{exePath}\" \"%1\"");
            cmdKey.SetValue("DropTarget", clsidBraced);
        }

        // 3. Register context menu for Directory Background (right-click empty space in folder)
        using (var bgKey = Registry.CurrentUser.CreateSubKey(BackgroundVerbPath))
        {
            bgKey.SetValue("MUIVerb", MenuTitle);
            bgKey.SetValue("Icon", iconValue);

            using var bgCmdKey = Registry.CurrentUser.CreateSubKey($@"{BackgroundVerbPath}\command");
            bgCmdKey.SetValue(null, $"\"{exePath}\" \"%V\"");
        }

        NotifyShellChanged();
    }

    public static void Uninstall()
    {
        foreach (var verbPath in DropTargetVerbPaths)
        {
            Registry.CurrentUser.DeleteSubKeyTree(verbPath, throwOnMissingSubKey: false);
        }

        Registry.CurrentUser.DeleteSubKeyTree(BackgroundVerbPath, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ClsidRegistryPath, throwOnMissingSubKey: false);

        NotifyShellChanged();
    }

    private static string? ExtractExePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        var trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
                return trimmed[1..endQuote];
        }

        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    }
}
