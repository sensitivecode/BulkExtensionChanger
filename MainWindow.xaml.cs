using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BulkExtensionChanger.Models;
using BulkExtensionChanger.Services;
using Microsoft.Win32;

namespace BulkExtensionChanger;

public partial class MainWindow : Window
{
    private readonly List<string> _sourceInputs = [];
    private readonly ObservableCollection<FileRenameItem> _items = [];
    private readonly DispatcherTimer _ipcMergeDebounceTimer;
    private List<UndoEntry> _lastUndoEntries = [];
    private bool _isUpdatingChecks;

    public MainWindow(IReadOnlyList<string> initialPaths)
    {
        InitializeComponent();

        _ipcMergeDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _ipcMergeDebounceTimer.Tick += (_, _) =>
        {
            _ipcMergeDebounceTimer.Stop();
            ReloadItemsFromSources();
        };

        FilesListView.ItemsSource = _items;

        ExtensionComboBox.ItemsSource = FileRenamerService.SupportedExtensions;
        ExtensionComboBox.SelectedIndex = 0;

        CollisionComboBox.ItemsSource = FileRenamerService.CollisionModes;
        CollisionComboBox.SelectedIndex = 0;

        SourceInitialized += (_, _) => EnableDarkTitleBar();

        RefreshRegistrationStatus();

        if (initialPaths.Count > 0)
        {
            _sourceInputs.AddRange(initialPaths);
            ReloadItemsFromSources();
        }
        else
        {
            UpdateUiState();
        }
    }

