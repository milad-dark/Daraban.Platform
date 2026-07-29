namespace Daraban.Platform.Common;

public static class Guard
{
    public static T NotNull<T>(T? value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static string NotNullOrWhiteSpace(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be blank.", paramName) : value;
}
