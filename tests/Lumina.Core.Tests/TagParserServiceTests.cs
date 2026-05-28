using Lumina.Core.Services;

namespace Lumina.Core.Tests;

public sealed class TagParserServiceTests
{
    private readonly TagParserService _parser = new();

    [Fact]
    public void ParseTagsFromFilename_NoPrefix_ReturnsEmptyList()
    {
        var tags = _parser.ParseTagsFromFilename("example.png");

        Assert.Empty(tags);
    }

    [Fact]
    public void ParseTagsFromFilename_LeadingTagBlock_ReturnsTags()
    {
        var tags = _parser.ParseTagsFromFilename("[work asset] example.png");

        Assert.Equal(["work", "asset"], tags);
    }

    [Fact]
    public void GetDisplayName_LeadingTagBlock_RemovesTagPrefix()
    {
        var displayName = _parser.GetDisplayName("[work asset] example.png");

        Assert.Equal("example.png", displayName);
    }

    [Fact]
    public void GetDisplayNameWithoutExtension_LeadingTagBlock_RemovesTagPrefixAndExtension()
    {
        var displayName = _parser.GetDisplayNameWithoutExtension("[work asset] example.png");

        Assert.Equal("example", displayName);
    }

    [Fact]
    public void InsertTagIntoFilename_NoExistingTags_AddsLeadingTagBlock()
    {
        var filename = _parser.InsertTagIntoFilename("example.png", "work", 0);

        Assert.Equal("[work] example.png", filename);
    }

    [Fact]
    public void InsertTagIntoFilename_ExistingTags_InsertsAtRequestedIndex()
    {
        var filename = _parser.InsertTagIntoFilename("[work asset] example.png", "urgent", 1);

        Assert.Equal("[work urgent asset] example.png", filename);
    }

    [Fact]
    public void InsertTagIntoFilename_ExistingTag_MovesTagInsteadOfDuplicating()
    {
        var filename = _parser.InsertTagIntoFilename("[work asset urgent] example.png", "asset", 2);

        Assert.Equal("[work urgent asset] example.png", filename);
    }

    [Fact]
    public void InsertTagIntoFilename_OutOfRangeIndex_ClampsToEnd()
    {
        var filename = _parser.InsertTagIntoFilename("[work] example.png", "urgent", 99);

        Assert.Equal("[work urgent] example.png", filename);
    }
}
