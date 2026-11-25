# BulkExtensionChanger (Массовая смена расширений)

[🇷🇺 Русский](#-русский) | [🇬🇧 English](#-english)

---

## 🇷🇺 Русский

Локальная кроссплатформенная утилита для массовой смены расширений файлов через контекстное меню файлового менеджера, работающая полностью без интернета.

> [!NOTE]
> **Важное примечание по macOS и Linux:**
> Версии и установщики для **Linux** и **macOS** не тестировались на реальных устройствах, так как у автора нет техники с этими операционными системами. Если вы столкнётесь с ошибками или проблемами интеграции — пожалуйста, создайте **Issue** в этом репозитории.

### Возможности
- Предварительно выделенные файлы в Проводнике открываются **в одном окне** общим списком предпросмотра.
- Интеграция в контекстное меню Windows (файлы, папки и пустая область папки) без прав администратора.
- Поддержка 43 популярных расширений + ручной ввод произвольного расширения:
  - **Изображения:** `png`, `jpg`, `jpeg`, `webp`, `gif`, `bmp`, `ico`, `svg`, `tiff`, `tif`, `avif`, `heic`
  - **Текст и данные:** `txt`, `md`, `json`, `xml`, `yaml`, `yml`, `csv`, `log`, `ini`, `cfg`, `conf`, `env`
  - **Код и скрипты:** `html`, `htm`, `css`, `js`, `ts`, `jsx`, `tsx`, `py`, `cs`, `cpp`, `c`, `h`, `java`, `go`, `rs`, `sh`, `bat`, `ps1`, `sql`
- Работа с отдельными файлами, папками и вложенными подпапками.
- Безопасная обработка конфликтов имён: автонумерация `(1)`, `(2)...`, пропуск или перезапись.
- Кнопка отката последнего переименования (**Отменить последнее**).

### Установка
1. **Windows (`BulkExtensionChanger-Setup.exe`)** — проверено на Windows 10/11 (x64). Требуется [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0). Мастер установки добавляет пункт контекстного меню, ярлыки и деинсталлятор в «Приложениях и возможностях».
2. **macOS (`BulkExtensionChanger-macOS.zip` / `.dmg`)** *(не тестировалось)* — требуется macOS 12+ и Python 3 с `tkinter`. Содержит `.app`, службу Finder Quick Action и скрипты `Install-macOS.command` / `Uninstall-macOS.command`.
3. **Linux (`BulkExtensionChanger-Linux.tar.gz` / `.deb`)** *(не тестировалось)* — требуется `python3` и `python3-tk`. Скрипт `install.sh` добавляет интеграцию для Nautilus, Dolphin и Nemo.

### Сборка на Windows
```powershell
powershell -ExecutionPolicy Bypass -File .\build-all.ps1
```

---

## 🇬🇧 English

Offline cross-platform utility for bulk changing file extensions directly from your file manager's context menu, working 100% locally without internet access.

> [!NOTE]
> **Important notice for macOS and Linux:**
> The **Linux** and **macOS** versions and installers have not been tested on real hardware because the author does not own devices running those operating systems. If you encounter any bugs or integration issues, please open an **Issue** in this repository.

### Features
- Pre-selected files in Windows Explorer open together **in a single window** with a live rename preview list.
- Windows Explorer context menu integration (files, folders, and folder background) without requiring administrator privileges.
- 43 preset extensions in a dropdown list + custom extension input:
  - **Images:** `png`, `jpg`, `jpeg`, `webp`, `gif`, `bmp`, `ico`, `svg`, `tiff`, `tif`, `avif`, `heic`
  - **Text & Data:** `txt`, `md`, `json`, `xml`, `yaml`, `yml`, `csv`, `log`, `ini`, `cfg`, `conf`, `env`
  - **Code & Scripts:** `html`, `htm`, `css`, `js`, `ts`, `jsx`, `tsx`, `py`, `cs`, `cpp`, `c`, `h`, `java`, `go`, `rs`, `sh`, `bat`, `ps1`, `sql`
- Supports individual files, folders, and recursive subfolders.
- Safe filename collision handling: auto-numbering `(1)`, `(2)...`, skip, or overwrite.
- One-click **Undo** for the last rename batch.

### Installation
1. **Windows (`BulkExtensionChanger-Setup.exe`)** — tested on Windows 10/11 (x64). Requires [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0). Includes setup wizard, Explorer context menu integration, shortcuts, and uninstaller in Apps & Features.
2. **macOS (`BulkExtensionChanger-macOS.zip` / `.dmg`)** *(untested)* — requires macOS 12+ and Python 3 with `tkinter`. Includes `.app` bundle, Finder Quick Action workflow, and `Install-macOS.command` / `Uninstall-macOS.command`.
3. **Linux (`BulkExtensionChanger-Linux.tar.gz` / `.deb`)** *(untested)* — requires `python3` and `python3-tk`. `install.sh` adds menu entries for Nautilus, Dolphin, and Nemo.

### Build on Windows
```powershell
powershell -ExecutionPolicy Bypass -File .\build-all.ps1
```

## License / Лицензия
Distributed under the **GNU General Public License v3.0 (GPL-3.0)**.
Распространяется под свободной лицензией **GNU General Public License v3.0 (GPL-3.0)**.
