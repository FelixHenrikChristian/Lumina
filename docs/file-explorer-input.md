# 文件资源管理器输入与文件操作规范

本文档定义 Lumina 文件网格的输入行为。Windows 原生位置以 Windows 文件资源
管理器和 Windows Shell 为准；浏览器文件系统访问 API 与演示文件系统在无法调用
Windows Shell 时提供能力受限的兼容实现。

## Windows 原生位置的原则

1. `Ctrl + C`、`Ctrl + X` 和 `Ctrl + V` 只使用 Windows 文件剪贴板，不维护一份
   优先级更高的 Lumina 私有剪贴板。Lumina 与文件资源管理器可以互相复制、剪切和
   粘贴。原生端必须通过 `CF_HDROP` 提供文件路径，并通过包含 DWORD
   `DROPEFFECT_COPY` / `DROPEFFECT_MOVE` 的 `Preferred DropEffect` 标识复制或剪切；
   不得使用仅能由 .NET 自身识别的序列化对象代替该 Shell 格式。
2. 复制、移动、重命名、新建文件夹和删除由 Windows `IFileOperation` 执行。文件
   冲突、跨卷移动、权限提升、进度和取消均使用 Windows Shell 语义。
3. 同目录复制由 Shell 自动生成副本名称；同目录剪切、目标冲突和文件占用等情况
   使用 Windows 提供的处理逻辑和对话框。
4. 用户触发的 Shell 操作加入 Explorer.exe 持有的会话级撤销记录，因此可在文件
   资源管理器中撤销 Lumina 发起的操作。Lumina 同时保留本次运行中由自身发起的
   可逆操作记录，供其 `Ctrl + Z` / `Ctrl + Y` 使用。
5. Lumina 监听当前原生目录。无论变更来自 Lumina、文件资源管理器还是任一端的
   撤销/重做，当前视图都应自动刷新。
6. Windows 剪贴板中的剪切项目在 Lumina 中显示为半透明；切回 Lumina 窗口时会
   重新读取系统剪贴板状态。

## 鼠标

| 输入 | 行为 |
| --- | --- |
| 单击文件卡片 | 选中该项并设置键盘焦点。 |
| Shift + 单击 | 从选区锚点扩展连续选区。 |
| Ctrl + 单击 | 切换该项是否选中。 |
| 双击文件夹 | 在 Lumina 中打开文件夹。 |
| 双击文件 | 使用系统默认应用打开。 |
| 单击网格空白处 | 清除选区并聚焦文件网格。 |
| Ctrl + 鼠标滚轮 | 调整文件卡片缩放。 |

## 键盘

带 Ctrl 的文件操作快捷键在 Lumina 窗口范围内生效，不依赖文件网格 DOM 焦点；
正在输入文本或显示菜单、模态对话框时不接管快捷键。方向键、Enter、F2、Delete、
Home 和 End 等无 Ctrl 的项目操作仍要求文件网格持有焦点。

| 按键 | 行为 |
| --- | --- |
| 方向键 | 按网格位置移动选中项；上下键按当前列数移动。 |
| Shift + 方向键 | 从选区锚点扩展连续选区。 |
| Ctrl + 方向键 | 只移动键盘焦点，不改变现有选区。 |
| Ctrl + Space | 切换焦点项是否选中。 |
| Ctrl + A | 选中当前列表全部项目。 |
| Home / End | 移到第一项 / 最后一项；支持 Shift 或 Ctrl 修饰。 |
| Enter | 打开焦点项。 |
| F2 | 重命名焦点项，Enter 提交，Escape 取消。 |
| Delete / Ctrl + D | 删除选中项；原生位置使用 Windows 回收站。 |
| Shift + Delete | 永久删除选中项并进行确认。 |
| Ctrl + C / Ctrl + X | 复制 / 剪切选中项。原生位置写入 Windows 文件剪贴板。 |
| Ctrl + V | 粘贴 Windows 文件剪贴板或浏览器后端的应用内剪贴板。 |
| Ctrl + Shift + N | 创建文件夹并立即开始重命名。 |
| Ctrl + Z | 撤销 Lumina 本次运行中最近一次可逆文件操作。 |
| Ctrl + Y / Ctrl + Shift + Z | 重做最近一次由 Lumina 撤销的操作。 |
| Ctrl + F / Ctrl + E | 聚焦并全选文件夹搜索框。 |
| Escape | 清除选区；在搜索框中清空当前搜索。 |
| F5 | 刷新当前文件夹。 |
| Alt + Up | 打开上一级目录。 |
| Alt + Left / Alt + Right | 后退 / 前进目录历史。 |
| Backspace | 后退目录历史。 |

## 浏览器与演示后端

浏览器安全模型不允许可靠读写 Windows 的 `CF_HDROP` 文件剪贴板，也不能使用
`IFileOperation` 或 Windows 回收站。因此这些后端使用 Lumina 应用内剪贴板和
自有冲突对话框；删除通常是永久的，无法提供与 Windows 完全相同的撤销语义。

## 已知平台边界

`Alt + Enter` 打开 Windows 属性窗口尚未迁移。Windows Shell 的撤销记录由
Explorer.exe 在用户会话内维护，但 Windows 没有公开一个可由任意第三方窗口直接
执行“全局 Shell 撤销”的稳定 API；因此 Lumina 可以让文件资源管理器撤销其原生
操作，但 Lumina 的快捷键不会接管一个在外部文件资源管理器窗口中完成、且未经过
Lumina 的历史操作。涉及覆盖或合并冲突的粘贴也应在文件资源管理器中使用会话级
Shell 撤销，因为 Lumina 无法安全重建被替换项目。目录监听仍会立即反映外部撤销
后的文件系统结果。

## 参考资料

- [Microsoft：IFileOperation](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation)
- [Microsoft：IFileOperation::SetOperationFlags](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)
- [Microsoft：Shell Clipboard Formats](https://learn.microsoft.com/windows/win32/shell/clipboard)
- [Microsoft：Clipboard.SetFileDropList](https://learn.microsoft.com/dotnet/api/system.windows.forms.clipboard.setfiledroplist)
