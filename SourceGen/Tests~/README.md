# Generator batch regressions

Run after Unity has compiled the updated Nukecs runtime assembly:

```powershell
dotnet run --project SourceGen/Tests~/BatchDiagnostics.csproj -- "D:/Unity/Unity/6000.0.63f1/Editor/Data" "D:/Unity/NukecsSandbox/NukecsSandbox"
```

Requires .NET SDK 8 and the checked-in generator DLL. Uses SDK Roslyn and Unity
reference assemblies without running gameplay or importing invalid scripts into
Unity. Verifies contextual Single/Parallel generation compiles and RequireBatch
errors for unsupported bodies. Unity ignores the Tests~ directory.
