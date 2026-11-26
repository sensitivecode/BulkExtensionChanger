#!/usr/bin/env python3
"""BulkExtensionChanger — локальная кроссплатформенная утилита массовой смены расширений (macOS / Linux)."""

from __future__ import annotations

import os
import platform
import shutil
import sys
import uuid
import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from typing import Iterable

SUPPORTED_EXTENSIONS: list[tuple[str, str]] = [
    ("png", ".png — Изображение PNG"),
    ("jpg", ".jpg — Изображение JPG"),
    ("jpeg", ".jpeg — Изображение JPEG"),
    ("webp", ".webp — Изображение WebP"),
    ("gif", ".gif — Анимация GIF"),
    ("bmp", ".bmp — Растровое BMP"),
    ("ico", ".ico — Значок ICO"),
    ("svg", ".svg — Векторное SVG"),
    ("tiff", ".tiff — Изображение TIFF"),
    ("tif", ".tif — Изображение TIF"),
    ("avif", ".avif — Изображение AVIF"),
    ("heic", ".heic — Фото HEIC"),
    ("txt", ".txt — Текстовый файл"),
    ("md", ".md — Markdown"),
    ("json", ".json — JSON"),
    ("xml", ".xml — XML"),
    ("yaml", ".yaml — YAML"),
    ("yml", ".yml — YML"),
    ("csv", ".csv — Таблица CSV"),
    ("log", ".log — Файл журнала (Log)"),
    ("ini", ".ini — Конфиг INI"),
    ("cfg", ".cfg — Конфиг CFG"),
    ("conf", ".conf — Конфиг CONF"),
    ("env", ".env — Файл .env"),
    ("html", ".html — Страница HTML"),
    ("htm", ".htm — Страница HTM"),
    ("css", ".css — Стили CSS"),
    ("js", ".js — JavaScript"),
    ("ts", ".ts — TypeScript"),
    ("jsx", ".jsx — React JSX"),
    ("tsx", ".tsx — React TSX"),
    ("py", ".py — Python"),
    ("cs", ".cs — C#"),
    ("cpp", ".cpp — C++"),
    ("c", ".c — C"),
    ("h", ".h — Заголовок C/C++"),
    ("java", ".java — Java"),
    ("go", ".go — Go"),
    ("rs", ".rs — Rust"),
    ("sh", ".sh — Bash / Shell"),
    ("bat", ".bat — Скрипт BAT"),
    ("ps1", ".ps1 — PowerShell"),
    ("sql", ".sql — SQL"),
    ("__custom__", "Своё расширение (ввести вручную)..."),
]

COLLISION_MODES: list[tuple[str, str]] = [
    ("suffix", "Добавить номер (1), (2)..."),
    ("skip", "Пропустить если файл уже есть"),
    ("overwrite", "Перезаписать существующий"),
]


@dataclass
class FileRenameItem:
    original_path: str
    file_name: str
    directory: str
    selected: bool = True
    new_file_name: str = ""
    target_path: str = ""
    status_text: str = ""
    unchanged: bool = False
    conflict: bool = False


def sanitize_extension(raw: str) -> str:
    cleaned = "".join(ch for ch in raw.strip().lstrip(".") if ch not in '/\\:*?"<>|.' and not ch.isspace())
    return cleaned.lower()


def collect_files(input_paths: Iterable[str], include_subdirs: bool) -> list[FileRenameItem]:
    seen: set[str] = set()
    items: list[FileRenameItem] = []

    def add_file(path_str: str) -> None:
        full = os.path.abspath(path_str)
        norm = os.path.normcase(full)
        if norm in seen or not os.path.isfile(full):
            return
        seen.add(norm)
        items.append(
            FileRenameItem(
                original_path=full,
                file_name=os.path.basename(full),
                directory=os.path.dirname(full),
            )
        )

    for raw in input_paths:
        if not raw or not raw.strip():
            continue
        p = os.path.abspath(raw.strip())
        if os.path.isfile(p):
            add_file(p)
        elif os.path.isdir(p):
            if include_subdirs:
                for root, _, files in os.walk(p):
                    for fn in files:
                        add_file(os.path.join(root, fn))
            else:
                try:
                    for entry in os.scandir(p):
                        if entry.is_file():
                            add_file(entry.path)
                except OSError:
                    pass

    items.sort(key=lambda i: i.original_path.lower())
    return items


