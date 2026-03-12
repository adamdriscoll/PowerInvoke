using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PowerInvoke.Generators;

[Generator]
public sealed class PowerShellWrapperGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PowerInvoke.GeneratePowerShellWrapperAttribute";
    private const string CmdletAttributeName = "System.Management.Automation.CmdletAttribute";
    private const string ParameterAttributeName = "System.Management.Automation.ParameterAttribute";

    private static readonly DiagnosticDescriptor PartialClassRequired = new(
        id: "PSSG001",
        title: "Wrapper target must be partial",
        messageFormat: "Class '{0}' must be declared partial to receive generated PowerShell wrappers",
        category: "PowerInvoke",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CmdletAttributeRequired = new(
        id: "PSSG002",
        title: "Cmdlet attribute not found",
        messageFormat: "Type '{0}' must be decorated with CmdletAttribute",
        category: "PowerInvoke",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CommandNotFound = new(
        id: "PSSG003",
        title: "Command could not be discovered",
        messageFormat: "Command '{0}' could not be discovered during source generation",
        category: "PowerInvoke",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CommandDiscoveryFailed = new(
        id: "PSSG004",
        title: "Command discovery failed",
        messageFormat: "Command '{0}' could not be discovered during source generation: {1}",
        category: "PowerInvoke",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly HashSet<string> ExcludedCommonParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose",
        "Debug",
        "ErrorAction",
        "WarningAction",
        "InformationAction",
        "ProgressAction",
        "ErrorVariable",
        "WarningVariable",
        "InformationVariable",
        "OutVariable",
        "OutBuffer",
        "PipelineVariable",
        "WhatIf",
        "Confirm",
        "UseTransaction"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var wrapperTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (generatorContext, _) => CreateModel(generatorContext));

        context.RegisterSourceOutput(
            wrapperTargets,
            static (productionContext, model) =>
            {
                if (model.Diagnostic is not null)
                {
                    productionContext.ReportDiagnostic(model.Diagnostic);
                    return;
                }

                if (model.Specification is not null)
                {
                    productionContext.AddSource(
                        $"{model.Specification.HintName}.g.cs",
                        model.Specification.Source);
                }
            });
    }

    private static GenerationResult CreateModel(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetType)
        {
            return GenerationResult.Empty;
        }

        if (!IsPartial(context.TargetNode))
        {
            return new GenerationResult(
                null,
                Diagnostic.Create(PartialClassRequired, context.TargetNode.GetLocation(), targetType.Name));
        }

        var attributeData = context.Attributes[0];
        var command = GetCommandSpecification(context, attributeData);
        if (command is null)
        {
            return GenerationResult.Empty;
        }

        if (command.Diagnostic is not null)
        {
            return new GenerationResult(null, command.Diagnostic);
        }

        var source = RenderSource(targetType, command.CommandName, command.MethodName, command.Parameters);
        return new GenerationResult(
            new GeneratedWrapper($"{targetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{command.MethodName}", source),
            null);
    }

    private static bool IsPartial(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration &&
               classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
    }

    private static CommandSpecification? GetCommandSpecification(
        GeneratorAttributeSyntaxContext context,
        AttributeData attributeData)
    {
        var argument = attributeData.ConstructorArguments[0];

        if (argument.Value is INamedTypeSymbol cmdletType)
        {
            return GetCommandSpecificationFromType(context, cmdletType);
        }

        if (argument.Value is string commandName)
        {
            return GetCommandSpecificationFromName(context, commandName);
        }

        return null;
    }

    private static CommandSpecification GetCommandSpecificationFromType(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol cmdletType)
    {
        var cmdletAttribute = cmdletType.GetAttributes()
            .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(
                x.AttributeClass,
                context.SemanticModel.Compilation.GetTypeByMetadataName(CmdletAttributeName)));

        if (cmdletAttribute is null)
        {
            return new CommandSpecification(
                string.Empty,
                string.Empty,
                [],
                Diagnostic.Create(CmdletAttributeRequired, context.TargetNode.GetLocation(), cmdletType.Name));
        }

        var verb = cmdletAttribute.ConstructorArguments[0].Value?.ToString() ?? "Invoke";
        var noun = cmdletAttribute.ConstructorArguments[1].Value?.ToString() ?? context.TargetSymbol.Name;
        var commandName = $"{verb}-{noun}";

        var parameters = GetParameters(cmdletType)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        return new CommandSpecification(commandName, verb, parameters, null);
    }

    private static CommandSpecification GetCommandSpecificationFromName(
        GeneratorAttributeSyntaxContext context,
        string commandName)
    {
        try
        {
            var discoveredCommand = DiscoverCommand(commandName);
            if (discoveredCommand is null)
            {
                return new CommandSpecification(
                    string.Empty,
                    string.Empty,
                    [],
                    Diagnostic.Create(
                        CommandDiscoveryFailed,
                        context.TargetNode.GetLocation(),
                        commandName,
                        "pwsh could not be found or did not return command metadata"));
            }

            if (discoveredCommand.Parameters.Count == 0 && !string.Equals(discoveredCommand.Name, commandName, StringComparison.OrdinalIgnoreCase))
            {
                return new CommandSpecification(
                    string.Empty,
                    string.Empty,
                    [],
                    Diagnostic.Create(CommandNotFound, context.TargetNode.GetLocation(), commandName));
            }

            var parameters = discoveredCommand.Parameters
                .Where(x => !ExcludedCommonParameters.Contains(x.Name))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => CreateParameterModel(context.SemanticModel.Compilation, x.Name, x.ParameterType))
                .ToList();

            return new CommandSpecification(discoveredCommand.Name, GetMethodName(discoveredCommand.Name), parameters, null);
        }
        catch (Exception exception)
        {
            return new CommandSpecification(
                string.Empty,
                string.Empty,
                [],
                Diagnostic.Create(CommandDiscoveryFailed, context.TargetNode.GetLocation(), commandName, exception.Message));
        }
    }

    private static IEnumerable<ParameterModel> GetParameters(INamedTypeSymbol cmdletType)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var current = cmdletType; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (!property.GetAttributes().Any(static attribute =>
                    attribute.AttributeClass?.ToDisplayString() == ParameterAttributeName))
                {
                    continue;
                }

                if (!seenNames.Add(property.Name))
                {
                    continue;
                }

                yield return new ParameterModel(
                    property.Name,
                    ToParameterIdentifier(property.Name),
                    GetParameterType(property.Type),
                    IsOptionalValueType(property.Type));
            }
        }
    }

    private static ParameterModel CreateParameterModel(Compilation compilation, string parameterName, Type parameterType)
    {
        var isOptionalValueType = IsOptionalValueType(parameterType);
        return new ParameterModel(
            parameterName,
            ToParameterIdentifier(parameterName),
            GetParameterType(compilation, parameterType),
            isOptionalValueType);
    }

    private static string GetParameterType(ITypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return typeName;
        }

        if (typeSymbol.IsReferenceType)
        {
            return $"{typeName}?";
        }

        return $"global::System.Nullable<{typeName}>";
    }

    private static string GetParameterType(Compilation compilation, Type type)
    {
        if (type.IsByRef)
        {
            return GetParameterType(compilation, type.GetElementType()!);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return RenderRuntimeTypeName(type);
        }

        if (type.IsValueType)
        {
            return $"global::System.Nullable<{RenderDiscoveredParameterType(compilation, type)}>";
        }

        return $"{RenderDiscoveredParameterType(compilation, type)}?";
    }

    private static bool IsOptionalValueType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.IsReferenceType)
        {
            return false;
        }

        return typeSymbol is not INamedTypeSymbol namedType ||
               namedType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    private static bool IsOptionalValueType(Type type)
    {
        if (!type.IsValueType)
        {
            return false;
        }

        return Nullable.GetUnderlyingType(type) is null;
    }

    private static string ToParameterIdentifier(string propertyName)
    {
        var builder = new StringBuilder(propertyName.Length + 1);
        builder.Append(char.ToLowerInvariant(propertyName[0]));

        if (propertyName.Length > 1)
        {
            builder.Append(propertyName, 1, propertyName.Length - 1);
        }

        var identifier = builder.ToString();
        return SyntaxFacts.GetKeywordKind(identifier) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + identifier
            : identifier;
    }

    private static string GetMethodName(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return "Invoke";
        }

        var dashIndex = commandName.IndexOf('-');
        if (dashIndex > 0)
        {
            return commandName.Substring(0, dashIndex);
        }

        var builder = new StringBuilder(commandName.Length);
        var capitalizeNext = true;

        foreach (var character in commandName)
        {
            if (!SyntaxFacts.IsIdentifierPartCharacter(character))
            {
                capitalizeNext = true;
                continue;
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        return builder.Length == 0 ? "Invoke" : builder.ToString();
    }

    private static DiscoveredCommand? DiscoverCommand(string commandName)
    {
        var pwshPath = FindPwshPath();
        if (pwshPath is null)
        {
            return null;
        }

        var escapedCommandName = EscapePowerShellSingleQuotedString(commandName);
        var script = $@"
$ErrorActionPreference = 'Stop'
$command = Get-Command -Name '{escapedCommandName}' | Select-Object -First 1
if ($null -eq $command) {{ exit 3 }}
if ($command.CommandType -eq 'Alias') {{ $command = $command.ResolvedCommand }}
[Console]::Out.WriteLine($command.Name)
foreach ($parameter in $command.Parameters.Values | Sort-Object Name) {{
    $parameterType = if ($null -ne $parameter.ParameterType) {{ $parameter.ParameterType.AssemblyQualifiedName }} else {{ [object].AssemblyQualifiedName }}
    [Console]::Out.WriteLine(($parameter.Name + '=' + $parameterType))
}}
";

        var startInfo = new ProcessStartInfo
        {
            FileName = pwshPath,
            Arguments = $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {EncodePowerShellScript(script)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        if (process.ExitCode == 3)
        {
            return new DiscoveredCommand(commandName, []);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(standardError) ? "pwsh command discovery failed" : standardError.Trim());
        }

        var lines = standardOutput
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var discoveredName = lines[0];
        var parameters = new List<DiscoveredParameter>();
        foreach (var line in lines.Skip(1))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            var name = line.Substring(0, separatorIndex);
            var assemblyQualifiedTypeName = line.Substring(separatorIndex + 1);
            var parameterType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false) ?? typeof(object);
            parameters.Add(new DiscoveredParameter(name, parameterType));
        }

        return new DiscoveredCommand(discoveredName, parameters);
    }

    private static string? FindPwshPath()
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Where(static x => !string.IsNullOrWhiteSpace(x));

        foreach (var pathEntry in pathEntries)
        {
            var pwshPath = Path.Combine(pathEntry.Trim(), "pwsh.exe");
            if (File.Exists(pwshPath))
            {
                return pwshPath;
            }
        }

        var commonLocation = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell",
            "7",
            "pwsh.exe");
        if (File.Exists(commonLocation))
        {
            return commonLocation;
        }

        return null;
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

    private static string EncodePowerShellScript(string value)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(value));
    }

    private static string RenderDiscoveredParameterType(Compilation compilation, Type type)
    {
        if (CanResolveType(compilation, type))
        {
            return RenderRuntimeTypeName(type);
        }

        return "global::System.Object";
    }

    private static bool CanResolveType(Compilation compilation, Type type)
    {
        if (type.IsByRef)
        {
            return CanResolveType(compilation, type.GetElementType()!);
        }

        if (type.IsArray)
        {
            return CanResolveType(compilation, type.GetElementType()!);
        }

        if (type.IsPointer)
        {
            return CanResolveType(compilation, type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            if (!CanResolveType(compilation, type.GetGenericTypeDefinition()))
            {
                return false;
            }

            return type.GetGenericArguments().All(x => CanResolveType(compilation, x));
        }

        if (type.FullName is null)
        {
            return false;
        }

        return compilation.GetTypeByMetadataName(type.FullName) is not null;
    }

    private static string RenderRuntimeTypeName(Type type)
    {
        if (type.IsByRef)
        {
            return RenderRuntimeTypeName(type.GetElementType()!);
        }

        if (type.IsArray)
        {
            return $"{RenderRuntimeTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsPointer)
        {
            return $"{RenderRuntimeTypeName(type.GetElementType()!)}*";
        }

        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            var genericArguments = type.GetGenericArguments();
            var genericTypeName = genericDefinition.FullName ?? genericDefinition.Name;
            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                genericTypeName = genericTypeName.Substring(0, backtickIndex);
            }

            return $"global::{genericTypeName.Replace('+', '.') }<{string.Join(", ", genericArguments.Select(RenderRuntimeTypeName))}>";
        }

        return $"global::{(type.FullName ?? type.Name).Replace('+', '.')}";
    }

    private static string RenderSource(
        INamedTypeSymbol targetType,
        string commandName,
        string methodName,
        IReadOnlyList<ParameterModel> parameters)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (!targetType.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(targetType.ContainingNamespace.ToDisplayString()).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").Append(targetType.Name).AppendLine();
        builder.AppendLine("{");
        builder.Append("    private readonly global::System.Management.Automation.PowerShell _powerShell;").AppendLine();
        builder.AppendLine();
        builder.Append("    public ").Append(targetType.Name)
            .Append("(global::System.Management.Automation.PowerShell powerShell)").AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("        _powerShell = powerShell ?? throw new global::System.ArgumentNullException(nameof(powerShell));");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public global::System.Collections.ObjectModel.Collection<global::System.Management.Automation.PSObject> ")
            .Append(methodName)
            .Append("(");

        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(parameter.TypeName)
                .Append(' ')
                .Append(parameter.Identifier)
                .Append(" = default");
        }

        builder.AppendLine(")");
        builder.AppendLine("    {");
        builder.AppendLine("        var parameters = new global::System.Collections.Generic.List<global::PowerInvoke.PowerShellParameter>();");

        foreach (var parameter in parameters)
        {
            if (parameter.IsOptionalValueType)
            {
                builder.Append("        if (").Append(parameter.Identifier).AppendLine(".HasValue)");
                builder.AppendLine("        {");
                builder.Append("            parameters.Add(new global::PowerInvoke.PowerShellParameter(\"")
                    .Append(parameter.Name)
                    .Append("\", ")
                    .Append(parameter.Identifier)
                    .AppendLine(".Value));");
                builder.AppendLine("        }");
            }
            else
            {
                builder.Append("        if (").Append(parameter.Identifier).AppendLine(" is not null)");
                builder.AppendLine("        {");
                builder.Append("            parameters.Add(new global::PowerInvoke.PowerShellParameter(\"")
                    .Append(parameter.Name)
                    .Append("\", ")
                    .Append(parameter.Identifier)
                    .AppendLine("));");
                builder.AppendLine("        }");
            }
        }

        builder.Append("        return global::PowerInvoke.PowerShellCommandInvoker.Invoke(_powerShell, \"")
            .Append(commandName)
            .AppendLine("\", parameters);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private sealed class ParameterModel
    {
        public ParameterModel(string name, string identifier, string typeName, bool isOptionalValueType)
        {
            Name = name;
            Identifier = identifier;
            TypeName = typeName;
            IsOptionalValueType = isOptionalValueType;
        }

        public string Name { get; }

        public string Identifier { get; }

        public string TypeName { get; }

        public bool IsOptionalValueType { get; }
    }

    private sealed class GeneratedWrapper
    {
        public GeneratedWrapper(string hintName, string source)
        {
            HintName = hintName;
            Source = source;
        }

        public string HintName { get; }

        public string Source { get; }
    }

    private sealed class CommandSpecification
    {
        public CommandSpecification(
            string commandName,
            string methodName,
            IReadOnlyList<ParameterModel> parameters,
            Diagnostic? diagnostic)
        {
            CommandName = commandName;
            MethodName = methodName;
            Parameters = parameters;
            Diagnostic = diagnostic;
        }

        public string CommandName { get; }

        public string MethodName { get; }

        public IReadOnlyList<ParameterModel> Parameters { get; }

        public Diagnostic? Diagnostic { get; }
    }

    private sealed class DiscoveredCommand
    {
        public DiscoveredCommand(string name, IReadOnlyList<DiscoveredParameter> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }

        public IReadOnlyList<DiscoveredParameter> Parameters { get; }
    }

    private sealed class DiscoveredParameter
    {
        public DiscoveredParameter(string name, Type parameterType)
        {
            Name = name;
            ParameterType = parameterType;
        }

        public string Name { get; }

        public Type ParameterType { get; }
    }

    private sealed class GenerationResult
    {
        public static GenerationResult Empty { get; } = new GenerationResult(null, null);

        public GenerationResult(GeneratedWrapper? specification, Diagnostic? diagnostic)
        {
            Specification = specification;
            Diagnostic = diagnostic;
        }

        public GeneratedWrapper? Specification { get; }

        public Diagnostic? Diagnostic { get; }
    }
}
