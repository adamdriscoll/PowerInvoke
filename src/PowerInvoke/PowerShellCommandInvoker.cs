using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace PowerInvoke;

public static class PowerShellCommandInvoker
{
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
