using System.Text.Json;
using System.Text.Json.Serialization;

using Lumina.Core.Models;

namespace Lumina.Core.Services;

public sealed class JsonTagGroupTransferService
{
    private const string AppName = "Lumina";
    private const string AppVersion = "1.0.0";
    private const int SettingsVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ITagGroupStore _tagGroupStore;

    public JsonTagGroupTransferService()
        : this(new JsonTagGroupStore())
    {
    }

    public JsonTagGroupTransferService(ITagGroupStore tagGroupStore)
    {
        _tagGroupStore = tagGroupStore;
    }

    public async Task ExportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var tagGroups = await _tagGroupStore.LoadAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var package = new PortableTagLibrary
        {
            ExportedAt = now,
            TagGroups = tagGroups.Select(group => ToPortableTagGroup(group, now)).ToList(),
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, package, SerializerOptions, cancellationToken);
    }

    public async Task<TagGroupImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var plan = CreateImportPlan(document.RootElement);
        await _tagGroupStore.SaveAsync(plan.TagGroups, cancellationToken);

        return new TagGroupImportResult
        {
            SourceFormat = plan.SourceFormat,
            TagGroupCount = plan.TagGroups.Count,
            TagCount = plan.TagGroups.Sum(group => group.Tags.Count),
        };
    }

    private static TagGroupImportPlan CreateImportPlan(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return new TagGroupImportPlan(
                "Lumina tag groups",
                DeserializeNativeTagGroups(root));
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The tag library file must be a JSON object or tag group array.");
        }

        if (!root.TryGetProperty("tagGroups", out var tagGroupsElement) ||
            tagGroupsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The tag library file does not contain a tagGroups array.");
        }

        var tagGroups = LooksLikePortableTagGroups(tagGroupsElement)
            ? DeserializePortableTagGroups(tagGroupsElement)
            : DeserializeNativeTagGroups(tagGroupsElement);

        return new TagGroupImportPlan(ResolveSourceFormat(root), tagGroups);
    }

    private static string ResolveSourceFormat(JsonElement root)
    {
        var appName = root.TryGetProperty("appName", out var appNameElement)
            ? appNameElement.GetString()
            : null;

        return string.IsNullOrWhiteSpace(appName)
            ? "TagSpaces"
            : appName.Trim();
    }

    private static IReadOnlyList<TagGroup> DeserializeNativeTagGroups(JsonElement element)
    {
        return element
            .Deserialize<IReadOnlyList<TagGroup>>(SerializerOptions)?
            .Select(NormalizeGroup)
            .ToList() ?? [];
    }

    private static IReadOnlyList<TagGroup> DeserializePortableTagGroups(JsonElement element)
    {
        return element
            .Deserialize<IReadOnlyList<PortableTagGroup>>(SerializerOptions)?
            .Select(ToTagGroup)
            .Select(NormalizeGroup)
            .ToList() ?? [];
    }

    private static bool LooksLikePortableTagGroups(JsonElement tagGroupsElement)
    {
        foreach (var groupElement in tagGroupsElement.EnumerateArray())
        {
            if (groupElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            return groupElement.TryGetProperty("title", out _) ||
                groupElement.TryGetProperty("uuid", out _) ||
                groupElement.TryGetProperty("children", out _);
        }

        return true;
    }

    private static PortableTagGroup ToPortableTagGroup(TagGroup group, DateTimeOffset timestamp)
    {
        var unixTime = timestamp.ToUnixTimeMilliseconds();

        return new PortableTagGroup
        {
            Title = group.Name,
            Uuid = group.Id,
            Children = group.Tags.Select(ToPortableTag).ToList(),
            CreatedDate = unixTime,
            Color = group.DefaultColor,
            TextColor = group.DefaultTextColor ?? GetContrastColor(group.DefaultColor),
            ModifiedDate = unixTime,
            Expanded = true,
            Description = group.Description,
        };
    }

    private static PortableTag ToPortableTag(Tag tag)
    {
        return new PortableTag
        {
            Id = tag.Id,
            Title = tag.Name,
            Color = tag.Color,
            TextColor = tag.TextColor ?? GetContrastColor(tag.Color),
        };
    }

    private static TagGroup ToTagGroup(PortableTagGroup group)
    {
        var groupId = string.IsNullOrWhiteSpace(group.Uuid)
            ? Guid.NewGuid().ToString("N")
            : group.Uuid.Trim();

        return new TagGroup
        {
            Id = groupId,
            Name = group.Title,
            DefaultColor = group.Color,
            DefaultTextColor = group.TextColor,
            Description = group.Description,
            Tags = group.Children.Select(tag => ToTag(tag, groupId)).ToList(),
        };
    }

    private static Tag ToTag(PortableTag tag, string groupId)
    {
        return new Tag
        {
            Id = string.IsNullOrWhiteSpace(tag.Id) ? Guid.NewGuid().ToString("N") : tag.Id.Trim(),
            Name = tag.Title,
            Color = tag.Color,
            TextColor = tag.TextColor,
            GroupId = groupId,
        };
    }

    private static TagGroup NormalizeGroup(TagGroup group)
    {
        var groupId = string.IsNullOrWhiteSpace(group.Id)
            ? Guid.NewGuid().ToString("N")
            : group.Id.Trim();

        return group with
        {
            Id = groupId,
            Name = NormalizeRequiredText(group.Name, "Untitled group"),
            DefaultColor = NormalizeColor(group.DefaultColor, "#2196f3"),
            DefaultTextColor = NormalizeColor(group.DefaultTextColor, "#ffffff"),
            Description = NormalizeOptionalText(group.Description),
            Tags = (group.Tags ?? []).Select(tag => NormalizeTag(tag, groupId)).ToList(),
        };
    }

    private static Tag NormalizeTag(Tag tag, string groupId)
    {
        return tag with
        {
            Id = string.IsNullOrWhiteSpace(tag.Id) ? Guid.NewGuid().ToString("N") : tag.Id.Trim(),
            Name = NormalizeRequiredText(tag.Name, "Untitled tag"),
            Color = NormalizeColor(tag.Color, "#2196f3"),
            TextColor = NormalizeColor(tag.TextColor, "#ffffff"),
            GroupId = groupId,
        };
    }

    private static string NormalizeRequiredText(string? text, string fallback)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string? NormalizeOptionalText(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeColor(string? color, string fallback)
    {
        var trimmed = color?.Trim();
        return IsHexColor(trimmed) ? trimmed! : fallback;
    }

    private static bool IsHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color) ||
            color[0] != '#' ||
            color.Length is not (7 or 9))
        {
            return false;
        }

        return color.Skip(1).All(Uri.IsHexDigit);
    }

    private static string GetContrastColor(string backgroundColor)
    {
        if (!IsHexColor(backgroundColor))
        {
            return "#ffffff";
        }

        var hex = backgroundColor.TrimStart('#');
        var offset = hex.Length == 8 ? 2 : 0;
        var red = Convert.ToInt32(hex.Substring(offset, 2), 16);
        var green = Convert.ToInt32(hex.Substring(offset + 2, 2), 16);
        var blue = Convert.ToInt32(hex.Substring(offset + 4, 2), 16);
        var brightness = (red * 299 + green * 587 + blue * 114) / 1000;

        return brightness > 128 ? "#000000" : "#ffffff";
    }

    private sealed record TagGroupImportPlan(
        string SourceFormat,
        IReadOnlyList<TagGroup> TagGroups);

    private sealed record PortableTagLibrary
    {
        public string AppName { get; init; } = JsonTagGroupTransferService.AppName;

        public string AppVersion { get; init; } = JsonTagGroupTransferService.AppVersion;

        public int SettingsVersion { get; init; } = JsonTagGroupTransferService.SettingsVersion;

        public DateTimeOffset ExportedAt { get; init; }

        public IReadOnlyList<PortableTagGroup> TagGroups { get; init; } = [];
    }

    private sealed record PortableTagGroup
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("uuid")]
        public string Uuid { get; init; } = string.Empty;

        [JsonPropertyName("children")]
        public IReadOnlyList<PortableTag> Children { get; init; } = [];

        [JsonPropertyName("created_date")]
        public long CreatedDate { get; init; }

        [JsonPropertyName("color")]
        public string Color { get; init; } = "#2196f3";

        [JsonPropertyName("textcolor")]
        public string TextColor { get; init; } = "#ffffff";

        [JsonPropertyName("modified_date")]
        public long ModifiedDate { get; init; }

        [JsonPropertyName("expanded")]
        public bool Expanded { get; init; } = true;

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }

    private sealed record PortableTag
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; init; } = "#2196f3";

        [JsonPropertyName("textcolor")]
        public string TextColor { get; init; } = "#ffffff";
    }
}
