namespace PowerInvoke;

/// <summary>
/// Describes how a command parameter participates in PowerShell pipeline binding.
/// </summary>
public readonly record struct PowerShellPipelineParameter(
    string Name,
    string ParameterTypeName,
    bool AcceptsValue,
    bool AcceptsPropertyName);
