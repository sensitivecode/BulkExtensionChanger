using System.IO;
using BulkExtensionChanger.Models;

namespace BulkExtensionChanger.Services;

public sealed record RenameBatchResult(
    int RenamedCount,
    int SkippedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<UndoEntry> UndoEntries);

public static class FileRenamerService
{
    public static IReadOnlyList<ExtensionOption> SupportedExtensions { get; } =
    [
        // Изображения
        new ExtensionOption("png", ".png — Изображение PNG", "Изображение PNG"),
        new ExtensionOption("jpg", ".jpg — Изображение JPG", "Изображение JPG"),
        new ExtensionOption("jpeg", ".jpeg — Изображение JPEG", "Изображение JPEG"),
        new ExtensionOption("webp", ".webp — Изображение WebP", "Изображение WebP"),
        new ExtensionOption("gif", ".gif — Анимация GIF", "Изображение GIF"),
        new ExtensionOption("bmp", ".bmp — Растровое BMP", "Изображение BMP"),
        new ExtensionOption("ico", ".ico — Значок ICO", "Значок Windows"),
        new ExtensionOption("svg", ".svg — Векторное SVG", "Векторная графика SVG"),
        new ExtensionOption("tiff", ".tiff — Изображение TIFF", "Изображение TIFF"),
        new ExtensionOption("tif", ".tif — Изображение TIF", "Изображение TIF"),
        new ExtensionOption("avif", ".avif — Изображение AVIF", "Изображение AVIF"),
        new ExtensionOption("heic", ".heic — Фото HEIC", "Изображение HEIC"),

        // Текст, документы и конфиги
        new ExtensionOption("txt", ".txt — Текстовый файл", "Обычный текст"),
        new ExtensionOption("md", ".md — Markdown", "Документ Markdown"),
        new ExtensionOption("json", ".json — JSON", "Данные JSON"),
        new ExtensionOption("xml", ".xml — XML", "Документ XML"),
        new ExtensionOption("yaml", ".yaml — YAML", "Конфиг YAML"),
        new ExtensionOption("yml", ".yml — YML", "Конфиг YML"),
        new ExtensionOption("csv", ".csv — Таблица CSV", "Таблица CSV"),
        new ExtensionOption("log", ".log — Файл журнала (Log)", "Лог-файл"),
        new ExtensionOption("ini", ".ini — Конфиг INI", "Настройки INI"),
        new ExtensionOption("cfg", ".cfg — Конфиг CFG", "Конфиг CFG"),
        new ExtensionOption("conf", ".conf — Конфиг CONF", "Конфиг CONF"),
        new ExtensionOption("env", ".env — Файл .env", "Переменные окружения"),

        // Код и скрипты
        new ExtensionOption("html", ".html — Страница HTML", "Веб-страница HTML"),
        new ExtensionOption("htm", ".htm — Страница HTM", "Веб-страница HTM"),
        new ExtensionOption("css", ".css — Стили CSS", "Таблица стилей CSS"),
        new ExtensionOption("js", ".js — JavaScript", "Скрипт JavaScript"),
        new ExtensionOption("ts", ".ts — TypeScript", "Скрипт TypeScript"),
        new ExtensionOption("jsx", ".jsx — React JSX", "Компонент JSX"),
        new ExtensionOption("tsx", ".tsx — React TSX", "Компонент TSX"),
        new ExtensionOption("py", ".py — Python", "Скрипт Python"),
        new ExtensionOption("cs", ".cs — C#", "Исходный код C#"),
        new ExtensionOption("cpp", ".cpp — C++", "Исходный код C++"),
        new ExtensionOption("c", ".c — C", "Исходный код C"),
        new ExtensionOption("h", ".h — Заголовок C/C++", "Заголовочный файл"),
        new ExtensionOption("java", ".java — Java", "Исходный код Java"),
        new ExtensionOption("go", ".go — Go", "Исходный код Go"),
        new ExtensionOption("rs", ".rs — Rust", "Исходный код Rust"),
        new ExtensionOption("sh", ".sh — Bash / Shell", "Скрипт Shell"),
        new ExtensionOption("bat", ".bat — Скрипт BAT", "Пакетный файл Windows"),
        new ExtensionOption("ps1", ".ps1 — PowerShell", "Скрипт PowerShell"),
        new ExtensionOption("sql", ".sql — SQL", "SQL-запрос"),

        // Пользовательский ввод
        new ExtensionOption("", "Своё расширение (ввести вручную)...", "Произвольное расширение", IsCustom: true)
    ];

    public static string SanitizeExtension(string? rawExtension)
    {
        if (string.IsNullOrWhiteSpace(rawExtension))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(rawExtension
            .Trim()
            .TrimStart('.')
            .Where(c => !char.IsWhiteSpace(c) && Array.IndexOf(invalid, c) < 0 && c != '.')
            .ToArray());

        return cleaned.ToLowerInvariant();
    }

