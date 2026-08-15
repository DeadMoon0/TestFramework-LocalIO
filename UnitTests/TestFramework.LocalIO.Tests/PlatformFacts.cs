using System;
using Xunit;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// A fact that really is skipped on non-Windows agents instead of returning green.
/// </summary>
/// <remarks>Pair it with <c>[Trait("Category", "WindowsOnly")]</c> so CI can filter it out as well.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>
    /// Creates the attribute and marks the test as skipped when the host is not Windows.
    /// </summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only test.";
        }
    }
}

/// <summary>
/// A fact that really is skipped on Windows agents instead of returning green.
/// </summary>
/// <remarks>Pair it with <c>[Trait("Category", "UnixOnly")]</c> so CI can filter it out as well.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UnixFactAttribute : FactAttribute
{
    /// <summary>
    /// Creates the attribute and marks the test as skipped when the host is Windows.
    /// </summary>
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Unix-only test.";
        }
    }
}
