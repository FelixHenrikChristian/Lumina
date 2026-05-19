using Microsoft.UI.Xaml.Media;

using Lumina.Core.Models;

namespace Lumina.App.Services;

public interface IFileThumbnailService
{
    Task<ImageSource?> LoadThumbnailAsync(
        FileItem file,
        int requestedSize,
        CancellationToken cancellationToken = default);
}
