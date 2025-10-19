// tests/Game.Chess.Tests.Unit/TestFileHelper.cs

using System.IO;

namespace Game.Core.Tests.Unit;

public static class TestFileHelper
{
    public static string GetOutputPath(string fileName, bool asReference = false)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(TestFileHelper).Assembly.Location)!;
        string rootDir = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\..\\.."));
        string subFolder = asReference ? "TestResultsReference" : "TestResults";
        return Path.Combine(rootDir, subFolder, "Game.Chess", fileName);
    }

    public static void SaveBytesToFile(byte[] bytes, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, bytes);
    }

    public static void SaveTextToFile(string text, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, text);
    }
}
