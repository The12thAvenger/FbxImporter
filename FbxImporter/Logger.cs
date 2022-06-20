namespace FbxImporter;

public static class Logger
{
    public static ILoggable? CurrentLoggable { get; set; }

    public static void Log(object message)
    {
        if (CurrentLoggable is null) return;
        CurrentLoggable.Log += "\n" + message;
    }
}

public interface ILoggable
{
    public string Log { get; set; }
}