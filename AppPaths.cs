using System.IO;

namespace HeadphoneHub;

public static class AppPaths
{
    public static bool IsPortable { get; } = File.Exists(Path.Combine(ExeDir(), "portable.txt"));

    public static string DataDir { get; } = IsPortable
        ? Path.Combine(ExeDir(), "data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeadphoneHub");

    private static string ExeDir() => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
}
