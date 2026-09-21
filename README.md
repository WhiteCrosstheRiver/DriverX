# DriverX

## 下载 v0.1.1

[发布说明及下载](https://github.com/WhiteCrosstheRiver/DriverX/releases/tag/v0.1.1)

- Portable.zip：解压后运行 DriverX.exe，内置 .NET 与 rclone；挂载需要已安装 WinFsp。
- Online.zip：内含 EXE 与联网安装脚本，下载并安装 WinFsp。
- Offline.zip：内含 EXE、安装脚本及 WinFsp MSI，无需联网下载依赖。

安装包目前为 ZIP + PowerShell 脚本，解压后在管理员 PowerShell 中运行对应 Install-DriverX 脚本。尚不是图形化 Setup.exe。程序未进行代码签名，此版本供测试使用。

新版包含卡片/列表视图、五档大小、拖动排序、挂载优先、适配浅深主题的 Logo，以及简化的操作菜单。X 隐藏到托盘，电源键或托盘的完全退出负责退出并清理 DriverX 挂载。日/英/法翻译仍有部分动态提示待完善。

DriverX 是一个轻量的 Windows 远程磁盘管理器，把 SFTP、WebDAV、FTP、SMB、S3 和云存储连接映射成普通盘符。界面使用 WPF/Fluent 风格，挂载由 rclone + WinFsp 完成，rclone 进程在后台静默运行。

## 三种发布方式

| 发布物 | 适合场景 | 依赖处理 |
| --- | --- | --- |
| `DriverX-Portable\\DriverX.exe` | 已安装 WinFsp 的电脑，解压即用 | rclone 和 .NET 已内置 |
| `DriverX-Online\\Install-DriverX-Online.ps1` | 普通用户首次安装 | 在线下载并安装 WinFsp，显示进度 |
| `DriverX-Offline\\Install-DriverX-Offline.ps1` | 无网络环境 | 包含 WinFsp MSI，完全离线安装 |

便携版已经包含 rclone 和 .NET 运行时。首次挂载仍需要 WinFsp，因为它是 Windows 文件系统驱动，不能只靠复制一个 EXE 替代。安装脚本会自动处理这一步。

## 构建发布包

```powershell
.\\packaging\\build_release.ps1 -Version 0.1.0
```

脚本会生成 `release\\DriverX-0.1.0` 下的三个目录。离线包构建时会从 WinFsp 官方发布地址下载签名 MSI；如果只需要便携版，也可以运行 `build_native.ps1`。

## 使用

安装或解压后启动 DriverX，在“添加连接”中选择协议、名称、服务器、凭据和空闲盘符。密码默认以圆点隐藏，可以点击眼睛查看。连接编辑页支持默认 Windows 图标及多种盘符图标。退出或从托盘选择“完全退出”时，DriverX 会先停止自己启动的 rclone 进程，再清理 WinFsp、Windows 网络映射和 Explorer 残留记录。

配置保存于 `%APPDATA%\\DriverX\\profiles.json`。密码目前保存在本地配置文件中，正式面向公众发布前应迁移到 Windows Credential Manager。

## 开发依赖

- Windows 10/11 x64
- .NET 8 SDK
- WinFsp（运行挂载功能时需要）
- rclone 已作为资源嵌入原生桌面版本

## 项目结构

- `native/DriverX.Desktop`：原生 WPF 客户端
- `packaging`：联网、离线安装脚本和发布构建脚本
- `import`：本地 RaiDrive 连接导入数据（凭据文件已被 `.gitignore` 排除）
- `test_*.ps1`：带宽、挂载负载和 SFTP 测试脚本

## 当前边界

DriverX 优先保证 SFTP 挂载链路和低占用运行。rclone 的目录缓存、属性缓存、读块大小、并发数等参数可以在设置页调整；默认值偏向较低延迟与较小本机负载。删除连接前必须先卸载，程序不会占用已被 Windows 或其他 DriverX 连接使用的盘符。
