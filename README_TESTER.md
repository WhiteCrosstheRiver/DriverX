# DriverX 测试版

1. 安装 [WinFsp](https://winfsp.dev/rel/)，安装完成后重启 Windows。
2. 解压整个压缩包，双击 `start_driverx.vbs` 或 `DriverX.exe`。
3. 点击“添加连接”，选择协议并填写自己的服务器信息。测试包不包含作者的连接配置、密码或 SSH 私钥。
4. 首版重点测试 SFTP 挂载、打开、卸载、编辑、删除和浅色/深色主题。

`rclone.exe` 已随包提供；映射盘由 WinFsp 提供。程序和 rclone 都会静默运行，不弹出命令行窗口。

这是预览版，请使用测试服务器或测试目录，不要把唯一的生产数据作为首次测试对象。