def build_preview(items: list[FileRenameItem], target_ext: str, collision_mode: str) -> None:
    ext = sanitize_extension(target_ext)
    if not ext:
        for item in items:
            item.new_file_name = item.file_name
            item.target_path = item.original_path
            item.unchanged = True
            item.conflict = False
            item.status_text = "Введите расширение"
        return

    dot_ext = "." + ext
    reserved: set[str] = set()
    moving: set[str] = set()
    planned_targets: set[str] = set()

    def is_dotfile(name: str) -> bool:
        return name.startswith(".") and "." not in name[1:]

    for item in items:
        cur_ext = os.path.splitext(item.original_path)[1].lstrip(".").lower()
        if not item.selected or cur_ext == ext or is_dotfile(item.file_name):
            reserved.add(os.path.normcase(item.original_path))
        else:
            moving.add(os.path.normcase(item.original_path))

    def exists_or_reserved(path: str) -> bool:
        norm = os.path.normcase(path)
        if norm in reserved or norm in planned_targets:
            return True
        return os.path.exists(path) and norm not in moving

    for item in items:
        cur_ext = os.path.splitext(item.original_path)[1].lstrip(".").lower()
        if not item.selected:
            item.new_file_name = item.file_name
            item.target_path = item.original_path
            item.unchanged = True
            item.conflict = False
            item.status_text = "Снят выбор"
            continue

        if is_dotfile(item.file_name):
            item.new_file_name = item.file_name
            item.target_path = item.original_path
            item.unchanged = True
            item.conflict = True
            item.status_text = "Dot-файл (пропуск)"
            continue

        if cur_ext == ext:
            item.new_file_name = item.file_name
            item.target_path = item.original_path
            item.unchanged = True
            item.conflict = False
            item.status_text = f"Уже {dot_ext}"
            continue

        stem = os.path.splitext(item.file_name)[0]
        candidate = os.path.join(item.directory, stem + dot_ext)

        if not exists_or_reserved(candidate):
            planned_targets.add(os.path.normcase(candidate))
            reserved.add(os.path.normcase(candidate))
            item.target_path = candidate
            item.new_file_name = os.path.basename(candidate)
            item.unchanged = False
            item.conflict = False
            item.status_text = "Готов к смене"
            continue

        if collision_mode == "suffix":
            idx = 1
            while True:
                suffixed = os.path.join(item.directory, f"{stem} ({idx}){dot_ext}")
                if not exists_or_reserved(suffixed):
                    break
                idx += 1
            planned_targets.add(os.path.normcase(suffixed))
            reserved.add(os.path.normcase(suffixed))
            item.target_path = suffixed
            item.new_file_name = os.path.basename(suffixed)
            item.unchanged = False
            item.conflict = False
            item.status_text = f"С суффиксом ({idx})"
        elif collision_mode == "skip":
            item.target_path = item.original_path
            item.new_file_name = item.file_name
            item.unchanged = True
            item.conflict = True
            item.status_text = "Пропуск (имя занято)"
        else:
            cand_norm = os.path.normcase(candidate)
            if cand_norm in planned_targets:
                item.target_path = item.original_path
                item.new_file_name = item.file_name
                item.unchanged = True
                item.conflict = True
                item.status_text = "Конфликт в пакете"
            else:
                planned_targets.add(cand_norm)
                reserved.add(cand_norm)
                item.target_path = candidate
                item.new_file_name = os.path.basename(candidate)
                item.unchanged = False
                item.conflict = True
                item.status_text = "Перезапишет файл"


