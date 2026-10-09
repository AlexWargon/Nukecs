# Generator batch regressions

Run after Unity has compiled the updated Nukecs runtime assembly:

```powershell
dotnet run --project SourceGen/Tests~/BatchDiagnostics.csproj -- "D:/Unity/Unity/6000.0.63f1/Editor/Data" "D:/Unity/NukecsSandbox/NukecsSandbox"
```

When Unity has not recompiled the runtime yet, pass a freshly built
`Nukecs.dll` as a third argument (for example `obj/Debug/Nukecs.dll` after
`dotnet build Nukecs.csproj`; run `dotnet build-server shutdown` after replacing
the analyzer DLL so the compiler server does not reuse the old generator).
`NUKECS_DUMP=<case>` prints the generated sources of one case.

Requires .NET SDK 8 and the checked-in generator DLL. Uses SDK Roslyn and Unity
reference assemblies without running gameplay or importing invalid scripts into
Unity. Verifies contextual Single/Parallel generation compiles, RequireBatch
errors for unsupported bodies, class-scope binding of system bodies (nested
partial jobs, private helpers, constants, nested types, user types shadowing
framework names, non-partial fallback, NUKECS010/013) and Any<> generation and
diagnostics (NUKECS020/021). Unity ignores the Tests~ directory.
