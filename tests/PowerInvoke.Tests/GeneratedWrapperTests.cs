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

    [Fact]
    public void Generated_wrapper_allows_valid_parameter_set_combinations()
    {
        using var runspace = CreateRunspace();

        var command = new MultiTargetWidgetCommands(runspace: runspace);

        var byName = Assert.IsType<ParameterSetResult>(Assert.Single(command.Get(name: "alpha")));
        var byId = Assert.IsType<ParameterSetResult>(Assert.Single(command.Get(id: 42)));

        Assert.Equal("ByName", byName.ParameterSetName);
        Assert.Equal("alpha", byName.Name);
        Assert.Null(byName.Id);

        Assert.Equal("ById", byId.ParameterSetName);
        Assert.Null(byId.Name);
        Assert.Equal(42, byId.Id);
    }

    [Fact]
    public void Generated_wrapper_surfaces_invalid_parameter_set_combinations()
    {
        using var runspace = CreateRunspace();

        var command = new MultiTargetWidgetCommands(runspace: runspace);
        var exception = Assert.Throws<ParameterBindingException>(() => command.Get(name: "alpha", id: 42));

        Assert.Contains("Parameter set", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generated_new_wrapper_feels_like_creating_a_record()
    {
        using var runspace = CreateRunspace();

        var command = new NewWidgetLifecycleCommands(runspace: runspace);
        var result = command.New(name: "alpha", count: 3, passThru: SwitchParameter.Present);

        var item = Assert.IsType<WidgetLifecycleResult>(Assert.Single(result));
        Assert.Equal("New", item.Operation);
        Assert.Equal("alpha", item.Name);
        Assert.Equal(3, item.Count);
        Assert.True(item.FlagWasBound);
        Assert.Equal("Create", item.ParameterSetName);
    }

    [Fact]
    public void Generated_set_wrapper_supports_multiple_parameter_sets()
    {
        using var runspace = CreateRunspace();

        var command = new SetWidgetLifecycleCommands(runspace: runspace);
        var byName = Assert.IsType<WidgetLifecycleResult>(Assert.Single(command.Set(name: "alpha", count: 4)));
        var byId = Assert.IsType<WidgetLifecycleResult>(Assert.Single(command.Set(id: 9, count: 5)));

        Assert.Equal("Set", byName.Operation);
        Assert.Equal("UpdateByName", byName.ParameterSetName);
        Assert.Equal("alpha", byName.Name);
        Assert.Null(byName.Id);
        Assert.False(byName.FlagWasBound);

        Assert.Equal("Set", byId.Operation);
        Assert.Equal("UpdateById", byId.ParameterSetName);
        Assert.Equal(9, byId.Id);
        Assert.Null(byId.Name);
    }

    [Fact]
    public void Generated_remove_wrapper_supports_switch_parameters()
    {
        using var runspace = CreateRunspace();

        var command = new RemoveWidgetLifecycleCommands(runspace: runspace);
        var result = command.Remove(name: "alpha", force: SwitchParameter.Present);

        var item = Assert.IsType<WidgetLifecycleResult>(Assert.Single(result));
        Assert.Equal("Remove", item.Operation);
        Assert.Equal("DeleteByName", item.ParameterSetName);
        Assert.Equal("alpha", item.Name);
        Assert.True(item.FlagWasBound);
    }

    private static Runspace CreateRunspace()
    {
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-Widget", typeof(GetWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PipelineWidget", typeof(GetPipelineWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PropertyPipelineWidget", typeof(GetPropertyPipelineWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-MultiTargetWidget", typeof(GetMultiTargetWidgetCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("New-WidgetLifecycle", typeof(NewWidgetLifecycleCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Set-WidgetLifecycle", typeof(SetWidgetLifecycleCommand), helpFileName: null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Remove-WidgetLifecycle", typeof(RemoveWidgetLifecycleCommand), helpFileName: null));

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

[OutputType(typeof(ParameterSetResult))]
[Cmdlet(VerbsCommon.Get, "MultiTargetWidget", DefaultParameterSetName = "ByName")]
public sealed class GetMultiTargetWidgetCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ParameterSetName = "ByName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "ById")]
    public int Id { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new ParameterSetResult(
            ParameterSetName,
            Name,
            MyInvocation.BoundParameters.ContainsKey(nameof(Id)) ? Id : null));
    }
}

public sealed record ParameterSetResult(string ParameterSetName, string? Name, int? Id);

[GeneratePowerShellWrapper(typeof(GetMultiTargetWidgetCommand))]
public partial class MultiTargetWidgetCommands
{
}

[OutputType(typeof(WidgetLifecycleResult))]
[Cmdlet(VerbsCommon.New, "WidgetLifecycle", DefaultParameterSetName = "Create")]
public sealed class NewWidgetLifecycleCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ParameterSetName = "Create")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "Create")]
    public int Count { get; set; }

    [Parameter(ParameterSetName = "Create")]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new WidgetLifecycleResult(
            Operation: "New",
            ParameterSetName,
            Name,
            Id: null,
            Count,
            FlagWasBound: MyInvocation.BoundParameters.ContainsKey(nameof(PassThru))));
    }
}

[OutputType(typeof(WidgetLifecycleResult))]
[Cmdlet(VerbsCommon.Set, "WidgetLifecycle", DefaultParameterSetName = "UpdateByName")]
public sealed class SetWidgetLifecycleCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ParameterSetName = "UpdateByName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "UpdateById")]
    public int Id { get; set; }

    [Parameter]
    public int Count { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new WidgetLifecycleResult(
            Operation: "Set",
            ParameterSetName,
            Name,
            MyInvocation.BoundParameters.ContainsKey(nameof(Id)) ? Id : null,
            Count,
            FlagWasBound: MyInvocation.BoundParameters.ContainsKey(nameof(Force))));
    }
}

[OutputType(typeof(WidgetLifecycleResult))]
[Cmdlet(VerbsCommon.Remove, "WidgetLifecycle", DefaultParameterSetName = "DeleteByName")]
public sealed class RemoveWidgetLifecycleCommand : PSCmdlet
{
    [Parameter(Mandatory = true, ParameterSetName = "DeleteByName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "DeleteById")]
    public int Id { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        WriteObject(new WidgetLifecycleResult(
            Operation: "Remove",
            ParameterSetName,
            Name,
            MyInvocation.BoundParameters.ContainsKey(nameof(Id)) ? Id : null,
            Count: 0,
            FlagWasBound: MyInvocation.BoundParameters.ContainsKey(nameof(Force))));
    }
}

public sealed record WidgetLifecycleResult(
    string Operation,
    string ParameterSetName,
    string? Name,
    int? Id,
    int Count,
    bool FlagWasBound);

[GeneratePowerShellWrapper(typeof(NewWidgetLifecycleCommand))]
public partial class NewWidgetLifecycleCommands
{
}

[GeneratePowerShellWrapper(typeof(SetWidgetLifecycleCommand))]
public partial class SetWidgetLifecycleCommands
{
}

[GeneratePowerShellWrapper(typeof(RemoveWidgetLifecycleCommand))]
public partial class RemoveWidgetLifecycleCommands
{
}

[GeneratePowerShellWrapper("Get-Date")]
public partial class DateCommands
{
}
