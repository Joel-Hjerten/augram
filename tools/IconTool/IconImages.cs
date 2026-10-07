using SkiaSharp;

/// <summary>Scaling, the disabled look and the macOS template image, as pure functions over SkiaSharp.</summary>
internal static class IconImages
{
    /// <summary>A disabled tray icon's opacity: dimmed enough that "off" is unmistakable (F7) without vanishing.</summary>
    public const float DisabledOpacity = 0.5f;

    /// <summary>The master scaled to <paramref name="size"/> square with high-quality (mipmapped) filtering, as PNG.</summary>
    public static byte[] Render(SKBitmap source, int size, float opacity = 1f, bool greyscale = false)
    {
        using var bitmap = Scale(source, size, opacity, greyscale);
        return Png(bitmap);
    }

    /// <summary>
    /// A macOS menu-bar template image: black plus alpha only (the system tints it for light and dark menu bars). From the
    /// app icon, alpha is its coverage with the white strokes cut out, so the star's outline reads as holes in the disc
    /// (Eyeris' rule: "white" is the straight colour's smallest channel; purple and black stay solid). A hand-drawn tray
    /// master (<paramref name="coverageOnly"/>) counts every painted pixel as shape.
    /// </summary>
    public static byte[] Template(SKBitmap source, int size, float opacity, bool coverageOnly)
    {
        using var scaled = Scale(source, size, 1f, greyscale: false);
        using var template = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = scaled.GetPixel(x, y);
                var shape = coverageOnly ? 1f : Math.Clamp((0.9f - (Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue)) / 255f)) / 0.3f, 0f, 1f);
                var alpha = (byte)Math.Round(pixel.Alpha * shape * opacity);
                template.SetPixel(x, y, new SKColor(0, 0, 0, alpha));
            }
        }

        return Png(template);
    }

    private static SKBitmap Scale(SKBitmap source, int size, float opacity, bool greyscale)
    {
        var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        if (greyscale || opacity < 1f)
        {
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(greyscale ? Greyscale(opacity) : Faded(opacity));
        }

        canvas.DrawBitmap(source, new SKRect(0, 0, size, size), paint);
        canvas.Flush();
        return bitmap;
    }

    private static byte[] Png(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Luminance (Rec. 709) into all three channels, alpha scaled by <paramref name="opacity"/>.</summary>
    private static float[] Greyscale(float opacity) =>
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0, 0, 0, opacity, 0,
    ];

    private static float[] Faded(float opacity) =>
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, opacity, 0,
    ];
}
