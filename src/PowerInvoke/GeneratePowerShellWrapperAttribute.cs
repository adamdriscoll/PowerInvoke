using System;

namespace PowerInvoke;

/// <summary>
/// Marks a partial class for generation of strongly typed PowerShell wrapper methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GeneratePowerShellWrapperAttribute : Attribute
{
    /// <summary>
    /// Generates a wrapper from a cmdlet type.
    /// </summary>
    public GeneratePowerShellWrapperAttribute(Type cmdletType)
    {
        CmdletType = cmdletType ?? throw new ArgumentNullException(nameof(cmdletType));
    }

    /// <summary>
    /// Generates a wrapper by discovering a command by name.
    /// </summary>
    public GeneratePowerShellWrapperAttribute(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        CommandName = commandName;
    }

    /// <summary>
    /// Gets the cmdlet type used for generation, when generation is type-based.
    /// </summary>
    public Type? CmdletType { get; }

    /// <summary>
    /// Gets the command name used for generation, when generation is name-based.
    /// </summary>
    public string? CommandName { get; }
}
