<p align="center">
  <img src="build/icon.png" width="132" height="132" alt="Lumina 应用图标">
</p>

<h1 align="center">Lumina</h1>

<p align="center">
  <strong>一款本地优先、以标签为核心的 Windows 文件管理器。</strong><br>
  直接使用可迁移的文件名标签整理文件，无需数据库或云端账户。
</p>

<p align="center">
  <a href="https://github.com/FelixHenrikChristian/Lumina/releases/latest"><img src="https://img.shields.io/github/v/release/FelixHenrikChristian/Lumina?display_name=tag&sort=semver" alt="最新版本"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/FelixHenrikChristian/Lumina" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows&logoColor=white" alt="Windows 10 和 11">
</p>

<p align="center">
  <a href="README.md">English</a> · <strong>简体中文</strong>
</p>

Lumina 将标签直接保存在文件名开头，例如 `[工作 紧急] 报告.pdf`。无论是在文件资源管理器、备份、移动硬盘还是其他应用中，带标签的文件都能保持清晰、便于迁移。

## 下载

**[下载最新版本 →](https://github.com/FelixHenrikChristian/Lumina/releases/latest)**

| 安装包 | 适用场景 |
| --- | --- |
| `Lumina-Setup-<version>.exe` | 日常使用；提供开始菜单和桌面快捷方式、卸载程序及应用内更新 |
| `Lumina-Portable-<version>.exe` | 免安装使用；更新时手动下载并替换可执行文件 |

Lumina 支持 64 位 Windows 10 和 Windows 11。

> [!WARNING]
> Lumina 当前的 Windows 可执行文件尚未进行数字签名，Windows 可能显示“未知发布者”或 SmartScreen 警告。请仅从官方 Releases 页面下载，并在运行前将 SHA-256 摘要与 GitHub 显示的摘要进行比较。

```powershell
Get-FileHash .\Lumina-Setup-*.exe -Algorithm SHA256
```

## 功能亮点

| 分类 | 详细功能 |
| --- | --- |
| **本地访问** | 原生文件夹选择器，直接访问本地 Windows 文件系统 |
| **浏览导航** | 网格视图、面包屑导航、历史记录、递归搜索、排序、缩放、多选和键盘导航 |
| **文件操作** | 新建文件夹、重命名、复制、移动、粘贴、撤销/重做粘贴、回收站、使用默认应用打开，以及在文件资源管理器中显示 |
| **标签管理** | 可迁移的文件名标签、标签组、标签颜色、拖放添加标签、多标签筛选，以及兼容 TagSpaces 风格的导入与导出 |
| **媒体预览** | 图片预览和 Windows Shell 视频缩略图 |
| **界面体验** | 英文和简体中文、自定义壁纸，以及可配置的液态玻璃效果 |
| **自动更新** | 安装版自动检查更新，由你控制下载、进度和重启安装；便携版会打开手动下载页面 |

## 隐私优先

文件、标签、缩略图、设置和壁纸都保留在你的设备上。Lumina 不包含遥测、账户、云存储、广告或崩溃报告功能。更新检查会访问官方 GitHub Releases，但不会上传受管理的文件、标签或使用数据。详情请参阅[隐私说明](PRIVACY.md)。

## 开发

<details>
<summary><strong>在本地构建和测试 Lumina</strong></summary>

### 环境要求

- Windows 10 或 Windows 11
- Node.js 22.12 或更高版本
- npm

### 安装与验证

```powershell
npm ci
npm test
npm run lint
npm run build
npm run app:smoke
```

### 常用命令

```powershell
npm run dev       # 浏览器开发服务器
npm run app:dev   # Vite + Electron 热更新开发环境
npm run dist      # 生产构建、安装版和便携版
```

Electron 渲染进程启用了上下文隔离，并关闭了 Node.js 集成。本地文件系统操作仅通过明确声明的 IPC 接口开放，而且所有路径都会根据用户选择的文件夹根目录进行校验。

</details>

## 项目链接

[变更记录](CHANGELOG.md) · [安全策略](SECURITY.md) · [隐私说明](PRIVACY.md) ·
[支持](SUPPORT.md) · [第三方声明](THIRD_PARTY_NOTICES.md) ·
[维护者发布指南](docs/releasing.md)

Lumina 使用 [MIT License](LICENSE) 开源。
