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
}
