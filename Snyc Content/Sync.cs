using System.Security.Cryptography;

namespace Snyc_Content;

internal static class Sync
{
    public static void SynchronizeDirectory(string sourceDir, string replicaDir, string logFilePath)
    {
        if (!Directory.Exists(replicaDir))
        {
            Directory.CreateDirectory(replicaDir);
            Log($"Created directory: {replicaDir}", logFilePath);
        }

        // 1. Copy new or modified files from source to replica
        foreach (string sourceFile in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(sourceFile);
            string replicaFile = Path.Combine(replicaDir, fileName);

            try
            {
                if (!File.Exists(replicaFile))
                {
                    File.Copy(sourceFile, replicaFile, overwrite: true);
                    Log($"Created file: {replicaFile}", logFilePath);
                }
                else if (!FilesAreEqual(sourceFile, replicaFile))
                {
                    File.SetAttributes(replicaFile, FileAttributes.Normal);
                    File.Copy(sourceFile, replicaFile, overwrite: true);
                    Log($"Updated file: {replicaFile}", logFilePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log($"Error syncing file '{sourceFile}': {ex.Message}", logFilePath);
            }
        }

        // 2. Remove files in replica that no longer exist in source
        foreach (string replicaFile in Directory.GetFiles(replicaDir))
        {
            string fileName = Path.GetFileName(replicaFile);
            string sourceFile = Path.Combine(sourceDir, fileName);

            if (!File.Exists(sourceFile))
            {
                try
                {
                    File.SetAttributes(replicaFile, FileAttributes.Normal);
                    File.Delete(replicaFile);
                    Log($"Removed file: {replicaFile}", logFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log($"Error removing file '{replicaFile}': {ex.Message}", logFilePath);
                }
            }
        }

        // 3. Recursively sync subdirectories
        foreach (string sourceSubDir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(sourceSubDir);
            string replicaSubDir = Path.Combine(replicaDir, dirName);
            SynchronizeDirectory(sourceSubDir, replicaSubDir, logFilePath);
        }

        // 4. Remove directories in replica that no longer exist in source
        foreach (string replicaSubDir in Directory.GetDirectories(replicaDir))
        {
            string dirName = Path.GetFileName(replicaSubDir);
            string sourceSubDir = Path.Combine(sourceDir, dirName);

            if (!Directory.Exists(sourceSubDir))
            {
                DeleteDirectoryRecursive(replicaSubDir, logFilePath);
            }
        }
    }

    private static void DeleteDirectoryRecursive(string dirPath, string logFilePath)
    {
        foreach (string subDir in Directory.GetDirectories(dirPath))
        {
            DeleteDirectoryRecursive(subDir, logFilePath);
        }

        foreach (string file in Directory.GetFiles(dirPath))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                Log($"Removed file: {file}", logFilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log($"Error removing file '{file}': {ex.Message}", logFilePath);
            }
        }

        try
        {
            Directory.Delete(dirPath);
            Log($"Removed directory: {dirPath}", logFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"Error removing directory '{dirPath}': {ex.Message}", logFilePath);
        }
    }

    private static bool FilesAreEqual(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);

        // Fast check: if sizes differ, contents definitely differ
        if (firstInfo.Length != secondInfo.Length)
            return false;

        // Same size: compare MD5 hashes to verify content is identical
        using var md5 = MD5.Create();
        using var firstStream = File.OpenRead(firstPath);
        using var secondStream = File.OpenRead(secondPath);

        byte[] firstHash = md5.ComputeHash(firstStream);
        byte[] secondHash = md5.ComputeHash(secondStream);

        return firstHash.SequenceEqual(secondHash);
    }

    public static void Log(string message, string logFilePath)
    {
        // shortcut: single-threaded console/file append without locking, upgrade if parallel file copy is added
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(line);
        File.AppendAllText(logFilePath, line + Environment.NewLine);
    }
}
