using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PowerInvoke;

namespace PowerInvoke.Tests;

public class GeneratedWrapperTests
{
    [Fact]
    public void Generated_wrapper_discovers_command_from_name_during_generation()
    {
        var expected = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var command = new DateCommands();
        var result = command.Get(date: expected);

        var actual = Assert.IsType<DateTime>(Assert.Single(result));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Generated_wrapper_returns_strongly_typed_output_when_cmdlet_declares_output_type()
    {
        using var runspace = CreateRunspace();

        var command = new WidgetCommands(runspace: runspace);
        var result = command.Get(name: "demo", count: 3);

        var item = Assert.IsType<WidgetResult>(Assert.Single(result));
        Assert.Equal("demo", item.Name);
        Assert.Equal(3, item.Count);
        Assert.Equal("Get-Widget", item.CommandName);
    }

    [Fact]
    public void Generated_wrapper_skips_unset_optional_parameters()
    {
        using var runspace = CreateRunspace();
        using var powerShell = PowerShell.Create(runspace);

        var command = new WidgetCommands(powerShell);
        var result = command.Get();

        var item = Assert.IsType<WidgetResult>(Assert.Single(result));
        Assert.Null(item.Name);
        Assert.False(item.HasCount);
    }

    [Fact]
    public void Generated_wrapper_prefers_supplied_powershell_instance()
    {
        using var runspace = CreateRunspace();
        using var powerShell = PowerShell.Create(runspace);
        using var alternateRunspace = RunspaceFactory.CreateRunspace();

        var command = new WidgetCommands(powerShell: powerShell, runspace: alternateRunspace);
        var result = command.Get(name: "demo");

        var item = Assert.IsType<WidgetResult>(Assert.Single(result));
        Assert.Equal("demo", item.Name);
    }

    private static Runspace CreateRunspace()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-Widget", typeof(GetWidgetCommand), helpFileName: null));

        var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();
        return runspace;
    }
}

[OutputType(typeof(WidgetResult))]
[Cmdlet(VerbsCommon.Get, "Widget")]
public sealed class GetWidgetCommand : PSCmdlet
{
    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public int Count { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new WidgetResult(
            MyInvocation.MyCommand.Name,
            Name,
            Count,
            MyInvocation.BoundParameters.ContainsKey(nameof(Count))));
    }
}

public sealed record WidgetResult(string CommandName, string? Name, int Count, bool HasCount);

[GeneratePowerShellWrapper(typeof(GetWidgetCommand))]
public partial class WidgetCommands
{
}

[GeneratePowerShellWrapper("Get-Date")]
public partial class DateCommands
{
}
