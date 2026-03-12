using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace PowerInvoke;

/// <summary>
/// Executes PowerShell commands using a prepared <see cref="PowerShell"/> instance.
/// </summary>
public static class PowerShellCommandInvoker
{
    /// <summary>
    /// Invokes a command and binds the supplied parameters.
    /// </summary>
    public static Collection<PSObject> Invoke(
        PowerShell powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(powerShell);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(parameters);

        powerShell.Commands.Clear();
        powerShell.AddCommand(commandName);

        foreach (var parameter in parameters)
        {
            powerShell.AddParameter(parameter.Name, parameter.Value);
        }

        return powerShell.Invoke();
    }
}
