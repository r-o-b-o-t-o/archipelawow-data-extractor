using BLPSharp;
using SkiaSharp;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// Reads the textures and interface files of an extracted 3.3.5 client, and writes images out as WebP.
/// </summary>
public sealed class ClientTextures
{
    /// <summary>The entries of each directory visited so far, keyed by <see cref="Key"/>.</summary>
    private readonly Dictionary<string, Dictionary<string, string>> listings = [];

    // Checked on first use: only the tracker extracts need a client
    private readonly string root = Env.GetString("CLIENT_DIRECTORY");

    /// <summary>
    /// The file at <paramref name="relative"/> under the client directory, or null. The game looks files up
    /// ignoring case, and a few icons are shipped with a space before their extension
    /// ("Boss_Mekgineer_Thermaplugg .blp"), which the names referring to them sometimes have too.
    /// </summary>
    public string Find(string relative)
    {
        if (root == null || !Directory.Exists(Path.Combine(root, "Interface")))
        {
            throw new InvalidOperationException(
                "Client directory not found. Set CLIENT_DIRECTORY to an extracted client folder holding Interface, such as Data_enUS.");
        }

        string path = root;
        foreach (string part in relative.Replace('\\', '/').Split('/'))
        {
            if (!listings.TryGetValue(path, out var listing))
            {
                listing = Directory.Exists(path)
                    ? Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).GroupBy(Key).ToDictionary(g => g.Key, g => g.First())
                    : [];
                listings[path] = listing;
            }

            if (!listing.TryGetValue(Key(part), out string name))
            {
                return null;
            }
            path = Path.Combine(path, name);
        }
        return path;
    }

    private static string Key(string name)
    {
        int dot = name.LastIndexOf('.');
        return (dot < 0 ? name.Trim() : name[..dot].Trim() + name[dot..]).ToLowerInvariant();
    }

    /// <summary>A BLP texture, given without its extension, or null when the client has none by that name.</summary>
    public SKBitmap Load(string relative)
    {
        string file = Find(relative + ".blp");
        if (file == null)
        {
            return null;
        }

        using var stream = File.OpenRead(file);
        using var blp = new BLPFile(stream);
        byte[] pixels = blp.GetPixels(0, out int width, out int height);
        if (blp.alphaSize == 0)
        {
            // Opaque textures leave the alpha channel undefined
            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }

        // Skia only draws onto premultiplied bitmaps, while BLPs hold straight alpha
        using var image = SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul), pixels);
        var bitmap = NewBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return bitmap;
    }

    public static SKBitmap NewBitmap(int width, int height) => new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

    public string ReadText(string relative)
    {
        string file = Find(relative);
        return file == null ? null : File.ReadAllText(file);
    }

    public byte[] ReadBytes(string relative)
    {
        string file = Find(relative);
        return file == null ? null : File.ReadAllBytes(file);
    }

    /// <summary>Writes <paramref name="bitmap"/> as a lossy WebP, creating the directory if need be.</summary>
    public static void SaveWebp(SKBitmap bitmap, string file, int quality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality));
        using var output = File.Create(file);
        data.SaveTo(output);
    }

    /// <summary>The part of <paramref name="bitmap"/> inside <paramref name="rect"/>, scaled to the given size.</summary>
    public static SKBitmap Crop(SKBitmap bitmap, SKRectI rect, int width, int height)
    {
        var result = NewBitmap(width, height);
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, rect, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        return result;
    }
}
