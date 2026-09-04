namespace ArchipelaWoW.DataExtractor.Services;

public static class OutputDirectory
{
    /// <summary>
    /// The directory the extracts are written to, created if it is not there yet.
    ///
    /// Every extractor resolves this before it starts rather than when it comes to write, so a missing
    /// OUT_DIR fails in a second instead of after a full pass over the database.
    /// </summary>
    public static string Prepare()
    {
        string outDir = Env.GetString("OUT_DIR") ?? throw new InvalidOperationException("\"OUT_DIR\" environment variable not set.");
        Directory.CreateDirectory(outDir);

        return outDir;
    }
}
