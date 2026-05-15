using System.Collections.ObjectModel;

using Microsoft.UI.Xaml;

using Lumina.Core.Models;
using Lumina.Core.Services;

namespace Lumina.App.ViewModels;

public sealed class TagSidebarViewModel : ObservableObject
{
    private readonly List<TagGroup> _tagGroups = [];
    private readonly ITagGroupStore _tagGroupStore;

    private string? _errorMessage;
    private bool _isBusy;

    public TagSidebarViewModel()
        : this(new JsonTagGroupStore())
    {
    }

    public TagSidebarViewModel(ITagGroupStore tagGroupStore)
    {
        _tagGroupStore = tagGroupStore;
    }

    public ObservableCollection<TagGroupItemViewModel> TagGroups { get; } = [];

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnComputedStateChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnComputedStateChanged();
            }
        }
    }

    public bool CanAddGroup => !IsBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasTagGroups => _tagGroups.Count > 0;

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility =>
        !IsBusy && !HasError && !HasTagGroups ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TagGroupListVisibility =>
        !IsBusy && HasTagGroups ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var savedGroups = await _tagGroupStore.LoadAsync(cancellationToken);

            ReplaceGroups(savedGroups.Select(NormalizeGroup));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load tags: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public IReadOnlyList<TagGroup> GetGroupsSnapshot()
    {
        return _tagGroups.ToList();
    }

    public async Task AddGroupAsync(
        string name,
        string? description,
        string defaultColor,
        string defaultTextColor,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName.Length == 0)
        {
            return;
        }

        var group = new TagGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            Description = NormalizeOptionalText(description),
            DefaultColor = NormalizeColor(defaultColor, "#2196f3"),
            DefaultTextColor = NormalizeColor(defaultTextColor, "#ffffff"),
            Tags = [],
        };

        await MutateAndSaveAsync(
            groups => groups.Add(group),
            "Failed to save tag group",
            cancellationToken);
    }

    public async Task RenameGroupAsync(
        string groupId,
        string name,
        string? description,
        string defaultColor,
        string defaultTextColor,
        CancellationToken cancellationToken = default)
    {
        var index = IndexOfGroup(groupId);
        var normalizedName = NormalizeName(name);
        if (index < 0 || normalizedName.Length == 0)
        {
            return;
        }

        await MutateAndSaveAsync(
            groups =>
            {
                var original = groups[index];
                groups[index] = original with
                {
                    Name = normalizedName,
                    Description = NormalizeOptionalText(description),
                    DefaultColor = NormalizeColor(defaultColor, "#2196f3"),
                    DefaultTextColor = NormalizeColor(defaultTextColor, "#ffffff"),
                };
            },
            "Failed to save tag group",
            cancellationToken);
    }

    public async Task DeleteGroupAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        var index = IndexOfGroup(groupId);
        if (index < 0)
        {
            return;
        }

        await MutateAndSaveAsync(
            groups => groups.RemoveAt(index),
            "Failed to delete tag group",
            cancellationToken);
    }

    public async Task AddTagAsync(
        string groupId,
        string name,
        string color,
        string textColor,
        CancellationToken cancellationToken = default)
    {
        var index = IndexOfGroup(groupId);
        var normalizedName = NormalizeName(name);
        if (index < 0 || normalizedName.Length == 0)
        {
            return;
        }

        var group = _tagGroups[index];
        var tag = new Tag
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            Color = NormalizeColor(color, group.DefaultColor),
            TextColor = NormalizeColor(textColor, group.DefaultTextColor ?? "#ffffff"),
            GroupId = group.Id,
        };

        await MutateAndSaveAsync(
            groups =>
            {
                var original = groups[index];
                groups[index] = original with
                {
                    Tags = original.Tags.Concat([tag]).ToList(),
                };
            },
            "Failed to save tag",
            cancellationToken);
    }

    public async Task RenameTagAsync(
        string tagId,
        string targetGroupId,
        string name,
        string color,
        string textColor,
        CancellationToken cancellationToken = default)
    {
        var currentGroup = FindGroupContainingTag(tagId);
        var targetGroup = FindGroup(targetGroupId);
        var currentTag = currentGroup?.Tags.FirstOrDefault(tag => tag.Id == tagId);
        var normalizedName = NormalizeName(name);

        if (currentGroup is null || targetGroup is null || currentTag is null || normalizedName.Length == 0)
        {
            return;
        }

        var updatedTag = currentTag with
        {
            Name = normalizedName,
            Color = NormalizeColor(color, targetGroup.DefaultColor),
            TextColor = NormalizeColor(textColor, targetGroup.DefaultTextColor ?? "#ffffff"),
            GroupId = targetGroup.Id,
        };

        await MutateAndSaveAsync(
            groups =>
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group.Id == currentGroup.Id && group.Id == targetGroup.Id)
                    {
                        groups[i] = group with
                        {
                            Tags = group.Tags.Select(tag => tag.Id == tagId ? updatedTag : tag).ToList(),
                        };
                    }
                    else if (group.Id == currentGroup.Id)
                    {
                        groups[i] = group with
                        {
                            Tags = group.Tags.Where(tag => tag.Id != tagId).ToList(),
                        };
                    }
                    else if (group.Id == targetGroup.Id)
                    {
                        groups[i] = group with
                        {
                            Tags = group.Tags.Concat([updatedTag]).ToList(),
                        };
                    }
                }
            },
            "Failed to save tag",
            cancellationToken);
    }

    public async Task DeleteTagAsync(
        string tagId,
        CancellationToken cancellationToken = default)
    {
        var currentGroup = FindGroupContainingTag(tagId);
        if (currentGroup is null)
        {
            return;
        }

        await MutateAndSaveAsync(
            groups =>
            {
                var index = groups.FindIndex(group => group.Id == currentGroup.Id);
                if (index < 0)
                {
                    return;
                }

                var group = groups[index];
                groups[index] = group with
                {
                    Tags = group.Tags.Where(tag => tag.Id != tagId).ToList(),
                };
            },
            "Failed to delete tag",
            cancellationToken);
    }

    public TagGroup? FindGroup(string groupId)
    {
        return _tagGroups.FirstOrDefault(group => group.Id == groupId);
    }

    private async Task MutateAndSaveAsync(
        Action<List<TagGroup>> mutate,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var snapshot = _tagGroups.ToList();
        ErrorMessage = null;

        mutate(_tagGroups);
        RefreshTagGroups();

        try
        {
            await SaveGroupsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ReplaceGroups(snapshot);
            ErrorMessage = $"{failureMessage}: {ex.Message}";
        }
    }

    private Task SaveGroupsAsync(CancellationToken cancellationToken)
    {
        return _tagGroupStore.SaveAsync(_tagGroups.ToList(), cancellationToken);
    }

    private void ReplaceGroups(IEnumerable<TagGroup> groups)
    {
        _tagGroups.Clear();
        _tagGroups.AddRange(groups.Select(NormalizeGroup));

        RefreshTagGroups();
    }

    private void RefreshTagGroups()
    {
        TagGroups.Clear();
        foreach (var group in _tagGroups.Select(group => new TagGroupItemViewModel(group)))
        {
            TagGroups.Add(group);
        }

        OnComputedStateChanged();
    }

    private int IndexOfGroup(string groupId)
    {
        return _tagGroups.FindIndex(group => group.Id == groupId);
    }

    private TagGroup? FindGroupContainingTag(string tagId)
    {
        return _tagGroups.FirstOrDefault(group => group.Tags.Any(tag => tag.Id == tagId));
    }

    private static TagGroup NormalizeGroup(TagGroup group)
    {
        var groupId = string.IsNullOrWhiteSpace(group.Id)
            ? Guid.NewGuid().ToString("N")
            : group.Id.Trim();

        return group with
        {
            Id = groupId,
            Name = NormalizeRequiredName(group.Name, "Untitled group"),
            DefaultColor = NormalizeColor(group.DefaultColor, "#2196f3"),
            DefaultTextColor = NormalizeColor(group.DefaultTextColor, "#ffffff"),
            Description = NormalizeOptionalText(group.Description),
            Tags = group.Tags.Select(tag => NormalizeTag(tag, groupId)).ToList(),
        };
    }

    private static Tag NormalizeTag(Tag tag, string groupId)
    {
        return tag with
        {
            Id = string.IsNullOrWhiteSpace(tag.Id) ? Guid.NewGuid().ToString("N") : tag.Id.Trim(),
            Name = NormalizeRequiredName(tag.Name, "Untitled tag"),
            Color = NormalizeColor(tag.Color, "#2196f3"),
            TextColor = NormalizeColor(tag.TextColor, "#ffffff"),
            GroupId = groupId,
        };
    }

    private static string NormalizeName(string? name)
    {
        return name?.Trim() ?? string.Empty;
    }

    private static string NormalizeRequiredName(string? name, string fallback)
    {
        var normalized = NormalizeName(name);
        return normalized.Length == 0 ? fallback : normalized;
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

    private void OnComputedStateChanged()
    {
        OnPropertyChanged(nameof(CanAddGroup));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasTagGroups));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(TagGroupListVisibility));
    }
}

public sealed class TagGroupItemViewModel
{
    public TagGroupItemViewModel(TagGroup group)
    {
        Model = group;
        Tags = [.. group.Tags.Select(tag => new TagItemViewModel(tag))];
    }

    public TagGroup Model { get; }

    public ObservableCollection<TagItemViewModel> Tags { get; }

    public string Id => Model.Id;

    public string Name => Model.Name;

    public string DefaultColor => Model.DefaultColor;

    public string DefaultTextColor => Model.DefaultTextColor ?? "#ffffff";

    public string? Description => Model.Description;

    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EmptyTagsVisibility => Tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TagsVisibility => Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}

public sealed class TagItemViewModel
{
    public TagItemViewModel(Tag tag)
    {
        Model = tag;
    }

    public Tag Model { get; }

    public string Id => Model.Id;

    public string Name => Model.Name;

    public string Color => Model.Color;

    public string TextColor => Model.TextColor ?? "#ffffff";

    public string? GroupId => Model.GroupId;
}
