# PowerInvoke

PowerInvoke is a .NET-first library for calling PowerShell through strongly typed wrappers instead of building `PSCommand` pipelines by hand.

For now, the project documentation lives in this README.

## Status

The package metadata and publish pipeline are set up for NuGet publishing, but the package has not been published yet.

Preliminary install command:

```powershell
dotnet add package PowerInvoke
```

## What it does

- Keeps the public API centered on regular C# types.
- Uses a source generator to create typed wrapper methods.
- Lets you stay in .NET concepts until you intentionally cross into PowerShell hosting.
- Supports PowerShell pipeline input through generated `IEnumerable<T>` overloads when a cmdlet accepts pipeline-bound parameters.

## Getting started

1. Add the package to your project.
2. Create or obtain a `System.Management.Automation.PowerShell` instance.
3. Mark a partial class with `GeneratePowerShellWrapperAttribute`.
4. Call the generated wrapper methods like regular C# methods.

```csharp
using System;
using System.Management.Automation;
using PowerInvoke;

[GeneratePowerShellWrapper("Get-Date")]
public partial class DateCommands
{
}

using var powerShell = PowerShell.Create();
var commands = new DateCommands(powerShell);
var result = commands.Get(date: DateTime.UtcNow);
```

When the wrapper is generated from a cmdlet type, the method name and parameters are inferred from the cmdlet metadata:

```csharp
using System.Management.Automation;
using PowerInvoke;

[Cmdlet(VerbsCommon.Get, "Widget")]
public sealed class GetWidgetCommand : PSCmdlet
{
    [Parameter]
    public string? Name { get; set; }
}

[GeneratePowerShellWrapper(typeof(GetWidgetCommand))]
public partial class WidgetCommands
{
}
```

## Pipeline input

If a cmdlet parameter is marked with `ValueFromPipeline = true` or `ValueFromPipelineByPropertyName = true`, PowerInvoke generates an overload that accepts `IEnumerable<TInput>` so you can pass pipeline input as a normal .NET sequence.

Pipeline-by-value example:

```csharp
using System.Management.Automation;
using PowerInvoke;

[Cmdlet(VerbsCommon.Get, "PipelineWidget")]
public sealed class GetPipelineWidgetCommand : PSCmdlet
{
    [Parameter(ValueFromPipeline = true)]
    public string? Name { get; set; }

    [Parameter]
    public int Count { get; set; }
}

[GeneratePowerShellWrapper(typeof(GetPipelineWidgetCommand))]
public partial class PipelineWidgetCommands
{
}

var commands = new PipelineWidgetCommands(powerShell);
var results = commands.Get(new[] { "alpha", "beta" }, count: 2);
```

Pipeline-by-property-name example:

```csharp
using System.Management.Automation;
using PowerInvoke;

public sealed record WidgetInput(string Name);

[Cmdlet(VerbsCommon.Get, "PropertyPipelineWidget")]
public sealed class GetPropertyPipelineWidgetCommand : PSCmdlet
{
    [Parameter(ValueFromPipelineByPropertyName = true)]
    public string? Name { get; set; }
}

[GeneratePowerShellWrapper(typeof(GetPropertyPipelineWidgetCommand))]
public partial class PropertyPipelineWidgetCommands
{
}

var commands = new PropertyPipelineWidgetCommands(powerShell);
var results = commands.Get(new[]
{
    new WidgetInput("alpha"),
    new WidgetInput("beta")
});
```

Generated wrappers also expose pipeline metadata through `PipelineParameters`, which can be useful for diagnostics, tooling, or documentation:

```csharp
foreach (var parameter in PipelineWidgetCommands.PipelineParameters)
{
    Console.WriteLine($"{parameter.Name}: value={parameter.AcceptsValue}, propertyName={parameter.AcceptsPropertyName}");
}
```

If a command does not accept pipeline input, no pipeline overload is generated and `PipelineParameters` is empty.

## Local development

Build the solution:

```powershell
dotnet build PowerInvoke.slnx
```

Run tests:

```powershell
dotnet test PowerInvoke.slnx
```

Create a local package:

```powershell
dotnet pack src/PowerInvoke/PowerInvoke.csproj -c Release -o artifacts/packages
```

## CI and publishing

- CI runs on pushes and pull requests and validates restore, build, test, and package creation.
- Publishing is a separate manually triggered GitHub Actions workflow.
- The publish workflow expects a `NUGET_API_KEY` repository secret.

## Notes

- The `PowerInvoke` package includes both the runtime library and the source generator.
- Generated wrappers are intended to feel like normal .NET methods, not direct PowerShell hosting code.
