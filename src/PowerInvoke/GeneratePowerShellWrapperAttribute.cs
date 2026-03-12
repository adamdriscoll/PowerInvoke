using System;

namespace PowerInvoke;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GeneratePowerShellWrapperAttribute : Attribute
{
    public GeneratePowerShellWrapperAttribute(Type cmdletType)
    {
        CmdletType = cmdletType ?? throw new ArgumentNullException(nameof(cmdletType));
    }

    public GeneratePowerShellWrapperAttribute(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        CommandName = commandName;
    }

    public Type? CmdletType { get; }

    public string? CommandName { get; }
}