    private void EnableDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int useDark = 1;
            const int DWMWA_USE_IMMUTABLE_DARK_MODE = 20;
            const int DWMWA_USE_IMMUTABLE_DARK_MODE_BEFORE_20H1 = 19;

            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMUTABLE_DARK_MODE, ref useDark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMUTABLE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
            }
        }
        catch
        {
            // Ignore on unsupported Windows builds
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private string SelectedExtension
    {
        get
        {
            if (ExtensionComboBox.SelectedItem is ExtensionOption opt)
            {
                if (opt.IsCustom)
                    return FileRenamerService.SanitizeExtension(CustomExtensionTextBox.Text);
                return opt.Extension;
            }
            return "png";
        }
    }

    private CollisionMode SelectedCollisionMode =>
        (CollisionComboBox.SelectedItem as CollisionModeOption)?.Mode ?? CollisionMode.AutoSuffix;

    private void RefreshRegistrationStatus()
    {
        var status = ShellRegistrationService.GetStatus();
        if (status.IsInstalled && status.MatchesCurrentExecutable)
        {
            MenuStatusText.Text = "Контекстное меню Windows: добавлено и готово к работе";
            MenuStatusText.Foreground = (Brush)FindResource("SuccessBrush");
            InstallMenuButton.Content = "Обновить в меню";
            UninstallMenuButton.IsEnabled = true;
        }
        else if (status.IsInstalled)
        {
            MenuStatusText.Text = $"Контекстное меню указывает на другой путь: {status.RegisteredExePath}";
            MenuStatusText.Foreground = (Brush)FindResource("WarningBrush");
            InstallMenuButton.Content = "Привязать этот EXE";
            UninstallMenuButton.IsEnabled = true;
        }
        else
        {
            MenuStatusText.Text = "Контекстное меню Windows: не добавлено";
            MenuStatusText.Foreground = (Brush)FindResource("TextSecondaryBrush");
            InstallMenuButton.Content = "Добавить в контекстное меню";
            UninstallMenuButton.IsEnabled = false;
        }
    }

    private void ReloadItemsFromSources()
    {
        var includeSub = IncludeSubfoldersCheckBox.IsChecked == true;
        var collected = FileRenamerService.CollectFiles(_sourceInputs, includeSub);

        _items.Clear();
        foreach (var item in collected)
            _items.Add(item);

        RebuildPreview();
    }

    private void RebuildPreview()
    {
        var ext = SelectedExtension;
        if (_items.Count > 0 && !string.IsNullOrEmpty(ext))
        {
            FileRenamerService.BuildPreview(_items, ext, SelectedCollisionMode);
        }
        else if (_items.Count > 0)
        {
            foreach (var item in _items)
            {
                item.NewFileName = item.FileName;
                item.TargetPath = item.OriginalPath;
                item.IsUnchanged = true;
                item.HasConflict = false;
                item.StatusText = "Введите расширение";
            }
        }

        UpdateUiState();
    }

    private void UpdateUiState()
    {
        EmptyStatePanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var ext = SelectedExtension;
        var readyCount = string.IsNullOrEmpty(ext)
            ? 0
            : _items.Count(i => i.IsSelected && !i.IsUnchanged);

        if (_items.Count == 0)
        {
            SummaryText.Text = "Файлов: 0";
        }
        else if (string.IsNullOrEmpty(ext))
        {
            SummaryText.Text = $"Всего файлов: {_items.Count} • Укажите целевое расширение";
        }
        else
        {
            SummaryText.Text = $"Всего файлов: {_items.Count} • К смене на .{ext}: {readyCount}";
        }

        ApplyButton.IsEnabled = readyCount > 0;
        ApplyButton.Content = readyCount > 0 ? $"Применить ({readyCount})" : "Применить";
        UndoButton.IsEnabled = _lastUndoEntries.Count > 0;

        _isUpdatingChecks = true;
        SelectAllCheckBox.IsChecked = _items.Count > 0 && _items.All(i => i.IsSelected);
        _isUpdatingChecks = false;
    }

    private void ExtensionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var isCustom = (ExtensionComboBox.SelectedItem as ExtensionOption)?.IsCustom == true;
        CustomExtensionPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        if (isCustom)
            CustomExtensionTextBox.Focus();

        RebuildPreview();
    }

    private void CustomExtensionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            RebuildPreview();
    }

    private void CollisionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            RebuildPreview();
    }

    private void IncludeSubfoldersCheckBox_Click(object sender, RoutedEventArgs e)
    {
        ReloadItemsFromSources();
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingChecks)
            return;

        var check = SelectAllCheckBox.IsChecked == true;
        foreach (var item in _items)
            item.IsSelected = check;

        RebuildPreview();
    }

    private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
    {
        RebuildPreview();
    }

    private void InstallMenuButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellRegistrationService.Install();
            RefreshRegistrationStatus();
            ShowFeedback("Пункт «Массовая смена расширений...» добавлен в контекстное меню Windows.", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось обновить реестр:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UninstallMenuButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellRegistrationService.Uninstall();
            RefreshRegistrationStatus();
            ShowFeedback("Пункт удалён из контекстного меню Windows.", isError: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось удалить из реестра:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Выберите файлы для смены расширения"
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddSourcePaths(dialog.FileNames);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = true,
            Title = "Выберите папку с файлами"
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddSourcePaths(dialog.FolderNames);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _sourceInputs.Clear();
        _items.Clear();
        LastActionText.Visibility = Visibility.Collapsed;
        UpdateUiState();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var result = FileRenamerService.ExecuteRename(_items, SelectedCollisionMode);

        if (result.UndoEntries.Count > 0)
            _lastUndoEntries = result.UndoEntries.ToList();

        // Update source paths so renamed files stay visible with their new names
        var renamedMap = result.UndoEntries.ToDictionary(
            u => u.OriginalPath,
            u => u.RenamedPath,
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _sourceInputs.Count; i++)
        {
            if (renamedMap.TryGetValue(_sourceInputs[i], out var newPath))
                _sourceInputs[i] = newPath;
        }

        ReloadItemsFromSources();

        if (result.Errors.Count > 0)
        {
            var msg = $"Переименовано: {result.RenamedCount}. Ошибок: {result.Errors.Count}\n\n" +
                      string.Join("\n", result.Errors.Take(10));
            MessageBox.Show(this, msg, "Завершено с ошибками", MessageBoxButton.OK, MessageBoxImage.Warning);
            ShowFeedback($"Переименовано: {result.RenamedCount}, ошибок: {result.Errors.Count}", isError: true);
        }
        else
        {
            ShowFeedback($"Готово! Расширение изменено у файлов: {result.RenamedCount}.", isError: false);
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastUndoEntries.Count == 0)
            return;

        var (restored, errors) = FileRenamerService.UndoRename(_lastUndoEntries);

        var restoreMap = _lastUndoEntries.ToDictionary(
            u => u.RenamedPath,
            u => u.OriginalPath,
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _sourceInputs.Count; i++)
        {
            if (restoreMap.TryGetValue(_sourceInputs[i], out var oldPath))
                _sourceInputs[i] = oldPath;
        }

        _lastUndoEntries.Clear();
        ReloadItemsFromSources();

        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n", errors.Take(10)), "Частичный откат", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ShowFeedback($"Откат выполнен. Восстановлено файлов: {restored}.", isError: errors.Count > 0);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped && dropped.Length > 0)
        {
            AddSourcePaths(dropped);
        }
    }

    public void AddPathsFromExternal(IReadOnlyList<string> paths)
    {
        var added = false;
        foreach (var p in paths)
        {
            if (!string.IsNullOrWhiteSpace(p) &&
                !_sourceInputs.Contains(p, StringComparer.OrdinalIgnoreCase))
            {
                _sourceInputs.Add(p);
                added = true;
            }
        }

        if (added && !_ipcMergeDebounceTimer.IsEnabled)
        {
            _ipcMergeDebounceTimer.Start();
        }

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    private void AddSourcePaths(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (!_sourceInputs.Contains(p, StringComparer.OrdinalIgnoreCase))
                _sourceInputs.Add(p);
        }
        ReloadItemsFromSources();
    }

    private void ShowFeedback(string text, bool isError)
    {
        LastActionText.Text = text;
        LastActionText.Foreground = (Brush)FindResource(isError ? "WarningBrush" : "SuccessBrush");
        LastActionText.Visibility = Visibility.Visible;
    }
}
