using System;
using System.IO;

namespace FbxImporter.Util;

public static class ErrorMessages
{
    /// <summary>
    /// The text the log shows for a failed command: one line for the failures a user can fix (a missing,
    /// locked or unreadable file, or a file of the wrong kind), and the message with its stack trace for
    /// anything else, which is a bug.
    /// </summary>
    public static string Describe(Exception e)
    {
        return e switch
        {
            FileNotFoundException { FileName: { } path } => $"File not found: {path}",
            FileNotFoundException or DirectoryNotFoundException => e.Message,
            IOException or UnauthorizedAccessException or InvalidDataException => e.Message,
            _ => e.ToString()
        };
    }
}
