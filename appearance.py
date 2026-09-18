"""DriverX's lightweight, dependency-free desktop appearance."""
import json
import tkinter as tk
from tkinter import ttk

THEMES = {
    'Apple': dict(bg='#f5f5f7', side='#ececf0', panel='#ffffff', text='#202124', muted='#62646c', border='#dcdce2', accent='#0066cc', soft='#dfeaff', on='#ffffff', radius=18),
    'Windows 11': dict(bg='#eef3fa', side='#e3ebf6', panel='#ffffff', text='#182638', muted='#52647a', border='#cdd9e7', accent='#005fb8', soft='#d5e7fa', on='#ffffff', radius=8),
    'GitHub 默认': dict(bg='#0d1117', side='#010409', panel='#161b22', text='#e6edf3', muted='#9da7b3', border='#30363d', accent='#238636', soft='#172f22', on='#ffffff', radius=6),
    'GitHub 高对比度': dict(bg='#010409', side='#010409', panel='#0d1117', text='#ffffff', muted='#d4e0ed', border='#8999aa', accent='#71b7ff', soft='#163451', on='#010409', radius=3),
}


class Button(tk.Canvas):
    def __init__(self, parent, text, command, colors, primary=False, width=120):
        super().__init__(parent, width=width, height=40, bg=parent.cget('bg'), highlightthickness=0, takefocus=1, cursor='hand2')
        self.label, self.command, self.c, self.primary = text, command, colors, primary
        self.hover = False
        self.bind('<Configure>', self.paint)
        self.bind('<Enter>', lambda e: self.set_hover(True))
        self.bind('<Leave>', lambda e: self.set_hover(False))
        self.bind('<FocusIn>', self.paint)
        self.bind('<FocusOut>', self.paint)
        self.bind('<ButtonRelease-1>', lambda e: command())
        self.bind('<Return>', lambda e: command())
        self.bind('<space>', lambda e: command())

    def set_hover(self, value):
        self.hover = value
        self.paint()

    def paint(self, event=None):
        self.delete('all')
        w, h, r = max(self.winfo_width(), 20), 40, min(self.c['radius'], 12)
        fill = self.c['accent'] if self.primary else self.c['soft'] if self.hover else self.c['panel']
        outline = self.c['accent'] if self.focus_get() == self else self.c['border']
        self.create_polygon(2+r,2,w-2-r,2,w-2,2,w-2,2+r,w-2,h-2-r,w-2,h-2,w-2-r,h-2,2+r,h-2,2,h-2,2,h-2-r,2,2+r,2,2, smooth=True, fill=fill, outline=outline, width=2)
        self.create_text(w/2, h/2, text=self.label, fill=self.c['on'] if self.primary else self.c['text'], font=('Microsoft YaHei UI', 10, 'bold' if self.primary else 'normal'))


