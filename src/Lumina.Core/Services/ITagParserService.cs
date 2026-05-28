namespace Lumina.Core.Services;

public interface ITagParserService
{
    IReadOnlyList<string> ParseTagsFromFilename(string filename);

    string GetDisplayName(string filename);

    string GetDisplayNameWithoutExtension(string filename);

    string InsertTagIntoFilename(string filename, string tag, int insertionIndex);

    string RemoveTagFromFilename(string filename, string tag);
}
