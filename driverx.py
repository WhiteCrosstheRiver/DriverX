import json
import os
import shutil
import subprocess
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from appearance import Appearance, THEMES


APP_DIR = Path(os.environ.get("APPDATA", Path.home())) / "DriverX"
CONFIG = APP_DIR / "profiles.json"


def hidden_process_options():
    """Force console programs to start without allocating or showing a window."""
    options = {"creationflags": getattr(subprocess, "CREATE_NO_WINDOW", 0)}
    if os.name == "nt":
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        options["startupinfo"] = startup
    return options


class DriverX(Appearance, tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("DriverX")
        self.geometry("1120x720")
        self.minsize(1000, 650)
        self.profiles = self.load_profiles()
        self.mounts = {}
        self.settings_path = APP_DIR / 'appearance.json'
        try:
            self.theme = json.loads(self.settings_path.read_text(encoding='utf-8')).get('theme', 'Apple')
        except (OSError, ValueError):
            self.theme = 'Apple'
        if self.theme not in THEMES:
            self.theme = 'Apple'
        self.style = ttk.Style(self)
        self.apply_theme(self.theme)
        self.build_ui()
        self.refresh()

    def load_profiles(self):
        try:
            if CONFIG.exists():
                saved = json.loads(CONFIG.read_text(encoding="utf-8"))
                if saved:
                    return saved
            imported = Path(__file__).with_name('import') / 'raidrive_connections.json'
            if imported.exists():
                return json.loads(imported.read_text(encoding='utf-8'))
            return []
        except (OSError, json.JSONDecodeError):
            return []

    def save_profiles(self):
        APP_DIR.mkdir(parents=True, exist_ok=True)
        CONFIG.write_text(json.dumps(self.profiles, ensure_ascii=False, indent=2), encoding="utf-8")


    def refresh(self):
        self.draw_cards()
        rclone = self.rclone_path()
        winfsp = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "WinFsp"
        if not winfsp.exists():
            winfsp = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "WinFsp"
        if rclone and winfsp.exists():
            self.info.configure(text="rclone + WinFsp 已就绪 · 可测试挂载")
        elif not rclone:
            self.info.configure(text="缺少 rclone（重启 DriverX 后刷新 PATH）")
        else:
            self.info.configure(text="缺少 WinFsp（见 README）")

    def rclone_path(self):
        found = shutil.which("rclone")
        if found:
            return found
        candidate = Path(os.environ.get('LOCALAPPDATA', '')) / 'Microsoft/WinGet/Links/rclone.exe'
        return str(candidate) if candidate.exists() else None

    def add_profile(self):
        ProfileDialog(self, None, self.on_profile).show()

    def edit_selected(self):
        i = self.selected()
        if i is not None:
            ProfileDialog(self, self.profiles[i], lambda p: self.on_profile(p, i)).show()

    def on_profile(self, profile, index=None):
        if index is None:
            self.profiles.append(profile)
        else:
            self.profiles[index] = profile
        self.save_profiles()
        self.refresh()

    def delete_selected(self):
        i = self.selected()
        if i is None:
            return
        if i in self.mounts:
            messagebox.showwarning("无法删除", "请先卸载此连接。")
            return
        if messagebox.askyesno("确认删除", f"删除连接“{self.profiles[i]['name']}”？"):
            self.profiles.pop(i)
            self.save_profiles()
            self.refresh()

    def toggle_selected(self):
        i = self.selected()
        if i is None:
            return
        if i in self.mounts:
            subprocess.run([self.rclone_path(), "unmount", self.profiles[i]["drive"] + ":"], capture_output=True, **hidden_process_options())
            self.mounts.pop(i, None)
            self.refresh()
            return
        if not self.rclone_path():
            messagebox.showerror("缺少依赖", "请先安装 rclone 和 WinFsp，详见 README.md。")
            return
        p = self.profiles[i]
        threading.Thread(target=self.mount, args=(i, p), daemon=True).start()

    def mount(self, i, p):
        # rclone's SFTP remote is created transiently from the profile, keeping the GUI self-contained.
        remote = f"driverx-{i}"
        binary = self.rclone_path()
        if not p.get('password') and not p.get('keyfile'):
            self.after(0, lambda: messagebox.showwarning('需要认证', f'连接“{p["name"]}”缺少密码或 SSH 私钥。'))
            return
        protocol = p.get('protocol', 'sftp').lower()
        args = [binary, "config", "create", remote, protocol]
        if protocol in ('sftp', 'ftp'):
            args += ["host", p.get("host", ""), "user", p.get("user", ""), "port", str(p.get("port", 22 if protocol == 'sftp' else 21)), "pass", p.get("password", "")]
            if protocol == 'sftp' and p.get("keyfile"):
                args += ["key_file", os.path.expandvars(p["keyfile"])]
        elif protocol == 'webdav':
            args += ["url", p.get("url", p.get("host", "")), "vendor", p.get('vendor', 'other'), "user", p.get('user', ''), "pass", p.get('password', '')]
        elif protocol == 'smb':
            args += ["host", p.get("host", ""), "user", p.get('user', ''), "pass", p.get('password', '')]
        elif protocol in ('http', 's3', 'drive', 'dropbox', 'onedrive'):
            if p.get('url'): args += ["url", p['url']]
            if p.get('user'): args += ["user", p['user']]
            if p.get('password'): args += ["pass", p['password']]
        subprocess.run(args, capture_output=True, text=True, **hidden_process_options())
        mount_args = [binary, "mount", f"{remote}:{p.get('path', '/') or '/'}", p["drive"] + ":", "--network-mode", "--dir-cache-time", "2s", "--attr-timeout", "1s", "--poll-interval", "0", "--vfs-cache-mode", "minimal", "--buffer-size", "4M", "--transfers", "2", "--checkers", "2", "--vfs-read-chunk-size", "8M", "--vfs-read-chunk-size-limit", "64M"]
        proc = subprocess.Popen(mount_args, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **hidden_process_options())
        self.mounts[i] = proc
        self.after(0, self.refresh)


class ProfileDialog:
    def __init__(self, parent, profile, callback):
        self.parent, self.profile, self.callback = parent, profile or {}, callback

    def show(self):
        self.win = tk.Toplevel(self.parent)
        self.win.title("连接设置")
        self.win.configure(bg=self.parent.colors['bg'], padx=18, pady=16)
        self.win.transient(self.parent)
        self.win.resizable(False, False)
        fields = [("协议", "protocol"), ("名称", "name"), ("主机", "host"), ("端口", "port"), ("用户名", "user"), ("密码", "password"), ("远程目录", "path"), ("盘符（如 X）", "drive"), ("SSH 私钥（可选）", "keyfile")]
        self.vars = {}
        for row, (label, key) in enumerate(fields):
            ttk.Label(self.win, text=label).grid(row=row, column=0, padx=10, pady=9, sticky="e")
            var = tk.StringVar(value=str(self.profile.get(key, 'sftp' if key == 'protocol' else 22 if key == "port" else "/" if key == "path" else "")))
            self.vars[key] = var
            if key == 'protocol':
                entry = ttk.Combobox(self.win, textvariable=var, width=39, state='readonly', values=['sftp', 'webdav', 'ftp', 'smb', 'http', 's3', 'drive', 'onedrive', 'dropbox'])
            else:
                entry = ttk.Entry(self.win, textvariable=var, width=42, show="*" if key == "password" else "")
            entry.grid(row=row, column=1, padx=10, pady=6)
            if key == "keyfile":
                ttk.Button(self.win, text="选择", command=lambda: self.pick_key()).grid(row=row, column=2, padx=5)
        ttk.Button(self.win, text="保存", command=self.save).grid(row=len(fields), column=1, pady=12, sticky="e")

    def pick_key(self):
        path = filedialog.askopenfilename(title="选择 SSH 私钥")
        if path:
            self.vars["keyfile"].set(path)

    def save(self):
        p = {k: v.get().strip() for k, v in self.vars.items()}
        p['protocol'] = p.get('protocol', 'sftp').lower() or 'sftp'
        if not p["name"] or not p["drive"] or (p['protocol'] in ('sftp', 'ftp', 'smb') and (not p["host"] or not p["user"])):
            messagebox.showwarning("信息不完整", "名称、盘符以及该协议要求的主机和用户名不能为空。", parent=self.win)
            return
        p["port"] = int(p["port"] or 22)
        p["drive"] = p["drive"].upper().rstrip(":")
        self.callback(p)
        self.win.destroy()


if __name__ == "__main__":
    DriverX().mainloop()
