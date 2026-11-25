#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP_DIR="$HOME/.local/share/bulk-extension-changer"
BIN_DIR="$HOME/.local/bin"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
APPS_DIR="$HOME/.local/share/applications"
NAUTILUS_DIR="$HOME/.local/share/nautilus/scripts"
DOLPHIN_DIR="$HOME/.local/share/kio/servicemenus"
NEMO_DIR="$HOME/.local/share/nemo/actions"

echo "=== Установка Массовая смена расширений (Linux) ==="
if ! command -v python3 >/dev/null 2>&1; then
  echo "Ошибка: требуется python3."
  exit 1
fi

if ! python3 -c "import tkinter" >/dev/null 2>&1; then
  echo "Внимание: модуль python3-tk не найден. Установите его, например:"
  echo "  Ubuntu/Debian: sudo apt install python3-tk"
  echo "  Fedora:        sudo dnf install python3-tkinter"
  echo "  Arch:          sudo pacman -S tk"
fi

mkdir -p "$APP_DIR" "$BIN_DIR" "$ICON_DIR" "$APPS_DIR" "$NAUTILUS_DIR" "$DOLPHIN_DIR" "$NEMO_DIR"

cp "$SCRIPT_DIR/bulk_extension_changer.py" "$APP_DIR/bulk_extension_changer.py"
chmod +x "$APP_DIR/bulk_extension_changer.py"
if [ -f "$SCRIPT_DIR/app.png" ]; then
  cp "$SCRIPT_DIR/app.png" "$ICON_DIR/bulk-extension-changer.png"
fi

cat > "$BIN_DIR/bulk-extension-changer" <<EOF
#!/bin/bash
exec python3 "$APP_DIR/bulk_extension_changer.py" "\$@"
EOF
chmod +x "$BIN_DIR/bulk-extension-changer"

# Ярлык в меню приложений
cat > "$APPS_DIR/bulk-extension-changer.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Массовая смена расширений
Name[en]=Bulk Extension Changer
Comment=Массовое переименование расширений файлов
Exec=$BIN_DIR/bulk-extension-changer %F
Icon=bulk-extension-changer
Terminal=false
Categories=Utility;FileTools;
EOF

# Контекстное меню GNOME Files (Nautilus) -> Сценарии
cat > "$NAUTILUS_DIR/Массовая смена расширений" <<EOF
#!/bin/bash
exec python3 "$APP_DIR/bulk_extension_changer.py" "\$@"
EOF
chmod +x "$NAUTILUS_DIR/Массовая смена расширений"

# Контекстное меню KDE Dolphin
cat > "$DOLPHIN_DIR/bulk-extension-changer.desktop" <<EOF
[Desktop Entry]
Type=Service
MimeType=all/allfiles;inode/directory;
Actions=BulkChangeExt;
X-KDE-Priority=TopLevel

[Desktop Action BulkChangeExt]
Name=Массовая смена расширений...
Name[en]=Bulk Extension Changer...
Icon=bulk-extension-changer
Exec=$BIN_DIR/bulk-extension-changer %F
EOF
chmod +x "$DOLPHIN_DIR/bulk-extension-changer.desktop"

# Контекстное меню Cinnamon Nemo
cat > "$NEMO_DIR/bulk-extension-changer.nemo_action" <<EOF
[Nemo Action]
Name=Массовая смена расширений...
Comment=Изменить расширения выбранных файлов
Exec=$BIN_DIR/bulk-extension-changer %F
Icon-Name=bulk-extension-changer
Selection=any
Extensions=any;
EOF

echo "Готово! Интеграция добавлена для Nautilus, Dolphin и Nemo."
echo "Для удаления запустите: $SCRIPT_DIR/uninstall.sh"
