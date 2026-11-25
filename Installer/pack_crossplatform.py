import io
import os
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / "dist"
APP_PY = ROOT / "CrossPlatform" / "bulk_extension_changer.py"
MAC_SRC = ROOT / "CrossPlatform" / "macos"
LIN_SRC = ROOT / "CrossPlatform" / "linux"
ICO_PATH = ROOT / "Assets" / "app.ico"
PNG_PATH = ROOT / "Assets" / "app.png"


def extract_256_png_from_ico() -> bytes:
    data = ICO_PATH.read_bytes()
    # Our ICO stores 4 PNG images, last one is 256x256
    import struct
    count = struct.unpack_from("<HHH", data, 0)[2]
    best = b""
    for i in range(count):
        w, h, _, _, _, _, size, offset = struct.unpack_from("<BBBBHHII", data, 6 + i * 16)
        chunk = data[offset : offset + size]
        if w == 0 or len(chunk) > len(best):
            best = chunk
    PNG_PATH.write_bytes(best)
    return best


def lf_bytes(text: str) -> bytes:
    return text.replace("\r\n", "\n").encode("utf-8")


def add_zip_file(zf: zipfile.ZipFile, arcname: str, data: bytes, executable: bool = False) -> None:
    zi = zipfile.ZipInfo(arcname)
    mode = 0o755 if executable else 0o644
    zi.external_attr = (0o100000 | mode) << 16
    zf.writestr(zi, data)


