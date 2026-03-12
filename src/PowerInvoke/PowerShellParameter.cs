namespace PowerInvoke;

/// <summary>
/// Represents a single named PowerShell parameter.
/// </summary>
public readonly record struct PowerShellParameter(string Name, object? Value);
