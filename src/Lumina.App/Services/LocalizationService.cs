using System.Globalization;

using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.Services;

public static class LocalizationService
{
    private static readonly string SystemLanguageName = CultureInfo.CurrentUICulture.Name;

    private static string _preferredLanguage = DisplayLanguage.System;
    private static string _effectiveLanguage = ResolveEffectiveLanguage(DisplayLanguage.System);

    public static event EventHandler? LanguageChanged;

    public static string PreferredLanguage => _preferredLanguage;

    public static string EffectiveLanguage => _effectiveLanguage;

    public static void InitializeFromSettings()
    {
        var settings = new JsonDisplaySettingsStore()
            .LoadAsync()
            .GetAwaiter()
            .GetResult();

        ApplyLanguage(settings.Language, raiseChanged: false);
    }

    public static void ApplyLanguage(string? language)
    {
        ApplyLanguage(language, raiseChanged: true);
    }

    public static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Text.TryGetValue(key, out var value)
            ? value.Get(_effectiveLanguage)
            : key;
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private static void ApplyLanguage(string? language, bool raiseChanged)
    {
        var preferredLanguage = DisplayLanguage.Normalize(language);
        var effectiveLanguage = ResolveEffectiveLanguage(preferredLanguage);
        var culture = effectiveLanguage == DisplayLanguage.SimplifiedChinese
            ? CultureInfo.GetCultureInfo("zh-CN")
            : CultureInfo.GetCultureInfo(DisplayLanguage.English);

        _preferredLanguage = preferredLanguage;
        _effectiveLanguage = effectiveLanguage;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (raiseChanged)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    private static string ResolveEffectiveLanguage(string preferredLanguage)
    {
        if (preferredLanguage == DisplayLanguage.SimplifiedChinese ||
            (preferredLanguage == DisplayLanguage.System &&
                SystemLanguageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase)))
        {
            return DisplayLanguage.SimplifiedChinese;
        }

        return DisplayLanguage.English;
    }