    public static IReadOnlyList<CollisionModeOption> CollisionModes { get; } =
    [
        new CollisionModeOption(CollisionMode.AutoSuffix, "Добавить номер (1), (2)..."),
        new CollisionModeOption(CollisionMode.Skip, "Пропустить если файл уже есть"),
        new CollisionModeOption(CollisionMode.Overwrite, "Перезаписать существующий")
    ];

    public static List<FileRenameItem> CollectFiles(IEnumerable<string> inputPaths, bool includeSubdirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FileRenameItem>();

        foreach (var rawPath in inputPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                continue;

            var cleanPath = rawPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Restore root slash for drive roots like "C:"
            if (cleanPath.Length == 2 && cleanPath[1] == ':')
                cleanPath += Path.DirectorySeparatorChar;

            try
            {
                if (File.Exists(cleanPath))
                {
                    AddFile(cleanPath, seen, items);
                }
                else if (Directory.Exists(cleanPath))
                {
                    var searchOption = includeSubdirectories
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    foreach (var file in EnumerateFilesSafe(cleanPath, searchOption))
                    {
                        AddFile(file, seen, items);
                    }
                }
            }
            catch
            {
                // Ignore inaccessible paths during collection
            }
        }

        items.Sort((a, b) => string.Compare(a.OriginalPath, b.OriginalPath, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootDir, SearchOption searchOption)
    {
        var dirs = new Stack<string>();
        dirs.Push(rootDir);

        while (dirs.Count > 0)
        {
            var currentDir = dirs.Pop();
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
                yield return f;

            if (searchOption == SearchOption.AllDirectories)
            {
                try
                {
                    foreach (var sub in Directory.GetDirectories(currentDir))
                    {
                        var attrs = File.GetAttributes(sub);
                        if ((attrs & FileAttributes.ReparsePoint) == 0)
                            dirs.Push(sub);
                    }
                }
                catch
                {
                    // Skip inaccessible subdirectories
                }
            }
        }
    }

    private static void AddFile(string fullPath, HashSet<string> seen, List<FileRenameItem> items)
    {
        var normalized = Path.GetFullPath(fullPath);
        if (!seen.Add(normalized))
            return;

        var fileName = Path.GetFileName(normalized);
        var ext = Path.GetExtension(normalized);
        var dir = Path.GetDirectoryName(normalized) ?? string.Empty;

        items.Add(new FileRenameItem
        {
            OriginalPath = normalized,
            FileName = fileName,
            DirectoryPath = dir,
            CurrentExtension = string.IsNullOrEmpty(ext) ? "(без расш.)" : ext.ToLowerInvariant()
        });
    }

    public static void BuildPreview(
        IList<FileRenameItem> items,
        string targetExtension,
        CollisionMode collisionMode)
    {
        var normalizedExt = targetExtension.Trim().TrimStart('.').ToLowerInvariant();
        var dotExt = "." + normalizedExt;

        // Paths that will stay untouched on disk (either unselected or already matching extension)
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var movingOriginals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var currentExtNoDot = Path.GetExtension(item.OriginalPath).TrimStart('.');
            var alreadyTarget = string.Equals(currentExtNoDot, normalizedExt, StringComparison.OrdinalIgnoreCase);

            if (!item.IsSelected || alreadyTarget)
            {
                reservedPaths.Add(item.OriginalPath);
            }
            else
            {
                movingOriginals.Add(item.OriginalPath);
            }
        }

        foreach (var item in items)
        {
            var currentExtNoDot = Path.GetExtension(item.OriginalPath).TrimStart('.');
            var alreadyTarget = string.Equals(currentExtNoDot, normalizedExt, StringComparison.OrdinalIgnoreCase);

            if (!item.IsSelected)
            {
                item.NewFileName = item.FileName;
                item.TargetPath = item.OriginalPath;
                item.IsUnchanged = true;
                item.HasConflict = false;
                item.StatusText = "Снят выбор";
                continue;
            }

            if (alreadyTarget)
            {
                item.NewFileName = item.FileName;
                item.TargetPath = item.OriginalPath;
                item.IsUnchanged = true;
                item.HasConflict = false;
                item.StatusText = "Уже " + dotExt;
                continue;
            }

            var baseName = Path.GetFileNameWithoutExtension(item.OriginalPath);
            var candidate = Path.Combine(item.DirectoryPath, baseName + dotExt);

            bool ExistsAndNotMovingAway(string path) =>
                reservedPaths.Contains(path) || (File.Exists(path) && !movingOriginals.Contains(path));

            if (!ExistsAndNotMovingAway(candidate))
            {
                reservedPaths.Add(candidate);
                item.TargetPath = candidate;
                item.NewFileName = Path.GetFileName(candidate);
                item.IsUnchanged = false;
                item.HasConflict = false;
                item.StatusText = "Готов к смене";
                continue;
            }

            switch (collisionMode)
            {
                case CollisionMode.AutoSuffix:
                {
                    var index = 1;
                    string suffixed;
                    do
                    {
                        suffixed = Path.Combine(item.DirectoryPath, $"{baseName} ({index}){dotExt}");
                        index++;
                    } while (ExistsAndNotMovingAway(suffixed));

                    reservedPaths.Add(suffixed);
                    item.TargetPath = suffixed;
                    item.NewFileName = Path.GetFileName(suffixed);
                    item.IsUnchanged = false;
                    item.HasConflict = false;
                    item.StatusText = $"С суффиксом ({index - 1})";
                    break;
                }
                case CollisionMode.Skip:
                {
                    item.TargetPath = item.OriginalPath;
                    item.NewFileName = item.FileName;
                    item.IsUnchanged = true;
                    item.HasConflict = true;
                    item.StatusText = "Пропуск (имя занято)";
                    break;
                }
                case CollisionMode.Overwrite:
                {
                    reservedPaths.Add(candidate);
                    item.TargetPath = candidate;
                    item.NewFileName = Path.GetFileName(candidate);
                    item.IsUnchanged = false;
                    item.HasConflict = true;
                    item.StatusText = "Перезапишет файл";
                    break;
                }
            }
        }
    }

    public static RenameBatchResult ExecuteRename(IList<FileRenameItem> items, CollisionMode collisionMode)
    {
        var renamed = 0;
        var skipped = 0;
        var errors = new List<string>();
        var undoEntries = new List<UndoEntry>();

        // Order so files that vacate a path move before files taking that path if needed,
        // or use two-phase temp names if a cycle/chain exists inside the batch.
        var toRename = items.Where(i => i.IsSelected && !i.IsUnchanged &&
            !string.Equals(i.OriginalPath, i.TargetPath, StringComparison.OrdinalIgnoreCase)).ToList();

        skipped = items.Count - toRename.Count;

        // Check for target collisions within the batch where target currently exists as another batch item's original path
        var targetToTemp = new List<(FileRenameItem Item, string TempPath)>();
        var originalsSet = new HashSet<string>(toRename.Select(i => i.OriginalPath), StringComparer.OrdinalIgnoreCase);

        foreach (var item in toRename)
        {
            if (!File.Exists(item.OriginalPath))
            {
                errors.Add($"{item.FileName}: исходный файл не найден");
                continue;
            }

            if (originalsSet.Contains(item.TargetPath))
            {
                // Stage through a temporary path so chained renames cannot fail
                var tempPath = Path.Combine(
                    item.DirectoryPath,
                    $".__bec_tmp_{Guid.NewGuid():N}_{item.FileName}");
                try
                {
                    File.Move(item.OriginalPath, tempPath);
                    targetToTemp.Add((item, tempPath));
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var overwrite = collisionMode == CollisionMode.Overwrite;
                    var targetExistedBefore = File.Exists(item.TargetPath);
                    File.Move(item.OriginalPath, item.TargetPath, overwrite);
                    renamed++;
                    if (!targetExistedBefore)
                        undoEntries.Add(new UndoEntry(item.OriginalPath, item.TargetPath));
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }
        }

        foreach (var (item, tempPath) in targetToTemp)
        {
            try
            {
                var overwrite = collisionMode == CollisionMode.Overwrite;
                var targetExistedBefore = File.Exists(item.TargetPath);
                File.Move(tempPath, item.TargetPath, overwrite);
                renamed++;
                if (!targetExistedBefore)
                    undoEntries.Add(new UndoEntry(item.OriginalPath, item.TargetPath));
            }
            catch (Exception ex)
            {
                // Attempt rollback to original path
                try
                {
                    if (File.Exists(tempPath) && !File.Exists(item.OriginalPath))
                        File.Move(tempPath, item.OriginalPath);
                }
                catch { }
                errors.Add($"{item.FileName}: {ex.Message}");
            }
        }

        return new RenameBatchResult(renamed, skipped, errors, undoEntries);
    }

    public static (int RestoredCount, List<string> Errors) UndoRename(IReadOnlyList<UndoEntry> entries)
    {
        var restored = 0;
        var errors = new List<string>();

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            try
            {
                if (!File.Exists(entry.RenamedPath))
                {
                    errors.Add($"{Path.GetFileName(entry.RenamedPath)}: файл не найден");
                    continue;
                }

                if (File.Exists(entry.OriginalPath))
                {
                    errors.Add($"{Path.GetFileName(entry.OriginalPath)}: исходное имя уже занято");
                    continue;
                }

                File.Move(entry.RenamedPath, entry.OriginalPath);
                restored++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(entry.RenamedPath)}: {ex.Message}");
            }
        }

        return (restored, errors);
    }
}
