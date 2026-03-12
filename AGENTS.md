# AGENTS.md

## Purpose
PowerInvoke is a .NET library for calling PowerShell in a strongly typed, low-friction way.

The main product goal is:
- make PowerShell easy to use from .NET
- hide as much of the PowerShell SDK surface area as practical
- present an API that feels like normal .NET code, not like hosting a PowerShell module from C#

When making changes, optimize for a consumer experience where a .NET developer can stay in typed C# concepts for as long as possible and only touch PowerShell-specific types at the edges.

## Current shape of the repo
- `src/PowerInvoke` contains the public runtime library
- `src/PowerInvoke.Generators` contains the Roslyn source generator that creates typed wrappers
- `tests/PowerInvoke.Tests` contains xUnit tests for generated wrapper behavior
- `PowerInvoke.slnx` is the solution entry point

## Design priorities
1. Keep the public API small and .NET-first.
2. Prefer typed wrappers, typed parameters, and conventional C# naming over PowerShell idioms.
3. Expose `System.Management.Automation` only when there is a clear technical need.
4. Treat PowerShell as an implementation detail behind a friendly .NET abstraction.
5. Favor predictable behavior over trying to mirror every PowerShell capability.

## What good changes look like
- Adding or refining .NET-friendly abstractions over raw PowerShell SDK concepts
- Improving generated APIs so they read like ordinary C# client code
- Making nullability, optional parameters, and defaults feel natural in C#
- Adding tests around the observable behavior of generated wrappers
- Keeping the runtime layer simple and focused on orchestration

## What to avoid
- Designing APIs around PowerShell jargon when a .NET term would do
- Forcing consumers to build `Command`, `PSCommand`, `PSObject`, or other SDK-heavy constructs directly
- Leaking runspace or pipeline management details through the main public surface unless required
- Expanding the public surface area just because the PowerShell SDK supports it
- Introducing module-style ergonomics when a library-style API would be clearer

## API guidance
- Prefer classes, methods, records, and options objects that look natural to .NET developers.
- Keep public names descriptive and C#-idiomatic.
- Use nullable reference types intentionally and make optional arguments explicit.
- If a PowerShell type must appear publicly, check whether a library-owned abstraction can wrap it first.
- Make the simplest path the most obvious one. Advanced behavior should not complicate the common path.

## Generator guidance
- Generated code should be easy to read in emitted form.
- Generated method names should feel like normal .NET methods, even when derived from cmdlet names.
- Parameters should preserve strong typing and avoid unnecessary dynamic behavior.
- Prefer compile-time diagnostics when misuse can be detected during generation.
- Keep generator output stable so small source changes do not cause noisy generated diffs.

## Testing guidance
- Prefer tests that verify user-visible behavior rather than implementation details.
- For generator-backed features, test the generated wrapper from the consumer point of view.
- Cover optional parameters, null handling, command naming, and binding behavior.
- Add regression tests for any bug fix in wrapper generation or invocation behavior.

## Build and test
Run from the repo root:

```powershell
dotnet build PowerInvoke.slnx
dotnet test PowerInvoke.slnx
```

## Agent workflow
Before changing code:
- read the relevant files in `src/` and `tests/`
- understand whether the change belongs in the runtime library, the generator, or both
- preserve the .NET-first design goal above all else

When proposing a public API change:
- explain how it reduces PowerShell SDK exposure
- explain why it feels more like a .NET library
- add or update tests that demonstrate the intended consumer experience

## Decision rule
If you are choosing between:
- a design that is closer to raw PowerShell
- and a design that is slightly more opinionated but feels natural in C#

choose the C#-first design unless there is a strong compatibility reason not to.
