# Lumina

[English](README.md) | **简体中文**

Lumina 是一款面向 Windows 的本地标签式文件管理器，采用液态玻璃界面。标签以文件名前导分组的形式存储，例如 `[工作 紧急] 报告.pdf`，因此带标签的文件可以自由迁移，不依赖单独的数据库。

## 下载

请从 [GitHub Releases](https://github.com/FelixHenrikChristian/Lumina/releases/latest) 下载最新版本。Lumina 支持 64 位 Windows 10 和 Windows 11。

- `Lumina-Setup-<version>.exe` 为安装版，会创建开始菜单和桌面快捷方式，并提供卸载程序。
- `Lumina-Portable-<version>.exe` 为免安装便携版。应用偏好设置仍会保存在当前 Windows 用户的应用数据目录中。

在自动更新功能加入之前发布的版本需要手动升级一次；此后的安装版可以在 Lumina 内完成更新。便携版仍需手动下载并替换。

> [!WARNING]
> Lumina 当前的 Windows 可执行文件尚未进行数字签名，Windows 可能显示“未知发布者”或 SmartScreen 警告。请仅从官方 Releases 页面下载，并在运行前将文件的 SHA-256 摘要与 GitHub 显示的摘要进行比较。

```powershell
Get-FileHash .\Lumina-Setup-*.exe -Algorithm SHA256
```

## 功能

- 原生文件夹选择器和本地 Windows 文件系统访问。
- 网格视图、图片缩略图和 Windows Shell 视频缩略图。
- 面包屑导航、历史记录、递归搜索、排序、缩放、多选和键盘导航。
- 新建文件夹、重命名、复制、移动、粘贴、撤销/重做粘贴、回收站、使用默认应用打开，以及在文件资源管理器中显示。
- 标签组、标签颜色、拖放添加标签、多标签筛选，以及兼容 TagSpaces 风格的导入和导出。
- 英文和简体中文界面。
- 自定义壁纸和可配置的液态玻璃外观。
- 安装版自动检查更新，由用户确认下载、查看进度并重启安装；便携版会跳转到手动下载页面。

Lumina 在本地处理文件，不包含遥测、账户、云存储、广告或崩溃报告功能。更新检查会访问官方 GitHub Releases，但不会上传受管理的文件、标签或使用数据。详情请参阅 [PRIVACY.md](PRIVACY.md)。

## 开发

环境要求：

- Windows 10 或 Windows 11
- Node.js 22.12 或更高版本
- npm

安装锁定版本的依赖并运行检查：

```powershell
npm ci
npm test
npm run lint
npm run build
npm run app:smoke
```

常用命令：

```powershell
npm run dev       # 浏览器开发服务器
npm run app:dev   # Vite + Electron 热更新开发环境
npm run dist      # 生产构建、安装版和便携版
```

Electron 渲染进程启用了上下文隔离，并关闭了 Node.js 集成。本地文件系统操作仅通过明确声明的 IPC 接口开放，而且所有路径都会根据用户选择的文件夹根目录进行校验。

## 发布流程

发布工作流由 `vX.Y.Z` 标签触发。它会确认标签与 `package.json` 中的版本一致，在 GitHub 托管的 Windows Runner 上运行测试和桌面端冒烟测试，构建两个可执行文件，并创建一个 **Draft Release**。工作流还会上传更新元数据和差分下载 blockmap，但不会自动正式发布；更新客户端无法看到草稿 Release。

准备发布前，应同时更新 `package.json` 和 `package-lock.json`。已经发布的标签及其附件不得替换。

```powershell
$newVersion = Read-Host "Release version"
npm version $newVersion --no-git-tag-version
$version = node -p "require('./package.json').version"
git tag -a "v$version" -m "Lumina $version"
git push origin "v$version"
```

Action 成功后，在 GitHub 上打开生成的草稿，检查发布说明、两个可执行文件、`latest.yml` 和安装包 blockmap。确认这些附件无误后，再点击 **Publish release**。

## 项目信息

- 变更记录：[CHANGELOG.md](CHANGELOG.md)
- 支持：[SUPPORT.md](SUPPORT.md)
- 安全策略：[SECURITY.md](SECURITY.md)
- 隐私说明：[PRIVACY.md](PRIVACY.md)
- 第三方声明：[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

Lumina 使用 [MIT License](LICENSE) 开源。
