#!/bin/bash
set -e
rm -rf "$HOME/.local/share/bulk-extension-changer"
rm -f "$HOME/.local/bin/bulk-extension-changer"
rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/bulk-extension-changer.png"
rm -f "$HOME/.local/share/applications/bulk-extension-changer.desktop"
rm -f "$HOME/.local/share/nautilus/scripts/Массовая смена расширений"
rm -f "$HOME/.local/share/kio/servicemenus/bulk-extension-changer.desktop"
rm -f "$HOME/.local/share/nemo/actions/bulk-extension-changer.nemo_action"
echo "Массовая смена расширений удалена."
