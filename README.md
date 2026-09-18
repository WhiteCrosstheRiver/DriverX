# DriverX

## 原生 Windows 版本（推荐）

新版界面位于 `native/DriverX.Desktop`，使用 C#、WPF 和 .NET 8。它提供 Fluent 风格导航、连接卡片、协议中心、设置页与浅色/深色主题，并使用隐藏窗口启动 rclone。

```powershell
.\build_native.ps1
```

构建后双击 `start_driverx_native.vbs`，或直接运行 `dist\DriverX\DriverX.exe`。程序优先读取 `%APPDATA%\DriverX\profiles.json`，首次运行会载入项目内的 RaiDrive 导入配置。

轻量 Windows 远程目录映射工具。DriverX 只负责连接配置和挂载管理，文件系统由成熟的 `rclone + WinFsp` 提供。

添加连接支持：SFTP、WebDAV、FTP、SMB、HTTP/HTTPS、S3、Google Drive、OneDrive、Dropbox。协议通过 rclone 后端实现，首版界面已保留统一的盘符、路径和凭据模型。

## 依赖

1. 安装 [WinFsp](https://github.com/winfsp/winfsp/releases)。
2. 下载 [rclone Windows 版](https://rclone.org/downloads/)，将 `rclone.exe` 放入 PATH，或放到 DriverX 目录。
3. Python 3.10+（首版使用系统 Tkinter，无第三方 Python 依赖）。

## 运行

```powershell
python driverx.py
```

日常使用可双击 `start_driverx.vbs`，它现在默认启动原生版本；DriverX 与全部 rclone 子进程都使用 Windows 隐藏窗口标志，不会显示命令行窗口。

首次添加连接后点击“挂载 / 卸载”。配置保存在 `%APPDATA%\DriverX\profiles.json`。生产版本需要把密码改为 Windows Credential Manager，并将 rclone/WinFsp 作为安装包依赖；当前代码是可运行的 MVP 骨架，便于先验证 SFTP 挂载链路。

## 当前边界

- 首版只开放 SFTP。
- 需要用户预先安装 rclone 与 WinFsp。
- 使用 rclone 的 `minimal` 缓存模式，避免默认占用大量磁盘。
- 删除连接前必须先卸载。

## RaiDrive 迁移测试

`import/raidrive_connections.json` 已从 RaiDrive 日志迁移 8 个 SFTP 连接。当前机器的 SSH 私钥已对 `ime001`、`gpu01`、`gpu02` 验证成功；三条连接都能通过 rclone 列出远程目录，并已完成一次临时 `R:` 盘符挂载/卸载测试。其他连接保留了地址和路径，但需要补充各自认证信息。