def execute_rename(items: list[FileRenameItem], collision_mode: str) -> tuple[int, list[str], list[tuple[str, str]]]:
    to_rename = [
        i
        for i in items
        if i.selected
        and not i.unchanged
        and os.path.normcase(i.original_path) != os.path.normcase(i.target_path)
    ]

    originals = {os.path.normcase(i.original_path) for i in to_rename}
    staged: list[tuple[FileRenameItem, str]] = []
    errors: list[str] = []
    undo_entries: list[tuple[str, str]] = []
    renamed = 0

    for item in to_rename:
        if not os.path.exists(item.original_path):
            errors.append(f"{item.file_name}: файл не найден")
            continue

        if os.path.normcase(item.target_path) in originals:
            tmp = os.path.join(item.directory, f".__bec_tmp_{uuid.uuid4().hex}_{item.file_name}")
            try:
                os.replace(item.original_path, tmp)
                staged.append((item, tmp))
            except OSError as exc:
                errors.append(f"{item.file_name}: {exc}")
        else:
            try:
                existed = os.path.exists(item.target_path)
                if existed and collision_mode != "overwrite":
                    errors.append(f"{item.file_name}: целевой файл уже существует")
                    continue
                os.replace(item.original_path, item.target_path)
                renamed += 1
                if not existed:
                    undo_entries.append((item.original_path, item.target_path))
            except OSError as exc:
                errors.append(f"{item.file_name}: {exc}")

    for item, tmp in staged:
        try:
            existed = os.path.exists(item.target_path)
            os.replace(tmp, item.target_path)
            renamed += 1
            if not existed:
                undo_entries.append((item.original_path, item.target_path))
        except OSError as exc:
            try:
                if os.path.exists(tmp) and not os.path.exists(item.original_path):
                    os.replace(tmp, item.original_path)
            except OSError:
                pass
            errors.append(f"{item.file_name}: {exc}")

    return renamed, errors, undo_entries


def undo_rename(entries: list[tuple[str, str]]) -> tuple[int, list[str]]:
    restored = 0
    errors: list[str] = []
    for orig, new in reversed(entries):
        try:
            if not os.path.exists(new):
                errors.append(f"{os.path.basename(new)}: файл не найден")
                continue
            if os.path.exists(orig):
                errors.append(f"{os.path.basename(orig)}: исходное имя уже занято")
                continue
            os.replace(new, orig)
            restored += 1
        except OSError as exc:
            errors.append(f"{os.path.basename(new)}: {exc}")
    return restored, errors


