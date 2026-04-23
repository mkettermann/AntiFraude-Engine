namespace AntiFraude.Application.UseCases;

/// <summary>
/// Provides ambient access to the current correlation ID within a request scope.
/// Populated by CorrelationIdMiddleware before request processing begins.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string> _current = new();

    public static string Current
    {
        get => _current.Value ?? string.Empty;
        set => _current.Value = value;
    }
}
