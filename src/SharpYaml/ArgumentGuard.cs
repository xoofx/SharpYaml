using System;
using System.Runtime.CompilerServices;

internal static class ArgumentGuard
{
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(object? argument, string? paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
#endif
}

