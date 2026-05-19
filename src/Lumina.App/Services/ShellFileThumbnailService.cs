using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

using Lumina.Core.Models;

namespace Lumina.App.Services;

public sealed class ShellFileThumbnailService : IFileThumbnailService
{
    public async Task<ImageSource?> LoadThumbnailAsync(
        FileItem file,
        int requestedSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.IsDirectory ||
            file.PreviewKind == FilePreviewKind.None ||
            !File.Exists(file.Path))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var storageFile = await StorageFile
                .GetFileFromPathAsync(file.Path)
                .AsTask(cancellationToken);
            using var thumbnail = await storageFile
                .GetThumbnailAsync(
                    ThumbnailMode.SingleItem,
                    (uint)Math.Clamp(requestedSize, 96, 1024),
                    ThumbnailOptions.ResizeThumbnail)
                .AsTask(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (thumbnail is null ||
                thumbnail.Size == 0 ||
                thumbnail.Type == ThumbnailType.Icon)
            {
                return null;
            }

            var bitmapImage = new BitmapImage
            {
                DecodePixelWidth = Math.Clamp(requestedSize, 96, 1024),
            };
            await bitmapImage.SetSourceAsync(thumbnail).AsTask(cancellationToken);

            return bitmapImage;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
