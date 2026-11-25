#!/bin/bash
set -e
rm -rf "$HOME/Applications/BulkExtensionChanger.app"
rm -rf "$HOME/Library/Services/Массовая смена расширений.workflow"
/System/Library/CoreServices/pbs -flush >/dev/null 2>&1 || true
echo "Приложение и пункт меню Finder успешно удалены."