class Appearance:
    def label(self, parent, text, size=10, muted=False, bold=False):
        return tk.Label(parent, text=text, bg=parent.cget('bg'), fg=self.colors['muted' if muted else 'text'], font=('Microsoft YaHei UI', size, 'bold' if bold else 'normal'), anchor='w')

    def apply_theme(self, name):
        self.colors = THEMES.get(name, THEMES['Apple'])
        c = self.colors
        self.configure(bg=c['bg'])
        self.style.theme_use('clam')
        self.style.configure('TFrame', background=c['bg'])
        self.style.configure('TLabel', background=c['bg'], foreground=c['text'], font=('Microsoft YaHei UI', 10))
        self.style.configure('TEntry', fieldbackground=c['panel'], foreground=c['text'], insertcolor=c['text'], padding=8)
        self.style.configure('TButton', background=c['panel'], foreground=c['text'], padding=9)
        self.style.map('TButton', background=[('active', c['soft'])], foreground=[('active', c['text'])])

    def build_ui(self):
        c = self.colors
        self.selected_index = None
        self.shell = tk.Frame(self, bg=c['bg'])
        self.shell.pack(fill='both', expand=True)
        side = tk.Frame(self.shell, bg=c['side'], width=216)
        side.pack(side='left', fill='y')
        side.pack_propagate(False)
        self.label(side, 'D / X', 25, bold=True).pack(anchor='w', padx=24, pady=(30, 0))
        self.label(side, 'DriverX', 15, bold=True).pack(anchor='w', padx=24, pady=(4, 0))
        self.label(side, '远程文件，近在眼前', 9, muted=True).pack(anchor='w', padx=24, pady=(6, 35))
        nav = tk.Frame(side, bg=c['soft'])
        nav.pack(fill='x', padx=14)
        self.label(nav, '▣   我的磁盘', 11, bold=True).pack(padx=14, pady=12)
        self.label(side, '远程协议 / PROTOCOLS', 9, muted=True).pack(anchor='w', padx=24, pady=(36, 12))
        for label in ('SFTP', 'WebDAV', 'FTP / SMB', '云存储'):
            self.label(side, '◇  ' + label, 10, muted=True).pack(anchor='w', padx=30, pady=7)
        self.label(side, 'SFTP  /  DriverX', 9, muted=True).pack(side='bottom', anchor='w', padx=24, pady=24)
        main = tk.Frame(self.shell, bg=c['bg'])
        main.pack(side='left', fill='both', expand=True, padx=30, pady=28)
        header = tk.Frame(main, bg=c['bg'])
        header.pack(fill='x')
        Button(header, '⚙ 设置', self.open_settings, c, width=88).pack(side='right', padx=(8, 0), pady=8)
        Button(header, '＋ 添加连接', self.add_profile, c, True, 136).pack(side='right', pady=8)
        self.label(header, '我的磁盘', 25, bold=True).pack(anchor='w')
        self.label(header, '将服务器目录映射为熟悉的本地盘符。', 10, muted=True).pack(anchor='w', pady=(7, 24))
        stats = tk.Frame(main, bg=c['bg'])
        stats.pack(fill='x', pady=(0, 24))
        self.summary = self.label(stats, '', 10, muted=True)
        self.summary.pack(side='left')
        self.label(stats, '协议  SFTP', 9, muted=True).pack(side='right')
        self.canvas = tk.Canvas(main, bg=c['bg'], highlightthickness=0)
        self.canvas.pack(fill='both', expand=True)
        self.cards = tk.Frame(self.canvas, bg=c['bg'])
        self.card_window = self.canvas.create_window(0, 0, window=self.cards, anchor='nw')
        self.canvas.bind('<Configure>', lambda e: self.canvas.itemconfigure(self.card_window, width=e.width))
        self.cards.bind('<Configure>', lambda e: self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.bind('<MouseWheel>', lambda e: self.canvas.yview_scroll(int(-e.delta/120), 'units'))
        footer = tk.Frame(main, bg=c['bg'])
        footer.pack(fill='x', pady=(20, 0))
        self.info = self.label(footer, '', 9, muted=True)
        self.info.pack(side='left')
        self.label(footer, '按需访问 · 轻量运行', 9, muted=True).pack(side='right')

    def open_settings(self):
        self.shell.destroy()
        self.build_settings()

    def build_settings(self):
        c = self.colors
        self.shell = tk.Frame(self, bg=c['bg'])
        self.shell.pack(fill='both', expand=True)
        side = tk.Frame(self.shell, bg=c['side'], width=216)
        side.pack(side='left', fill='y'); side.pack_propagate(False)
        self.label(side, 'D / X', 25, bold=True).pack(anchor='w', padx=24, pady=(30, 0))
        self.label(side, 'DriverX', 15, bold=True).pack(anchor='w', padx=24, pady=(4, 0))
        Button(side, '←  返回我的磁盘', self.back_home, c, width=184).pack(padx=16, pady=(40, 8))
        self.label(side, '设置 / SETTINGS', 9, muted=True).pack(anchor='w', padx=24, pady=(30, 10))
        self.label(side, '◇  外观风格', 10, bold=True).pack(anchor='w', padx=30, pady=8)
        self.label(side, '◇  连接与缓存', 10, muted=True).pack(anchor='w', padx=30, pady=8)
        self.label(side, '◇  关于 DriverX', 10, muted=True).pack(anchor='w', padx=30, pady=8)
        self.label(side, 'SFTP  /  DriverX', 9, muted=True).pack(side='bottom', anchor='w', padx=24, pady=24)
        main = tk.Frame(self.shell, bg=c['bg'])
        main.pack(side='left', fill='both', expand=True, padx=54, pady=42)
        top = tk.Frame(main, bg=c['bg']); top.pack(fill='x')
        Button(top, '← 返回', self.back_home, c, width=82).pack(side='right')
        self.label(top, '设置', 25, bold=True).pack(anchor='w')
        self.label(main, '外观风格', 16, bold=True).pack(anchor='w', pady=(48, 6))
        self.label(main, '选择 DriverX 的整体视觉风格，修改会立即生效并自动保存。', 10, muted=True).pack(anchor='w', pady=(0, 24))
        grid = tk.Frame(main, bg=c['bg']); grid.pack(anchor='w')
        for index, name in enumerate(THEMES):
            card = tk.Frame(grid, bg=c['panel'], highlightbackground=c['accent'] if name == self.theme else c['border'], highlightthickness=2, padx=20, pady=18)
            card.grid(row=index // 2, column=index % 2, padx=(0, 18), pady=(0, 18), sticky='nsew')
            swatch = tk.Canvas(card, width=220, height=42, bg=c['panel'], highlightthickness=0); swatch.pack()
            palette = THEMES[name]
            swatch.create_rectangle(2, 2, 218, 40, fill=palette['bg'], outline=palette['border'], width=2)
            swatch.create_rectangle(12, 11, 70, 31, fill=palette['accent'], outline='')
            swatch.create_rectangle(78, 11, 208, 31, fill=palette['panel'], outline=palette['border'])
            self.label(card, ('●  ' if name == self.theme else '○  ') + name, 11, bold=True).pack(anchor='w', pady=(12, 5))
            self.label(card, {'Apple':'清爽、柔和的浅色界面','Windows 11':'明亮、现代的系统风格','GitHub 默认':'适合长时间工作的深色界面','GitHub 高对比度':'高可读性和强对比度'}[name], 9, muted=True).pack(anchor='w')
            Button(card, '使用此风格' if name != self.theme else '当前使用', lambda n=name:self.change_theme(n), c, name == self.theme, 120).pack(anchor='w', pady=(14, 0))
        self.label(main, 'DriverX  ·  轻量远程磁盘', 9, muted=True).pack(anchor='w', pady=(54, 0))

    def back_home(self):
        self.shell.destroy()
        self.build_ui()
        if hasattr(self, 'cards'):
            self.refresh()

    def change_theme(self, name):
        self.theme = name
        self.apply_theme(name)
        self.shell.destroy()
        self.build_settings()
        if hasattr(self, 'cards'):
            self.refresh()
        self.settings_path.parent.mkdir(parents=True, exist_ok=True)
        self.settings_path.write_text(json.dumps({'theme': name}, ensure_ascii=False), encoding='utf-8')

    def selected(self):
        return self.selected_index

    def card_action(self, index, action):
        self.selected_index = index
        action()

    def draw_cards(self):
        c = self.colors
        for child in self.cards.winfo_children():
            child.destroy()
        self.summary.config(text=f'{len(self.profiles):02d} 个连接     /     {len(self.mounts):02d} 个挂载进程')
        if not self.profiles:
            empty = tk.Frame(self.cards, bg=c['panel'], highlightbackground=c['border'], highlightthickness=1)
            empty.pack(fill='x', pady=5)
            art = tk.Canvas(empty, height=112, bg=c['panel'], highlightthickness=0)
            art.pack(fill='x', pady=(30, 0))
            def draw(e):
                art.delete('all')
                x=e.width/2
                art.create_oval(x-43,10,x+43,96, fill=c['soft'], outline='')
                art.create_rectangle(x-24,35,x+24,68, fill=c['panel'], outline=c['accent'], width=2)
                art.create_line(x-24,58,x+24,58,fill=c['accent'],width=2)
                art.create_oval(x+12,62,x+15,65,fill=c['accent'],outline='')
            art.bind('<Configure>', draw)
            self.label(empty, '你的第一个远程磁盘', 17, bold=True).pack(pady=(7, 10))
            self.label(empty, '添加 SFTP 连接，即可从资源管理器访问远程文件。', 10, muted=True).pack(pady=(0, 24))
            Button(empty, '＋ 添加连接', self.add_profile, c, True, 150).pack(pady=(0, 42))
        for i, p in enumerate(self.profiles):
            card = tk.Frame(self.cards, bg=c['panel'], highlightbackground=c['border'], highlightthickness=1, padx=18, pady=18)
            card.pack(fill='x', pady=(0, 12))
            badge = tk.Label(card, text=p['drive']+':', bg=c['soft'], fg=c['text'], font=('Segoe UI', 19, 'bold'), width=3, pady=12)
            badge.pack(side='left', padx=(0,18))
            actions = tk.Frame(card, bg=c['panel'])
            actions.pack(side='right')
            Button(actions, '卸载' if i in self.mounts else '挂载', lambda n=i:self.card_action(n,self.toggle_selected), c, True, 76).pack(side='left', padx=3)
            Button(actions, '编辑', lambda n=i:self.card_action(n,self.edit_selected), c, width=66).pack(side='left', padx=3)
            Button(actions, '删除', lambda n=i:self.card_action(n,self.delete_selected), c, width=66).pack(side='left', padx=3)
            text = tk.Frame(card, bg=c['panel'])
            text.pack(side='left', fill='x', expand=True)
            self.label(text, p['name'], 13, bold=True).pack(anchor='w')
            self.label(text, f"{p['user']}@{p['host']}", 9, muted=True).pack(anchor='w', pady=(6,2))
            self.label(text, p.get('path','/'), 9, muted=True).pack(anchor='w')
