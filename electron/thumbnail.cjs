const DEFAULT_SIZE = { width: 640, height: 360 };

async function thumbnailDataUrlForPath(filePath, nativeImage, size = DEFAULT_SIZE) {
  try {
    const image = await nativeImage.createThumbnailFromPath(filePath, size);
    return image && !image.isEmpty() ? image.toDataURL() : null;
  } catch {
    return null;
  }
}

module.exports = { thumbnailDataUrlForPath };