def build_macos_zip() -> Path:
    out_zip = DIST / "BulkExtensionChanger-macOS.zip"
    app_py_bytes = lf_bytes(APP_PY.read_text(encoding="utf-8"))

    info_plist = lf_bytes("""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>Массовая смена расширений</string>
  <key>CFBundleDisplayName</key>
  <string>Массовая смена расширений</string>
  <key>CFBundleIdentifier</key>
  <string>com.bulkextensionchanger.app</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleExecutable</key>
  <string>launch.sh</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
""")

    launch_sh = lf_bytes("""#!/bin/bash
DIR="$(cd "$(dirname "$0")/../Resources" && pwd)"
exec python3 "$DIR/bulk_extension_changer.py" "$@"
""")

    wf_info_plist = lf_bytes("""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>NSServices</key>
  <array>
    <dict>
      <key>NSMenuItem</key>
      <dict>
        <key>default</key>
        <string>Массовая смена расширений...</string>
      </dict>
      <key>NSMessage</key>
      <string>runWorkflowAsService</string>
      <key>NSSendFileTypes</key>
      <array>
        <string>public.item</string>
        <string>public.folder</string>
      </array>
    </dict>
  </array>
</dict>
</plist>
""")

    wf_document = lf_bytes("""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>AMApplicationBuild</key>
  <string>523</string>
  <key>AMApplicationVersion</key>
  <string>2.10</string>
  <key>AMDocumentVersion</key>
  <string>2</string>
  <key>actions</key>
  <array>
    <dict>
      <key>action</key>
      <dict>
        <key>AMAccepts</key>
        <dict>
          <key>Container</key>
          <string>List</string>
          <key>Optional</key>
          <true/>
          <key>Types</key>
          <array>
            <string>com.apple.cocoa.path</string>
          </array>
        </dict>
        <key>AMActionVersion</key>
        <string>2.0.3</string>
        <key>AMApplication</key>
        <array><string>Automator</string></array>
        <key>AMCategory</key>
        <string>AMCategoryUtilities</string>
        <key>AMProvides</key>
        <dict>
          <key>Container</key>
          <string>List</string>
          <key>Types</key>
          <array>
            <string>com.apple.cocoa.path</string>
          </array>
        </dict>
        <key>ActionBundlePath</key>
        <string>/System/Library/Automator/Run Shell Script.action</string>
        <key>ActionName</key>
        <string>Run Shell Script</string>
        <key>ActionParameters</key>
        <dict>
          <key>COMMAND_STRING</key>
          <string>python3 "$HOME/Applications/BulkExtensionChanger.app/Contents/Resources/bulk_extension_changer.py" "$@"</string>
          <key>CheckedForUserDefaultShell</key>
          <true/>
          <key>inputMethod</key>
          <integer>1</integer>
          <key>shell</key>
          <string>/bin/bash</string>
          <key>source</key>
          <string></string>
        </dict>
        <key>BundleIdentifier</key>
        <string>com.apple.RunShellScript</string>
        <key>CFBundleVersion</key>
        <string>2.0.3</string>
        <key>Class Name</key>
        <string>RunShellScriptAction</string>
        <key>InputUUID</key>
        <string>5E7B8A10-1234-4B56-89AB-111111111111</string>
        <key>OutputUUID</key>
        <string>5E7B8A10-1234-4B56-89AB-222222222222</string>
        <key>UUID</key>
        <string>5E7B8A10-1234-4B56-89AB-333333333333</string>
      </dict>
    </dict>
  </array>
  <key>connectors</key>
  <dict/>
  <key>workflowMetaData</key>
  <dict>
    <key>serviceInputTypeIdentifier</key>
    <string>com.apple.Automator.fileSystemObject</string>
    <key>serviceOutputTypeIdentifier</key>
    <string>com.apple.Automator.nothing</string>
    <key>workflowTypeIdentifier</key>
    <string>com.apple.Automator.servicesMenu</string>
  </dict>
</dict>
</plist>
""")

    with zipfile.ZipFile(out_zip, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        base = "BulkExtensionChanger-macOS"
        add_zip_file(zf, f"{base}/Install-macOS.command", lf_bytes((MAC_SRC / "Install-macOS.command").read_text("utf-8")), executable=True)
        add_zip_file(zf, f"{base}/Uninstall-macOS.command", lf_bytes((MAC_SRC / "Uninstall-macOS.command").read_text("utf-8")), executable=True)
        add_zip_file(zf, f"{base}/BulkExtensionChanger.app/Contents/Info.plist", info_plist)
        add_zip_file(zf, f"{base}/BulkExtensionChanger.app/Contents/MacOS/launch.sh", launch_sh, executable=True)
        add_zip_file(zf, f"{base}/BulkExtensionChanger.app/Contents/Resources/bulk_extension_changer.py", app_py_bytes, executable=True)
        add_zip_file(zf, f"{base}/BulkExtensionChanger.app/Contents/Resources/app.png", PNG_PATH.read_bytes())
        add_zip_file(zf, f"{base}/Массовая смена расширений.workflow/Contents/Info.plist", wf_info_plist)
        add_zip_file(zf, f"{base}/Массовая смена расширений.workflow/Contents/document.wflow", wf_document)

    return out_zip


def add_tar_bytes(tf: tarfile.TarFile, name: str, data: bytes, executable: bool = False) -> None:
    ti = tarfile.TarInfo(name)
    ti.size = len(data)
    ti.mode = 0o755 if executable else 0o644
    tf.addfile(ti, io.BytesIO(data))


def build_linux_targz() -> Path:
    out_tgz = DIST / "BulkExtensionChanger-Linux.tar.gz"
    app_py_bytes = lf_bytes(APP_PY.read_text(encoding="utf-8"))

    with tarfile.open(out_tgz, "w:gz") as tf:
        base = "BulkExtensionChanger-Linux"
        add_tar_bytes(tf, f"{base}/install.sh", lf_bytes((LIN_SRC / "install.sh").read_text("utf-8")), executable=True)
        add_tar_bytes(tf, f"{base}/uninstall.sh", lf_bytes((LIN_SRC / "uninstall.sh").read_text("utf-8")), executable=True)
        add_tar_bytes(tf, f"{base}/bulk_extension_changer.py", app_py_bytes, executable=True)
        add_tar_bytes(tf, f"{base}/app.png", PNG_PATH.read_bytes())

    return out_tgz


if __name__ == "__main__":
    DIST.mkdir(parents=True, exist_ok=True)
    extract_256_png_from_ico()
    mac = build_macos_zip()
    lin = build_linux_targz()
    print(f"macOS package: {mac} ({mac.stat().st_size} bytes)")
    print(f"Linux package: {lin} ({lin.stat().st_size} bytes)")
