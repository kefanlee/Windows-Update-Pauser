# Windows Update Pauser

[![License](https://img.shields.io/github/license/kefanlee/Windows-Update-Pauser)](LICENSE)
[![Release](https://img.shields.io/github/v/release/kefanlee/Windows-Update-Pauser)](https://github.com/kefanlee/Windows-Update-Pauser/releases/latest)
[![.NET Framework](https://img.shields.io/badge/.NET-4.x-blue)](#构建)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-lightgrey)]()

一个轻量级 Windows 桌面工具，通过修改注册表一键暂停 / 恢复 Windows Update，无需安装，即开即用。

## 截图

<!-- 上传截图后取消下面的注释
![主界面](screenshots/main.png)
-->

## 功能

- **暂停更新** — 同时暂停功能更新（大版本）和质量更新（月度补丁）
- **预设天数** — 7 天 / 14 天 / 30 天 / 1 年一键选择，支持自定义天数
- **一键恢复** — 清除注册表键值，恢复自动更新
- **状态展示** — 实时显示当前暂停状态和截止日期
- **权限检测** — 自动识别管理员权限，非管理员模式可一键提权
- **操作日志** — 所有操作记录到 `logs/` 目录，按日期归档

## 原理

工具通过写入 Windows Update 注册表键值来控制更新暂停：

```
HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings
```

| 注册表键 | 用途 |
|----------|------|
| `PauseUpdatesStartTime` | 暂停开始时间 |
| `PauseUpdatesExpiryTime` | 暂停到期时间 |
| `PauseFeatureUpdatesStartTime` | 功能更新暂停开始 |
| `PauseFeatureUpdatesEndTime` | 功能更新暂停截止 |
| `PauseQualityUpdatesStartTime` | 质量更新暂停开始 |
| `PauseQualityUpdatesEndTime` | 质量更新暂停截止 |

写入后调用 `UsoClient.exe RefreshSettings` 通知系统刷新设置，立即生效。

## 构建

**依赖：** .NET Framework 4.x（系统自带）

```bat
build.bat
```

输出文件：`dist\Windows Update Pauser.exe`

## 使用

1. **以管理员身份运行** `Windows Update Pauser.exe`
2. 选择预设天数或输入自定义天数，右侧会实时预览截止日期
3. 点击 **"暂停更新"**，确认后即可生效
4. 需要恢复时点击 **"恢复更新"**

> 非管理员模式下运行，"暂停更新"按钮会被禁用。此时可以点击 **"以管理员运行"** 一键提权。

## 文件结构

```
├── WindowsUpdatePauser.cs  # 主程序源码
├── app.manifest          # 应用程序清单
├── build.bat             # 编译脚本
├── icon.ico              # 程序图标
├── .gitignore
├── LICENSE
└── README.md
```

## 注意事项

- 需要**管理员权限**才能修改 Windows Update 设置
- 暂停到期后 Windows 将自动恢复更新，你可以在到期前重新设置或延长
- 工具仅操作 Microsoft 官方文档记载的注册表键值，不修改系统文件

## 许可

本项目基于 **MIT License** 开源，详见 [LICENSE](LICENSE)。

仅供学习交流，使用本工具产生的任何后果由使用者自行承担。
