# Nukecs without Unity

Unity ignores this folder (its name ends in `~`).

- `Nukecs/Nukecs.csproj` builds the engine-independent core (`src/` without `src/Unity/`)
  for netstandard2.1 with the checked-in generator `SourceGen/NUKECSGEN.dll`, without any
  Unity reference. Burst API members come from `src/Platform/BurstShim.cs`, jobs run on
  `NJobScheduler`.
- `Nukecs.Tests/Nukecs.Tests.csproj` (net8, NUnit) runs the Unity tests from `UnitTests/` that
  need nothing from Unity, plus `SchedulerTests.cs`. The assembly is named `Nukecs.Tests`
  like the Unity test assembly, so it sees the core internals. Unity-dependent test files are
  listed in its `Exclude`; a new core test in `UnitTests/` is picked up automatically, so
  keep core tests free of `UnityEngine`, `Unity.Mathematics` and Unity jobs (use `Mem` and
  `AllocatorHandle`).

```powershell
dotnet test "DotNet~/Nukecs.Tests/Nukecs.Tests.csproj"
```

`NUKECS_WORKERS=n` runs the tests with `n` scheduler workers (default: processor count - 1;
`0` runs each job on the thread that makes it ready). Run at least the default and `0`
after scheduler changes. Native Burst probes are ignored here, as Burst is Unity-only.
After generator changes rebuild and copy `NUKECSGEN.dll` first (see `SourceGen/Tests~`).