class BulkExtensionApp:
    def __init__(self, initial_paths: list[str]) -> None:
        self.root = tk.Tk()
        self.root.title("Массовая смена расширений")
        self.root.geometry("860x580")
        self.root.minsize(680, 440)
        self.root.configure(bg="#18181B")

        self.source_inputs: list[str] = []
        self.items: list[FileRenameItem] = []
        self.last_undo: list[tuple[str, str]] = []

        self._setup_styles()
        self._build_ui()

        if initial_paths:
            self.add_source_paths(initial_paths)
        else:
            self.update_ui_state()

    def _setup_styles(self) -> None:
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass

        style.configure("Dark.TFrame", background="#18181B")
        style.configure("Card.TFrame", background="#222227")
        style.configure("Dark.TLabel", background="#222227", foreground="#F4F4F6", font=("Segoe UI", 10))
        style.configure("Muted.TLabel", background="#222227", foreground="#A1A1AA", font=("Segoe UI", 9))
        style.configure("Header.TLabel", background="#222227", foreground="#F4F4F6", font=("Segoe UI", 13, "bold"))
        style.configure("Footer.TLabel", background="#18181B", foreground="#F4F4F6", font=("Segoe UI", 10))

        style.configure(
            "Dark.TCombobox",
            fieldbackground="#2C2C32",
            background="#2C2C32",
            foreground="#F4F4F6",
            arrowcolor="#A1A1AA",
            bordercolor="#3A3A42",
        )
        style.map(
            "Dark.TCombobox",
            fieldbackground=[("readonly", "#2C2C32")],
            selectbackground=[("readonly", "#2C2C32")],
            selectforeground=[("readonly", "#F4F4F6")],
        )

        style.configure(
            "Treeview",
            background="#222227",
            fieldbackground="#222227",
            foreground="#F4F4F6",
            rowheight=26,
            borderwidth=0,
        )
        style.configure(
            "Treeview.Heading",
            background="#2A2A30",
            foreground="#A1A1AA",
            relief="flat",
            font=("Segoe UI", 9, "bold"),
        )
        style.map("Treeview", background=[("selected", "#2A3A4D")])

    def _build_ui(self) -> None:
        main = tk.Frame(self.root, bg="#18181B", padx=16, pady=14)
        main.pack(fill="both", expand=True)

        # Header
        hdr = tk.Frame(main, bg="#222227", highlightbackground="#3A3A42", highlightthickness=1, padx=14, pady=10)
        hdr.pack(fill="x", pady=(0, 10))
        tk.Label(hdr, text="Массовая смена расширений", bg="#222227", fg="#F4F4F6", font=("Segoe UI", 13, "bold")).pack(
            side="left"
        )
        tk.Label(
            hdr,
            text=f"ОС: {platform.system()} (локальный режим)",
            bg="#222227",
            fg="#4ADE80",
            font=("Segoe UI", 9),
        ).pack(side="right")

        # Controls card
        ctrl = tk.Frame(main, bg="#222227", highlightbackground="#3A3A42", highlightthickness=1, padx=14, pady=12)
        ctrl.pack(fill="x", pady=(0, 10))

        row1 = tk.Frame(ctrl, bg="#222227")
        row1.pack(fill="x")

        col1 = tk.Frame(row1, bg="#222227")
        col1.pack(side="left", padx=(0, 16))
        tk.Label(col1, text="Новое расширение (выберите из списка):", bg="#222227", fg="#A1A1AA", font=("Segoe UI", 9)).pack(
            anchor="w", pady=(0, 4)
        )
        self.ext_var = tk.StringVar(value=SUPPORTED_EXTENSIONS[0][1])
        self.ext_combo = ttk.Combobox(
            col1,
            textvariable=self.ext_var,
            values=[d for _, d in SUPPORTED_EXTENSIONS],
            state="readonly",
            width=36,
            style="Dark.TCombobox",
        )
        self.ext_combo.pack(anchor="w")
        self.ext_combo.bind("<<ComboboxSelected>>", lambda _e: self._on_ext_changed())

        self.custom_frame = tk.Frame(col1, bg="#222227")
        tk.Label(self.custom_frame, text="Своё (без точки):", bg="#222227", fg="#A1A1AA", font=("Segoe UI", 9)).pack(
            side="left", padx=(0, 6)
        )
        self.custom_var = tk.StringVar()
        self.custom_var.trace_add("write", lambda *_: self.rebuild_preview())
        self.custom_entry = tk.Entry(
            self.custom_frame,
            textvariable=self.custom_var,
            bg="#2C2C32",
            fg="#F4F4F6",
            insertbackground="#F4F4F6",
            relief="flat",
            width=18,
        )
        self.custom_entry.pack(side="left", ipady=4)

        col2 = tk.Frame(row1, bg="#222227")
        col2.pack(side="left")
        tk.Label(col2, text="Если файл с таким именем уже есть:", bg="#222227", fg="#A1A1AA", font=("Segoe UI", 9)).pack(
            anchor="w", pady=(0, 4)
        )
        self.coll_var = tk.StringVar(value=COLLISION_MODES[0][1])
        self.coll_combo = ttk.Combobox(
            col2,
            textvariable=self.coll_var,
            values=[d for _, d in COLLISION_MODES],
            state="readonly",
            width=32,
            style="Dark.TCombobox",
        )
        self.coll_combo.pack(anchor="w")
        self.coll_combo.bind("<<ComboboxSelected>>", lambda _e: self.rebuild_preview())

        row2 = tk.Frame(ctrl, bg="#222227")
        row2.pack(fill="x", pady=(10, 0))
        self.subfolders_var = tk.BooleanVar(value=False)
        tk.Checkbutton(
            row2,
            text="Включая вложенные папки",
            variable=self.subfolders_var,
            command=self.reload_from_sources,
            bg="#222227",
            fg="#F4F4F6",
            selectcolor="#2C2C32",
            activebackground="#222227",
            activeforeground="#F4F4F6",
        ).pack(side="left")
        tk.Label(
            row2,
            text="Меняется только расширение имени файла (без конвертации)",
            bg="#222227",
            fg="#A1A1AA",
            font=("Segoe UI", 9),
        ).pack(side="left", padx=(16, 0))

        # Preview table
        table_card = tk.Frame(main, bg="#222227", highlightbackground="#3A3A42", highlightthickness=1)
        table_card.pack(fill="both", expand=True, pady=(0, 10))

        cols = ("selected", "old", "new", "status")
        self.tree = ttk.Treeview(table_card, columns=cols, show="headings", selectmode="browse")
        self.tree.heading("selected", text="✓")
        self.tree.heading("old", text="Текущее имя")
        self.tree.heading("new", text="Новое имя")
        self.tree.heading("status", text="Статус")
        self.tree.column("selected", width=42, anchor="center", stretch=False)
        self.tree.column("old", width=260, anchor="w")
        self.tree.column("new", width=260, anchor="w")
        self.tree.column("status", width=180, anchor="w")

        vsb = ttk.Scrollbar(table_card, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=vsb.set)
        self.tree.pack(side="left", fill="both", expand=True)
        vsb.pack(side="right", fill="y")
        self.tree.bind("<Double-1>", self._toggle_selected_row)
        self.tree.bind("<space>", self._toggle_selected_row)

        # Footer
        ftr = tk.Frame(main, bg="#18181B")
        ftr.pack(fill="x")

        self.summary_label = tk.Label(ftr, text="Файлов: 0", bg="#18181B", fg="#F4F4F6", font=("Segoe UI", 10))
        self.summary_label.pack(side="left")

        btns = tk.Frame(ftr, bg="#18181B")
        btns.pack(side="right")

        self.undo_btn = self._mk_btn(btns, "Отменить", self.on_undo, primary=False)
        self.undo_btn.configure(state="disabled")
        self.undo_btn.pack(side="left", padx=(0, 6))

        self._mk_btn(btns, "Файлы...", self.on_add_files, primary=False).pack(side="left", padx=(0, 6))
        self._mk_btn(btns, "Папка...", self.on_add_folder, primary=False).pack(side="left", padx=(0, 6))
        self._mk_btn(btns, "Очистить", self.on_clear, primary=False).pack(side="left", padx=(0, 6))
        self.apply_btn = self._mk_btn(btns, "Применить", self.on_apply, primary=True)
        self.apply_btn.pack(side="left")

    def _mk_btn(self, parent: tk.Widget, text: str, cmd, primary: bool) -> tk.Button:
        return tk.Button(
            parent,
            text=text,
            command=cmd,
            bg="#0078D4" if primary else "#2C2C32",
            fg="#FFFFFF",
            activebackground="#188AE2" if primary else "#34343B",
            activeforeground="#FFFFFF",
            relief="flat",
            padx=12,
            pady=6,
            cursor="hand2",
            font=("Segoe UI", 9, "bold" if primary else "normal"),
        )

    def _selected_ext(self) -> str:
        idx = self.ext_combo.current()
        if idx < 0:
            return "png"
        key = SUPPORTED_EXTENSIONS[idx][0]
        if key == "__custom__":
            return sanitize_extension(self.custom_var.get())
        return key

    def _selected_collision(self) -> str:
        idx = self.coll_combo.current()
        return COLLISION_MODES[idx][0] if idx >= 0 else "suffix"

    def _on_ext_changed(self) -> None:
        idx = self.ext_combo.current()
        if idx >= 0 and SUPPORTED_EXTENSIONS[idx][0] == "__custom__":
            self.custom_frame.pack(anchor="w", pady=(8, 0))
            self.custom_entry.focus_set()
        else:
            self.custom_frame.pack_forget()
        self.rebuild_preview()

    def _toggle_selected_row(self, _event=None) -> None:
        sel = self.tree.selection()
        if not sel:
            return
        idx = int(sel[0])
        if 0 <= idx < len(self.items):
            self.items[idx].selected = not self.items[idx].selected
            self.rebuild_preview()

    def add_source_paths(self, paths: Iterable[str]) -> None:
        existing = {os.path.normcase(p) for p in self.source_inputs}
        for p in paths:
            if p and os.path.normcase(p) not in existing:
                self.source_inputs.append(p)
                existing.add(os.path.normcase(p))
        self.reload_from_sources()

    def reload_from_sources(self) -> None:
        self.items = collect_files(self.source_inputs, self.subfolders_var.get())
        self.rebuild_preview()

    def rebuild_preview(self) -> None:
        if self.items:
            build_preview(self.items, self._selected_ext(), self._selected_collision())

        self.tree.delete(*self.tree.get_children())
        for idx, item in enumerate(self.items):
            mark = "☑" if item.selected else "☐"
            self.tree.insert("", "end", iid=str(idx), values=(mark, item.file_name, item.new_file_name, item.status_text))

        self.update_ui_state()

    def update_ui_state(self) -> None:
        ext = self._selected_ext()
        ready = sum(1 for i in self.items if i.selected and not i.unchanged) if ext else 0

        if not self.items:
            self.summary_label.configure(text="Файлов: 0 (добавьте файлы или папку)")
        elif not ext:
            self.summary_label.configure(text=f"Всего файлов: {len(self.items)} • Введите расширение")
        else:
            self.summary_label.configure(text=f"Всего файлов: {len(self.items)} • К смене на .{ext}: {ready}")

        self.apply_btn.configure(
            state="normal" if ready > 0 else "disabled",
            text=f"Применить ({ready})" if ready > 0 else "Применить",
        )
        self.undo_btn.configure(state="normal" if self.last_undo else "disabled")

    def on_add_files(self) -> None:
        chosen = filedialog.askopenfilenames(title="Выберите файлы")
        if chosen:
            self.add_source_paths(chosen)

    def on_add_folder(self) -> None:
        chosen = filedialog.askdirectory(title="Выберите папку")
        if chosen:
            self.add_source_paths([chosen])

    def on_clear(self) -> None:
        self.source_inputs.clear()
        self.items.clear()
        self.rebuild_preview()

    def on_apply(self) -> None:
        renamed, errors, undo_entries = execute_rename(self.items, self._selected_collision())
        self.last_undo = list(undo_entries)

        rename_map = {os.path.normcase(old): new for old, new in undo_entries}
        self.source_inputs = [rename_map.get(os.path.normcase(p), p) for p in self.source_inputs]
        self.reload_from_sources()

        if errors:
            messagebox.showwarning("Завершено с ошибками", f"Переименовано: {renamed}\n\n" + "\n".join(errors[:10]))
        else:
            messagebox.showinfo("Готово", f"Расширение изменено у файлов: {renamed}")

    def on_undo(self) -> None:
        if not self.last_undo:
            return
        restored, errors = undo_rename(self.last_undo)
        restore_map = {os.path.normcase(new): old for old, new in self.last_undo}
        self.source_inputs = [restore_map.get(os.path.normcase(p), p) for p in self.source_inputs]
        self.last_undo.clear()
        self.reload_from_sources()
        if errors:
            messagebox.showwarning("Частичный откат", "\n".join(errors[:10]))
        else:
            messagebox.showinfo("Откат выполнен", f"Восстановлено файлов: {restored}")

    def run(self) -> None:
        self.root.mainloop()


def gather_initial_paths() -> list[str]:
    paths = [a for a in sys.argv[1:] if a and not a.startswith("-")]
    nautilus_env = os.environ.get("NAUTILUS_SCRIPT_SELECTED_FILE_PATHS", "")
    if nautilus_env:
        paths.extend(line for line in nautilus_env.splitlines() if line.strip())
    return paths


if __name__ == "__main__":
    app = BulkExtensionApp(gather_initial_paths())
    app.run()
