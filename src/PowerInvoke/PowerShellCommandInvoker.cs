using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PowerInvoke;

/// <summary>
/// Executes PowerShell commands using either a supplied <see cref="PowerShell"/> instance
/// or one created on demand.
/// </summary>
public static class PowerShellCommandInvoker
{
    /// <summary>
    /// Invokes a command and binds the supplied parameters.
    /// </summary>
    public static Collection<PSObject> Invoke(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        Runspace? runspace = null)
    {
        return Invoke(powerShell, commandName, parameters, input: null, runspace);
    }

    /// <summary>
    /// Invokes a command, binds the supplied parameters, and streams pipeline input into the command.
    /// </summary>
    public static Collection<PSObject> Invoke(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        IEnumerable? input,
        Runspace? runspace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(parameters);

        var effectivePowerShell = PreparePowerShell(powerShell, commandName, parameters, runspace, out var ownedPowerShell);
        using (ownedPowerShell)
        {
            return input is null
                ? effectivePowerShell.Invoke()
                : effectivePowerShell.Invoke(input);
        }
    }

    /// <summary>
    /// Invokes a command and unwraps the resulting PowerShell values to a strongly typed collection.
    /// </summary>
    public static Collection<T> Invoke<T>(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        Runspace? runspace = null)
    {
        return Invoke<T>(powerShell, commandName, parameters, input: null, runspace);
    }

    /// <summary>
    /// Invokes a command with pipeline input and unwraps the resulting PowerShell values to a strongly typed collection.
    /// </summary>
    public static Collection<T> Invoke<T>(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        IEnumerable? input,
        Runspace? runspace = null)
    {
        var results = Invoke(powerShell, commandName, parameters, input, runspace);
        var typedResults = new Collection<T>();

        foreach (var result in results)
        {
            var value = Unwrap(result);
            if (value is not T typedValue)
            {
                throw new InvalidOperationException(
                    $"Command '{commandName}' produced '{value?.GetType().FullName ?? "null"}' which cannot be assigned to '{typeof(T).FullName}'.");
            }

            typedResults.Add(typedValue);
        }

        return typedResults;
    }

    /// <summary>
    /// Invokes a command and unwraps the resulting PowerShell values to their base objects.
    /// </summary>
    public static Collection<dynamic?> InvokeDynamic(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        Runspace? runspace = null)
    {
        return InvokeDynamic(powerShell, commandName, parameters, input: null, runspace);
    }

    /// <summary>
    /// Invokes a command with pipeline input and unwraps the resulting PowerShell values to their base objects.
    /// </summary>
    public static Collection<dynamic?> InvokeDynamic(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        IEnumerable? input,
        Runspace? runspace = null)
    {
        var results = Invoke(powerShell, commandName, parameters, input, runspace);
        var dynamicResults = new Collection<dynamic?>();

        foreach (var result in results)
        {
            dynamicResults.Add(Unwrap(result));
        }

        return dynamicResults;
    }

    private static object? Unwrap(PSObject result)
    {
        var value = result.BaseObject;
        return value?.GetType().FullName == "System.Management.Automation.Internal.AutomationNull" ? null : value;
    }

    private static PowerShell PreparePowerShell(
        PowerShell? powerShell,
        string commandName,
        IReadOnlyList<PowerShellParameter> parameters,
        Runspace? runspace,
        out PowerShell? ownedPowerShell)
    {
        ownedPowerShell = powerShell is null
            ? (runspace is null ? PowerShell.Create() : PowerShell.Create(runspace))
            : null;

        var effectivePowerShell = powerShell ?? ownedPowerShell!;
        effectivePowerShell.Commands.Clear();
        effectivePowerShell.AddCommand(commandName);

        foreach (var parameter in parameters)
        {
            effectivePowerShell.AddParameter(parameter.Name, parameter.Value);
        }

        return effectivePowerShell;
    }
}
