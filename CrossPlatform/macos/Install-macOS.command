#!/bin/bash
set -e
export PATH="/opt/homebrew/bin:/usr/local/bin:/Library/Frameworks/Python.framework/Versions/Current/bin:$PATH"
DIR="$(cd "$(dirname "$0")" && pwd)"
APP_DEST="$HOME/Applications/BulkExtensionChanger.app"
SERV_DEST="$HOME/Library/Services/Массовая смена расширений.workflow"

echo "=== Установка Массовая смена расширений (macOS) ==="
if ! command -v python3 >/dev/null 2>&1; then
  echo "Ошибка: не найден python3. Установите Python 3 (https://www.python.org/downloads/macos/ или brew install python-tk)."
  exit 1
fi

mkdir -p "$HOME/Applications" "$HOME/Library/Services"
rm -rf "$APP_DEST" "$SERV_DEST"
cp -R "$DIR/BulkExtensionChanger.app" "$APP_DEST"
cp -R "$DIR/Массовая смена расширений.workflow" "$SERV_DEST"
chmod +x "$APP_DEST/Contents/MacOS/launch.sh"

# Обновить кэш служб Finder
/System/Library/CoreServices/pbs -flush >/dev/null 2>&1 || true

echo "Готово!"
echo "Приложение: $APP_DEST"
echo "Контекстное меню Finder: ПКМ по файлам -> Быстрые действия / Службы -> «Массовая смена расширений»"
