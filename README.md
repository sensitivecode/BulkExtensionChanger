# BulkExtensionChanger (Массовая смена расширений)

Локальная кроссплатформенная утилита для массовой смены расширений файлов через контекстное меню файлового менеджера, полностью без интернета.

## Поддерживаемые расширения (выпадающий список + ручной ввод)
- **Изображения**: `png`, `jpg`, `jpeg`, `webp`, `gif`, `bmp`, `ico`, `svg`, `tiff`, `tif`, `avif`, `heic`
- **Текст и данные**: `txt`, `md`, `json`, `xml`, `yaml`, `yml`, `csv`, `log`, `ini`, `cfg`, `conf`, `env`
- **Код и скрипты**: `html`, `htm`, `css`, `js`, `ts`, `jsx`, `tsx`, `py`, `cs`, `cpp`, `c`, `h`, `java`, `go`, `rs`, `sh`, `bat`, `ps1`, `sql`
- **Своё расширение**: возможность ввести любое произвольное расширение вручную

## Возможности
- Предварительно выделенные файлы открываются **в одном окне** общим списком предпросмотра.
- Работа с отдельными файлами, папками и вложенными подпапками.
- Разрешение конфликтов имён: автонумерация `(1)`, `(2)...`, пропуск или перезапись.
- Откат последнего переименования (**Отменить последнее**).

## Установщики для 3 платформ
1. **Windows (`BulkExtensionChanger-Setup.exe`)**
   - Требуется: Windows 10/11 (x64), [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Мастер установки добавляет пункт в контекстное меню Проводника (`HKCU\Software\Classes`), ярлыки и регистрацию в «Приложениях и возможностях» с деинсталлятором.
2. **macOS (`BulkExtensionChanger-macOS.zip` / `.dmg` в Releases)**
   - Требуется: macOS 12+, Python 3 с `tkinter`
   - Внутри: `BulkExtensionChanger.app`, служба Finder Quick Action (`Массовая смена расширений.workflow`) и скрипты `Install-macOS.command` / `Uninstall-macOS.command`.
3. **Linux (`BulkExtensionChanger-Linux.tar.gz` / `.deb` в Releases)**
   - Требуется: `python3`, `python3-tk`
   - Скрипт `install.sh` добавляет приложение в меню и контекстное меню для **Nautilus (GNOME Files)**, **KDE Dolphin** и **Cinnamon Nemo**; `uninstall.sh` удаляет всё обратно.

## Сборка из исходников (на Windows)
```powershell
powershell -ExecutionPolicy Bypass -File .\build-all.ps1
```

## Лицензия
Проект распространяется под свободной лицензией **GNU General Public License v3.0 (GPL-3.0)**.