    private static readonly IReadOnlyDictionary<string, LocalizedText> Text =
        new Dictionary<string, LocalizedText>(StringComparer.Ordinal)
        {
            ["Add"] = new("Add", "添加"),
            ["AddLocation"] = new("Add location", "添加位置"),
            ["AddTag"] = new("Add tag", "添加标签"),
            ["AddTagFailed"] = new("Add tag failed", "添加标签失败"),
            ["AddTagGroup"] = new("Add tag group", "添加标签组"),
            ["Amber"] = new("Amber", "琥珀色"),
            ["ApplyToAllConflicts"] = new("Do this for all current conflicts", "对当前所有冲突执行此操作"),
            ["Back"] = new("Back", "后退"),
            ["Black"] = new("Black", "黑色"),
            ["Blue"] = new("Blue", "蓝色"),
            ["Brown"] = new("Brown", "棕色"),
            ["BytesUnit"] = new("bytes", "字节"),
            ["Cancel"] = new("Cancel", "取消"),
            ["Cancelled"] = new("Cancelled", "已取消"),
            ["Cancelling"] = new("Cancelling...", "正在取消..."),
            ["ChooseManagedFolderFromLocationsPane"] = new("Choose a managed folder from the Locations pane.", "从“位置”面板选择一个已管理文件夹。"),
            ["Clear"] = new("Clear", "清空"),
            ["ClearLocations"] = new("Clear locations", "清空位置"),
            ["ClearLocationsContent"] = new("Remove all locations from Lumina? Files on disk will not be deleted.", "从 Lumina 移除所有位置？磁盘上的文件不会被删除。"),
            ["ClearTagFilter"] = new("Clear tag filter", "清除标签筛选"),
            ["ClearTags"] = new("Clear tags", "清空标签"),
            ["ClearTagsContent"] = new("Remove all tag groups and tags? Existing tags in file names will not be changed.", "移除所有标签组和标签？文件名里已有的标签不会被更改。"),
            ["ConflictDestinationLabel"] = new("Destination:", "目标:"),
            ["ConflictExists"] = new("An item named \"{0}\" already exists in the destination.", "目标位置已存在名为“{0}”的项目。"),
            ["ConflictSourceLabel"] = new("Source:", "来源:"),
            ["Copy"] = new("Copy", "复制"),
            ["CopyFailed"] = new("Copy failed", "复制失败"),
            ["CopyHere"] = new("Copy here", "复制到此处"),
            ["CopyOperation"] = new("Copy", "复制"),
            ["CopyOperationGerund"] = new("Copying", "正在复制"),
            ["CopyOperationVerb"] = new("copy", "复制"),
            ["CouldNotOpenFile"] = new("Could not open \"{0}\".\n\n{1}", "无法打开“{0}”。\n\n{1}"),
            ["Create"] = new("Create", "创建"),
            ["CreateAGroupBeforeAddingTags"] = new("Create a group before adding tags.", "先创建一个组，然后再添加标签。"),
            ["CreateOperation"] = new("Create", "创建"),
            ["CreateOperationGerund"] = new("Creating", "正在创建"),
            ["CreateOperationVerb"] = new("create", "创建"),
            ["CreatedLabel"] = new("Created:", "创建时间:"),
            ["CurrentFolder"] = new("current folder", "当前文件夹"),
            ["CustomColor"] = new("Custom color", "自定义颜色"),
            ["Cut"] = new("Cut", "剪切"),
            ["Cyan"] = new("Cyan", "青色"),
            ["DateCreated"] = new("Date created", "创建日期"),
            ["DateModified"] = new("Date modified", "修改日期"),
            ["DefaultTagColor"] = new("Default tag color", "默认标签颜色"),
            ["DefaultTextColor"] = new("Default text color", "默认文本颜色"),
            ["DeepOrange"] = new("Deep orange", "深橙色"),
            ["Delete"] = new("Delete", "删除"),
            ["DeleteFailed"] = new("Delete failed", "删除失败"),
            ["DeleteGroup"] = new("Delete group", "删除组"),
            ["DeleteLocation"] = new("Delete location", "删除位置"),
            ["DeleteLocationContent"] = new("Remove \"{0}\" from Lumina? Files on disk will not be deleted.", "从 Lumina 移除“{0}”？磁盘上的文件不会被删除。"),
            ["DeleteOperation"] = new("Delete", "删除"),
            ["DeleteOperationGerund"] = new("Deleting", "正在删除"),
            ["DeleteOperationVerb"] = new("delete", "删除"),
            ["DeleteTag"] = new("Delete tag", "删除标签"),
            ["DeleteTagFailed"] = new("Delete tag failed", "删除标签失败"),
            ["DeleteTagGroup"] = new("Delete tag group", "删除标签组"),
            ["DeleteTagGroupContent"] = new("Remove \"{0}\" and all tags in this group?", "移除“{0}”及该组内的所有标签？"),
            ["DeleteTagLibraryContent"] = new("Remove \"{0}\" from the tag library?", "从标签库移除“{0}”？"),
            ["Description"] = new("Description", "描述"),
            ["Done"] = new("Done", "完成"),
            ["DropOutsideCurrentLocation"] = new("Files can only be copied or moved into the current location.", "文件只能复制或移动到当前位置内。"),
            ["EditGroup"] = new("Edit group", "编辑组"),
            ["EditLocation"] = new("Edit location", "编辑位置"),
            ["EditTag"] = new("Edit tag", "编辑标签"),
            ["EditTagGroup"] = new("Edit tag group", "编辑标签组"),
            ["English"] = new("English", "English"),
            ["EnterDisplayName"] = new("Enter a display name", "输入显示名称"),
            ["EnterGroupName"] = new("Enter a group name", "输入组名称"),
            ["EnterTagName"] = new("Enter a tag name", "输入标签名称"),
            ["ExportFailed"] = new("Export failed", "导出失败"),
            ["ExportTagLibrary"] = new("Export tag library", "导出标签库"),
            ["FailedClearLocations"] = new("Failed to clear locations: {0}", "清空位置失败: {0}"),
            ["FailedClearTags"] = new("Failed to clear tags: {0}", "清空标签失败: {0}"),
            ["FailedDeleteLocation"] = new("Failed to delete location: {0}", "删除位置失败: {0}"),
            ["FailedDeleteTag"] = new("Failed to delete tag", "删除标签失败"),
            ["FailedDeleteTagGroup"] = new("Failed to delete tag group", "删除标签组失败"),
            ["FailedLoadFolder"] = new("Failed to load folder: {0}", "加载文件夹失败: {0}"),
            ["FailedLoadLocations"] = new("Failed to load locations: {0}", "加载位置失败: {0}"),
            ["FailedLoadTags"] = new("Failed to load tags: {0}", "加载标签失败: {0}"),
            ["FailedOpenLocation"] = new("Failed to open location: {0}", "打开位置失败: {0}"),
            ["FailedReorderTags"] = new("Failed to reorder tags", "重排标签失败"),
            ["FailedSaveLocation"] = new("Failed to save location: {0}", "保存位置失败: {0}"),
            ["FailedSaveSelectedLocation"] = new("Failed to save selected location: {0}", "保存所选位置失败: {0}"),
            ["FailedSaveTag"] = new("Failed to save tag", "保存标签失败"),
            ["FailedSaveTagGroup"] = new("Failed to save tag group", "保存标签组失败"),
            ["File"] = new("File", "文件"),
            ["FileTypeWithExtension"] = new("{0} File ({1})", "{0} 文件 ({1})"),
            ["FileOperationTitle"] = new("{0} {1}", "{0}{1}"),
            ["FilesAndFoldersWillAppearHere"] = new("Files and folders will appear here.", "文件和文件夹会显示在这里。"),
            ["Folder"] = new("Folder", "文件夹"),
            ["FolderError"] = new("Folder error", "文件夹错误"),
            ["FollowSystemTheme"] = new("Follow system theme", "跟随系统主题"),
            ["Forward"] = new("Forward", "前进"),
            ["Gray"] = new("Gray", "灰色"),
            ["Green"] = new("Green", "绿色"),
            ["Group"] = new("Group", "组"),
            ["GroupName"] = new("Group name", "组名称"),
            ["Import"] = new("Import", "导入"),
            ["ImportFailed"] = new("Import failed", "导入失败"),
            ["ImportSummary"] = new("Source: {0}, tags: {1}, groups: {2}", "来源: {0}, 标签: {1}, 组: {2}"),
            ["ImportTagLibrary"] = new("Import tag library", "导入标签库"),
            ["ImportTagLibraryContent"] = new("Importing a tag library replaces the current tag groups and tags.", "导入标签库会替换当前标签组和标签。"),
            ["Indigo"] = new("Indigo", "靛蓝色"),
            ["ItemCountMultiple"] = new("{0} items", "{0} 个项目"),
            ["ItemCountSingle"] = new("1 item", "1 个项目"),
            ["JsonTagLibrary"] = new("JSON tag library", "JSON 标签库"),
            ["KeepBoth"] = new("Keep both", "保留两者"),
            ["Language"] = new("Language", "语言"),
            ["LanguageEnglish"] = new("English", "English"),
            ["LanguageSimplifiedChinese"] = new("Simplified Chinese", "简体中文"),
            ["LanguageSystem"] = new("Use system setting", "使用系统设置"),
            ["Lime"] = new("Lime", "青柠色"),
            ["LoadingFolder"] = new("Loading folder...", "正在加载文件夹..."),
            ["LoadingLocations"] = new("Loading locations...", "正在加载位置..."),
            ["LoadingTags"] = new("Loading tags...", "正在加载标签..."),
            ["LocationActions"] = new("Location actions", "位置操作"),
            ["LocationError"] = new("Location error", "位置错误"),
            ["LocationLabel"] = new("Location:", "位置:"),
            ["LocationName"] = new("Location name", "位置名称"),
            ["Locations"] = new("Locations", "位置"),
            ["ManagedFoldersWillAppearHere"] = new("Managed folders will appear here.", "已管理文件夹会显示在这里。"),
            ["Merge"] = new("Merge", "合并"),
            ["MergeFolderTitle"] = new("Merge folder?", "合并文件夹？"),
            ["ModifiedLabel"] = new("Modified:", "修改时间:"),
            ["More"] = new("More", "更多"),
            ["MoveFailed"] = new("Move failed", "移动失败"),
            ["MoveHere"] = new("Move here", "移动到此处"),
            ["MoveOperation"] = new("Move", "移动"),
            ["MoveOperationGerund"] = new("Moving", "正在移动"),
            ["MoveOperationVerb"] = new("move", "移动"),
            ["MoveTag"] = new("Move tag", "移动标签"),
            ["Name"] = new("Name", "名称"),
            ["NameCannotBeEmpty"] = new("The name cannot be empty.", "名称不能为空。"),
            ["New"] = new("New", "新建"),
            ["NewFolder"] = new("New folder", "新建文件夹"),
            ["NewFolderFailed"] = new("New folder failed", "新建文件夹失败"),
            ["NoLocationSelected"] = new("No location selected", "未选择位置"),
            ["NoLocationsYet"] = new("No locations yet", "还没有位置"),
            ["NoTagGroupsYet"] = new("No tag groups yet", "还没有标签组"),
            ["NoTags"] = new("No tags", "无标签"),
            ["NoTagsInLibrary"] = new("No tags in library.", "标签库中没有标签。"),
            ["NoTagsInThisGroup"] = new("No tags in this group.", "此组中没有标签。"),
            ["OK"] = new("OK", "确定"),
            ["Open"] = new("Open", "打开"),
            ["OpenContainingFolder"] = new("Open containing folder: {0}", "打开所在文件夹: {0}"),
            ["OpenFileFailed"] = new("Open file failed", "打开文件失败"),
            ["OpenInFileExplorer"] = new("Open in File Explorer", "在文件资源管理器中打开"),
            ["OpenInFileExplorerFailed"] = new("Open in File Explorer failed", "在文件资源管理器中打开失败"),
            ["OpenLocationFailed"] = new("Open location failed", "打开位置失败"),
            ["OperationComplete"] = new("{0} complete", "{0}完成"),
            ["OperationProgressDetailBytes"] = new("{0} of {1}", "{0} / {1}"),
            ["OperationProgressDetailItems"] = new("{0} of {1} items", "{0} / {1} 个项目"),
            ["OperationRunning"] = new("{0}...", "{0}..."),
            ["OptionalDescription"] = new("Optional description", "可选描述"),
            ["Orange"] = new("Orange", "橙色"),
            ["Paste"] = new("Paste", "粘贴"),
            ["PasteFailed"] = new("Paste failed", "粘贴失败"),
            ["PermanentlyDeleteItemContent"] = new("\"{0}\" will be deleted permanently.", "“{0}”将被永久删除。"),
            ["PermanentlyDeleteItemTitle"] = new("Permanently delete item?", "永久删除项目？"),
            ["PermanentlyDeleteItemsContent"] = new("The selected items will be deleted permanently.", "所选项目将被永久删除。"),
            ["PermanentlyDeleteItemsTitle"] = new("Permanently delete {0} items?", "永久删除 {0} 个项目？"),
            ["Pink"] = new("Pink", "粉色"),
            ["Preparing"] = new("Preparing...", "正在准备..."),
            ["PreparingToOperation"] = new("Preparing to {0}...", "正在准备{0}..."),
            ["Properties"] = new("Properties", "属性"),
            ["PropertiesFailed"] = new("Properties failed", "属性失败"),
            ["PropertiesTitle"] = new("{0} Properties", "{0} 属性"),
            ["Purple"] = new("Purple", "紫色"),
            ["Redo"] = new("Redo", "重做"),
            ["RedoFailed"] = new("Redo failed", "重做失败"),
            ["RedoingItems"] = new("Redoing {0}", "正在重做{0}"),
            ["Red"] = new("Red", "红色"),
            ["Refresh"] = new("Refresh", "刷新"),
            ["Rename"] = new("Rename", "重命名"),
            ["RenameFailed"] = new("Rename failed", "重命名失败"),
            ["RenameOperation"] = new("Rename", "重命名"),
            ["RenameOperationGerund"] = new("Renaming", "正在重命名"),
            ["RenameOperationVerb"] = new("rename", "重命名"),
            ["ReorderTags"] = new("Reorder tags", "重排标签"),
            ["ReorderTagsHelp"] = new("Drag a row to reorder (no need to select it first). Use Sort A to Z for alphabetical order, then drag to fine-tune.", "拖动一行即可重排（无需先选择）。可先用“按 A 到 Z 排序”按字母排序，再拖动微调。"),
            ["ReorderTagsTitle"] = new("Reorder tags in {0}", "重排“{0}”中的标签"),
            ["Replace"] = new("Replace", "替换"),
            ["ReplaceOrSkipFileTitle"] = new("Replace or skip file?", "替换还是跳过文件？"),
            ["Save"] = new("Save", "保存"),
            ["SavedToPath"] = new("Saved to {0}", "已保存到 {0}"),
            ["Search"] = new("Search", "搜索"),
            ["SearchInFolder"] = new("Search in {0}", "在 {0} 中搜索"),
            ["SelectLocation"] = new("Select a location", "选择位置"),
            ["Settings"] = new("Settings", "设置"),
            ["ShowInFileExplorer"] = new("Show in File Explorer", "在文件资源管理器中显示"),
            ["SimplifiedChineseSearch"] = new("Simplified/traditional Chinese search", "简繁中文搜索"),
            ["Size"] = new("Size", "大小"),
            ["SizeLabel"] = new("Size:", "大小:"),
            ["SizeOnDiskLabel"] = new("Size on disk:", "占用空间:"),
            ["Skip"] = new("Skip", "跳过"),
            ["Slate"] = new("Slate", "石板灰"),
            ["Sort"] = new("Sort", "排序"),
            ["SortAToZ"] = new("Sort A to Z", "按 A 到 Z 排序"),
            ["SortAscending"] = new("Ascending", "升序"),
            ["SortDescending"] = new("Descending", "降序"),
            ["SortTagsAlphabeticallyHint"] = new("Sort tags alphabetically (you can still drag items afterward).", "按字母顺序排序标签（之后仍可拖动调整）。"),
            ["TagColor"] = new("Tag color", "标签颜色"),
            ["TagError"] = new("Tag error", "标签错误"),
            ["TagFilter"] = new("Tag filter", "标签筛选"),
            ["TagLibraryExported"] = new("Tag library exported", "标签库已导出"),
            ["TagLibraryImported"] = new("Tag library imported", "标签库已导入"),
            ["TagName"] = new("Tag name", "标签名称"),
            ["TagPreview"] = new("Tag Preview", "标签预览"),
            ["Tags"] = new("Tags", "标签"),
            ["Teal"] = new("Teal", "蓝绿色"),
            ["TextColor"] = new("Text color", "文本颜色"),
            ["ThisFolderIsEmpty"] = new("This folder is empty", "此文件夹为空"),
            ["Type"] = new("Type", "类型"),
            ["TypeLabel"] = new("Type:", "类型:"),
            ["Undo"] = new("Undo", "撤销"),
            ["UndoFailed"] = new("Undo failed", "撤销失败"),
            ["UndoingItems"] = new("Undoing {0}", "正在撤销{0}"),
            ["UntitledGroup"] = new("Untitled group", "未命名组"),
            ["UntitledTag"] = new("Untitled tag", "未命名标签"),
            ["UpOneLevel"] = new("Up one level", "上一级"),
            ["White"] = new("White", "白色"),
            ["Yellow"] = new("Yellow", "黄色"),
            ["ZeroBytes"] = new("0 bytes", "0 字节"),
        };

    private sealed record LocalizedText(string English, string SimplifiedChinese)
    {
        public string Get(string language)
        {
            return language == DisplayLanguage.SimplifiedChinese
                ? SimplifiedChinese
                : English;
        }
    }
}
