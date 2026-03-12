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

    [Fact]
    public void Generated_wrapper_accepts_pipeline_input_by_value()
    {
        using var runspace = CreateRunspace();

        var command = new PipelineWidgetCommands(runspace: runspace);
        var result = command.Get(new[] { "first", "second" }, count: 7);

        var items = result.Cast<WidgetResult>().ToArray();
        Assert.Collection(
            items,
            first =>
            {
                Assert.Equal("first", first.Name);
                Assert.Equal(7, first.Count);
                Assert.True(first.HasCount);
            },
            second =>
            {
                Assert.Equal("second", second.Name);
                Assert.Equal(7, second.Count);
                Assert.True(second.HasCount);
            });
    }

    [Fact]
    public void Generated_wrapper_accepts_pipeline_input_by_property_name()
    {
        using var runspace = CreateRunspace();

        var command = new PropertyPipelineWidgetCommands(runspace: runspace);
        var result = command.Get(new[]
        {
            new NamedWidgetInput("alpha"),
            new NamedWidgetInput("beta")
        });

        var items = result.Cast<WidgetResult>().ToArray();
        Assert.Collection(
            items,
            first => Assert.Equal("alpha", first.Name),
            second => Assert.Equal("beta", second.Name));
    }

    [Fact]
    public void Generated_wrapper_exposes_pipeline_parameter_metadata()
    {
        var metadata = PipelineWidgetCommands.PipelineParameters;

        var pipelineParameter = Assert.Single(metadata);
        Assert.Equal("Name", pipelineParameter.Name);
        Assert.True(pipelineParameter.AcceptsValue);
        Assert.False(pipelineParameter.AcceptsPropertyName);
    }

    private static Runspace CreateRunspace()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-Widget", typeof(GetWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PipelineWidget", typeof(GetPipelineWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PropertyPipelineWidget", typeof(GetPropertyPipelineWidgetCommand), helpFileName: null));

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

public sealed record NamedWidgetInput(string Name);

[GeneratePowerShellWrapper(typeof(GetWidgetCommand))]
public partial class WidgetCommands
{
}

[OutputType(typeof(WidgetResult))]
[Cmdlet(VerbsCommon.Get, "PipelineWidget")]
public sealed class GetPipelineWidgetCommand : PSCmdlet
{
    [Parameter(ValueFromPipeline = true)]
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

[GeneratePowerShellWrapper(typeof(GetPipelineWidgetCommand))]
public partial class PipelineWidgetCommands
{
}

[OutputType(typeof(WidgetResult))]
[Cmdlet(VerbsCommon.Get, "PropertyPipelineWidget")]
public sealed class GetPropertyPipelineWidgetCommand : PSCmdlet
{
    [Parameter(ValueFromPipelineByPropertyName = true)]
    public string? Name { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new WidgetResult(
            MyInvocation.MyCommand.Name,
            Name,
            Count: 0,
            HasCount: false));
    }
}

[GeneratePowerShellWrapper(typeof(GetPropertyPipelineWidgetCommand))]
public partial class PropertyPipelineWidgetCommands
{
}

[GeneratePowerShellWrapper("Get-Date")]
public partial class DateCommands
{
}
