using System.Diagnostics;
using Snyc_Content;

if (args.Length == 1 && args[0] == "--self-test")
{
    RunSelfTest();
    return 0;
}

if (args.Length != 4)
{
    Console.WriteLine("Usage: Snyc_Content <sourcePath> <replicaPath> <intervalSeconds> <logFilePath>");
    Console.WriteLine("   or: Snyc_Content --self-test");
    return 1;
}

string sourcePath = Path.GetFullPath(args[0]);
string replicaPath = Path.GetFullPath(args[1]);
string logFilePath = Path.GetFullPath(args[3]);

if (!int.TryParse(args[2], out int intervalSeconds) || intervalSeconds <= 0)
{
    Console.Error.WriteLine("Error: Sync interval must be a positive integer (in seconds).");
    return 1;
}

if (!Directory.Exists(sourcePath))
{
    Console.Error.WriteLine($"Error: Source directory does not exist: {sourcePath}");
    return 1;
}

if (PathsOverlap(sourcePath, replicaPath))
{
    Console.Error.WriteLine("Error: Source and replica directories cannot be the same or nested inside each other.");
    return 1;
}

if (IsSubPathOf(logFilePath, sourcePath) || IsSubPathOf(logFilePath, replicaPath))
{
    Console.Error.WriteLine("Error: Log file cannot be located inside the source or replica directory.");
    return 1;
}

string? logDir = Path.GetDirectoryName(logFilePath);
if (!string.IsNullOrEmpty(logDir))
{
    Directory.CreateDirectory(logDir);
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Sync.Log($"Starting synchronization: '{sourcePath}' -> '{replicaPath}' every {intervalSeconds}s.", logFilePath);

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        Sync.SynchronizeDirectory(sourcePath, replicaPath, logFilePath);
        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token);
    }
}
catch (OperationCanceledException)
{
    Sync.Log("Synchronization stopped by user.", logFilePath);
}

return 0;

static bool PathsOverlap(string pathA, string pathB) =>
    IsSubPathOf(pathA, pathB) || IsSubPathOf(pathB, pathA);

static bool IsSubPathOf(string candidatePath, string baseDirPath)
{
    string normalizedCandidate = Path.TrimEndingDirectorySeparator(candidatePath) + Path.DirectorySeparatorChar;
    string normalizedBase = Path.TrimEndingDirectorySeparator(baseDirPath) + Path.DirectorySeparatorChar;
    return normalizedCandidate.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
}

static void RunSelfTest()
{
    string tempRoot = Path.Combine(Path.GetTempPath(), "VeeamSyncTest_" + Guid.NewGuid().ToString("N")[..8]);
    string src = Path.Combine(tempRoot, "source");
    string rep = Path.Combine(tempRoot, "replica");
    string log = Path.Combine(tempRoot, "sync.log");

    try
    {
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "sub", "b.txt"), "world");

        // 1. Initial sync (creation)
        Sync.SynchronizeDirectory(src, rep, log);
        Debug.Assert(File.ReadAllText(Path.Combine(rep, "a.txt")) == "hello", "File a.txt should be copied");
        Debug.Assert(File.ReadAllText(Path.Combine(rep, "sub", "b.txt")) == "world", "Nested file b.txt should be copied");

        // 2. Modify file (same length so MD5 check is exercised) & delete nested folder
        File.WriteAllText(Path.Combine(src, "a.txt"), "HELLO");
        Directory.Delete(Path.Combine(src, "sub"), recursive: true);

        Sync.SynchronizeDirectory(src, rep, log);
        Debug.Assert(File.ReadAllText(Path.Combine(rep, "a.txt")) == "HELLO", "Modified file a.txt should be updated via MD5 check");
        Debug.Assert(!Directory.Exists(Path.Combine(rep, "sub")), "Deleted directory sub should be removed from replica");
        Debug.Assert(File.Exists(log), "Log file should be created");

        Console.WriteLine("Self-test passed.");
    }
    finally
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }
}
