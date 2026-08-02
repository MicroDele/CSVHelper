namespace CsvReaderApp;

internal static class CsvFileNameRules
{
    public static bool TryCreateTargetPath(
        string currentFilePath,
        string proposedFileName,
        out string targetPath,
        out string errorMessage)
    {
        var fileName = proposedFileName.Trim();
        if (fileName.Length == 0)
        {
            targetPath = string.Empty;
            errorMessage = "Enter a file name.";
            return false;
        }

        if (fileName is "." or ".." || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            targetPath = string.Empty;
            errorMessage = "The file name contains invalid characters.";
            return false;
        }

        var directory = Path.GetDirectoryName(currentFilePath) ?? string.Empty;
        targetPath = Path.Combine(directory, fileName);
        errorMessage = string.Empty;
        return true;
    }
}
