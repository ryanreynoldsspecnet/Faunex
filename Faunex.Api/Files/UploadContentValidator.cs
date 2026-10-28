using SkiaSharp;

namespace Faunex.Api.Files;

public sealed record ValidatedUpload(byte[] Bytes, string ContentType);

public sealed class UploadContentValidator
{
    private readonly SemaphoreSlim _decodes = new(2);
    public async Task<ValidatedUpload> ValidateAsync(byte[] bytes, string fileName, string claimedType, bool isPhoto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 160 || fileName.Any(char.IsControl) || fileName.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new FileSecurityException("Use a filename without paths, control characters or more than 160 characters.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".pdf" && !isPhoto)
        {
            if (claimedType != "application/pdf" || bytes.Length < 12 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
                || !System.Text.Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).Contains("%%EOF", StringComparison.Ordinal))
                throw new FileSecurityException("The file does not match a PDF document.");
            return new(bytes, "application/pdf");
        }
        var type = extension is ".jpg" or ".jpeg" ? "image/jpeg" : extension == ".png" ? "image/png" : null;
        if (type is null || claimedType != type || bytes.Length > FileUploadOptions.MaxImageBytes)
            throw new FileSecurityException("Use a JPEG/PNG image up to 5 MB, or a PDF document up to 10 MB.");
        await _decodes.WaitAsync(cancellationToken);
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.EncodedFormat != (type == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg)
                || codec.Info.Width < 1 || codec.Info.Height < 1 || codec.Info.Width > 8192 || codec.Info.Height > 8192
                || (long)codec.Info.Width * codec.Info.Height > 16000000 || codec.FrameCount > 1)
                throw new FileSecurityException("Use a non-animated JPEG/PNG image with at most 16 million pixels.");
            using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw new FileSecurityException("The image is incomplete or invalid.");
            using var oriented = Orient(decoded, codec.EncodedOrigin);
            var ratio = Math.Min(1d, 1800d / Math.Max(oriented.Width, oriented.Height));
            using var resized = new SKBitmap(Math.Max(1, (int)(oriented.Width * ratio)), Math.Max(1, (int)(oriented.Height * ratio)));
            if (!oriented.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear))) throw new FileSecurityException("The image could not be processed.");
            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(type == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 85);
            if (encoded is null || encoded.Size > FileUploadOptions.MaxStoredImageBytes) throw new FileSecurityException("The processed image is too large. Use a smaller image.");
            return new(encoded.ToArray(), type);
        }
        finally { _decodes.Release(); }
    }

    private static SKBitmap Orient(SKBitmap input, SKEncodedOrigin origin)
    {
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var output = new SKBitmap(swapped ? input.Height : input.Width, swapped ? input.Width : input.Height);
        var w = input.Width; var h = input.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        using var canvas = new SKCanvas(output);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(input, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return output;
    }

    public async Task<byte[]> ThumbnailAsync(byte[] bytes, CancellationToken ct)
    {
        await _decodes.WaitAsync(ct);
        try
        {
            using var source = SKBitmap.Decode(bytes) ?? throw new FileServiceUnavailableException("Photo preview unavailable.");
            var ratio = Math.Min(1d, 320d / Math.Max(source.Width, source.Height));
            using var target = new SKBitmap(Math.Max(1, (int)(source.Width * ratio)), Math.Max(1, (int)(source.Height * ratio)));
            using (var canvas = new SKCanvas(target))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(source, new SKRect(0, 0, target.Width, target.Height), new SKSamplingOptions(SKFilterMode.Linear));
            }
            using var image = SKImage.FromBitmap(target);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 75);
            if (encoded is null || encoded.Size > 150 * 1024) throw new FileServiceUnavailableException("Photo preview unavailable.");
            return encoded.ToArray();
        }
        finally { _decodes.Release(); }
    }
}
