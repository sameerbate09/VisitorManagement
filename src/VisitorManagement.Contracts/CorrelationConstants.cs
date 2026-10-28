namespace VisitorManagement.Contracts;

public static class CorrelationConstants
{
    public const string HeaderName = "X-Correlation-ID";
    public const string CorrelationIdProperty = "CorrelationId";
    public const string SourceProperty = "Source";
}

public static class LogSources
{
    public const string Ui = "UI";
    public const string Api = "API";
    public const string Db = "DB";
    public const string Function = "FUNCTION";
}