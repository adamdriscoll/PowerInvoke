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
        using var runspace = CreateRunspace();
        using var powerShell = PowerShell.Create(runspace);

        var command = new DateCommands(powerShell);
        var expected = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var result = command.Get(date: expected);

        var actual = Assert.IsType<DateTime>(Assert.Single(result).BaseObject);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Generated_wrapper_invokes_cmdlet_with_typed_parameters()
    {
        using var runspace = CreateRunspace();
        using var powerShell = PowerShell.Create(runspace);

        var command = new WidgetCommands(powerShell);
        var result = command.Get(name: "demo", count: 3);

        var item = Assert.Single(result);
        Assert.Equal("demo", item.Properties["Name"].Value);
        Assert.Equal(3, item.Properties["Count"].Value);
        Assert.Equal("Get-Widget", item.Properties["CommandName"].Value);
    }

    [Fact]
    public void Generated_wrapper_skips_unset_optional_parameters()
    {
        using var runspace = CreateRunspace();
        using var powerShell = PowerShell.Create(runspace);

        var command = new WidgetCommands(powerShell);
        var result = command.Get();

        var item = Assert.Single(result);
        Assert.Null(item.Properties["Name"].Value);
        Assert.False((bool)item.Properties["HasCount"].Value);
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

[Cmdlet(VerbsCommon.Get, "Widget")]
public sealed class GetWidgetCommand : PSCmdlet
{
    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public int Count { get; set; }

    protected override void ProcessRecord()
    {
        var result = new PSObject();
        result.Properties.Add(new PSNoteProperty("CommandName", MyInvocation.MyCommand.Name));
        result.Properties.Add(new PSNoteProperty("Name", Name));
        result.Properties.Add(new PSNoteProperty("Count", Count));
        result.Properties.Add(new PSNoteProperty("HasCount", MyInvocation.BoundParameters.ContainsKey(nameof(Count))));
        WriteObject(result);
    }
}

[GeneratePowerShellWrapper(typeof(GetWidgetCommand))]
public partial class WidgetCommands
{
}

[GeneratePowerShellWrapper("Get-Date")]
public partial class DateCommands
{
}
