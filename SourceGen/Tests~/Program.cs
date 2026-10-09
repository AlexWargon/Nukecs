using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wargon.Nukecs.NUKECSGEN;

if (args.Length < 2) throw new ArgumentException("Pass Unity Editor Data directory, Unity project directory and optionally a Nukecs.dll override.");
// Optional third argument: a freshly built Nukecs.dll (e.g. obj/Debug/Nukecs.dll from
// `dotnet build Nukecs.csproj`) when Unity has not recompiled the runtime yet.
var nukecsOverride = args.Length > 2 ? args[2] : null;
var references = Directory.GetFiles(Path.Combine(args[0], "NetStandard", "ref", "2.1.0"), "*.dll")
    .Concat(Directory.GetFiles(Path.Combine(args[0], "Managed", "UnityEngine"), "*.dll"))
    .Concat(Directory.GetFiles(Path.Combine(args[1], "Library", "ScriptAssemblies"), "*.dll")
        .Where(p => (nukecsOverride == null && Path.GetFileName(p) == "Nukecs.dll") || Path.GetFileName(p).StartsWith("Unity.")))
    .Concat(nukecsOverride != null ? new[] { nukecsOverride } : Array.Empty<string>())
    .Distinct().Select(p => MetadataReference.CreateFromFile(p)).ToArray();
var parse = new CSharpParseOptions(LanguageVersion.CSharp9);

(Compilation Output, Diagnostic[] Diagnostics) Run(string source)
{
    var compilation = CSharpCompilation.Create("Nukecs.Tests", new[] { CSharpSyntaxTree.ParseText(source, parse, path: "BatchProbe.cs") }, references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new SrcGen().AsSourceGenerator() }, parseOptions: parse);
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
    return (output, output.GetDiagnostics().Concat(diagnostics).ToArray());
}

string Generated(Compilation output) => string.Join("\n", output.SyntaxTrees.Skip(1).Select(t => t.ToString()));

void ExpectClean(string name, (Compilation Output, Diagnostic[] Diagnostics) result, bool allowNonPartialWarning = false)
{
    var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length > 0) throw new Exception(name + ": " + string.Join("\n", errors.Select(d => d.ToString())));
    if (!allowNonPartialWarning && result.Diagnostics.Any(d => d.Id == "NUKECS012"))
        throw new Exception(name + ": unexpected NUKECS012 for a partial class");
}

var passed = 0;
var cases = new (string Name, string Query, string Body, string Reason)[] {
    ("Envelope", "Query<Entity, ProbeValue>", "unsafe { var dt = state.Time.DeltaTime; var count = 0; foreach (var (entity, value) in query) { value.Get.Value += dt; count++; } state.Time.DeltaTime = count; }", null),
    ("Guard", "Query<ProbeValue>", "if (state.Time.DeltaTime <= 0) return; var dt = state.Time.DeltaTime; if (dt > 0) { foreach (ref var value in query) value.Value += dt; }", null),
    ("Pool", "Query<ProbePool>", "foreach (ref var value in query) value.Value++;", "PoolComponent"),
    ("Explicit", "Query<ProbeValue>", "foreach (var value in query.iter()) value.C0.Get.Value++;", "ExplicitRuntimeIterator"),
    ("Return", "Query<ProbeValue>", "foreach (ref var value in query) { if (value.Value < 0) return; value.Value++; }", "UnsupportedControlFlow"),
    ("Multiple", "Query<ProbeValue>", "foreach (ref var value in query) value.Value++; foreach (ref var value in query) value.Value++;", "MultipleQueryLoops"),
    ("LocalFunction", "Query<ProbeValue>", "float Scale(float x) => x * 2; foreach (ref var value in query) value.Value = Scale(value.Value);", "LocalFunction"),
    ("RefCapture", "Query<ProbeValue>", "ref var dt = ref state.Time.DeltaTime; foreach (ref var value in query) value.Value += dt;", "UnsupportedCapture"),
    ("Nested", "Query<ProbeValue>", "foreach (ref var value in query) { foreach (ref var other in query) value.Value += other.Value; }", "MultipleQueryLoops"),
    ("ConstCapture", "Query<ProbeValue>", "const float dt = 1; foreach (ref var value in query) value.Value += dt;", "UnsupportedCapture"),
    ("ReservedCapture", "Query<ProbeValue>", "var _dt = 1f; foreach (ref var value in query) value.Value += _dt;", "UnsupportedCapture"),
    ("EnclosingWhile", "Query<ProbeValue>", "while (state.Time.DeltaTime > 0) { foreach (ref var value in query) value.Value++; }", "UnsupportedControlFlow"),
    ("ValueCopy", "Query<ProbeValue>", "foreach (var value in query) { var copy = value; copy.Value++; }", "UnsupportedPattern"),
    ("MissingLoop", "Query<ProbeValue>", "state.Time.DeltaTime = 0;", "NoQueryLoop"),
    ("MissingQuery", "", "state.Time.DeltaTime = 0;", "NoQuery"),
    ("ExpressionBody", "Query<ProbeValue>", "=> state.Time.DeltaTime = 0;", "NoBody"),
};
foreach (var test in cases)
{
  foreach (var requireBatch in new[] { true, false })
  {
    var source = "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " +
        "public struct ProbeValue : IComponent { public float Value; } public struct ProbePool : IPoolComponent { public float Value; } " +
        "public static partial class ProbeSystems { [System, BurstCompile" + (requireBatch ? ", RequireBatch" : "") + "] public static void " + test.Name +
        "(" + (test.Query.Length == 0 ? "" : "ref " + test.Query + " query, ") + "ref State state) " +
        (test.Body.StartsWith("=>") ? test.Body : "{ " + test.Body + " }") + " } }";
    var (output, diagnostics) = Run(source);
    var required = diagnostics.Where(d => d.Id == "NUKECS002").ToArray();
    if (test.Reason == null)
    {
        ExpectClean(test.Name, (output, diagnostics));
        var generated = Generated(output);
        if (!generated.Contains("SystemCompilationKind.PointerBatch") || !generated.Contains("BatchDispatchParallel("))
            throw new Exception(test.Name + ": missing contextual batch metadata/parallel dispatch");
    }
    else
    {
        if (requireBatch)
        {
            if (required.Length != 1 || required[0].Severity != DiagnosticSeverity.Error || !required[0].GetMessage().Contains(test.Reason))
                throw new Exception(test.Name + ": expected NUKECS002 / " + test.Reason + ", got " + string.Join("\n", diagnostics.Select(d => d.ToString())));
            var diagnostic = required[0];
            if (diagnostic.GetMessage().Length < 140 || diagnostic.Location.GetLineSpan().Path != "BatchProbe.cs")
                throw new Exception(test.Name + ": missing detail or source location");
            var fragment = source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
            var expected = test.Name switch {
                "Pool" => "query", "Explicit" => "query.iter()", "Return" => "return;",
                "LocalFunction" => "float Scale", "RefCapture" => "dt = ref",
                "ConstCapture" => "dt = 1", "ReservedCapture" => "_dt = 1f",
                "EnclosingWhile" => "while", "ValueCopy" => "foreach", _ => null
            };
            if (expected != null && !fragment.Contains(expected))
                throw new Exception(test.Name + ": diagnostic points to wrong source: " + fragment);
        }
        else if (required.Length != 0) throw new Exception(test.Name + ": RequireBatch error without attribute");
        var unexpected = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "NUKECS002").ToArray();
        if (unexpected.Length > 0) throw new Exception(test.Name + ": " + string.Join("\n", unexpected.Select(d => d.ToString())));
        var generated = Generated(output);
        if (!generated.Contains("BatchFallbackReason." + test.Reason) || !generated.Contains("BatchProbe.cs"))
            throw new Exception(test.Name + ": fallback runner lost reason/location");
    }
    Console.WriteLine("PASS " + test.Name + (requireBatch ? " [RequireBatch]" : " automatic"));
    passed++;
  }
}

// ---------------- class scope of the system body ----------------
const string Components = "public struct ProbeValue : IComponent { public float Value; } public struct ProbeTag : IComponent { } " +
                          "public struct ProbeA : IComponent { public int V; } public struct ProbeB : IComponent { public int V; } " +
                          "public struct ProbeC : IComponent { public int V; } public struct ProbeD : IComponent { public int V; } ";

void ScopeCase(string name, string source, Action<Compilation, Diagnostic[]> check)
{
    var (output, diagnostics) = Run(source);
    // NUKECS_DUMP=<case name> prints the generated sources of that case.
    if (Environment.GetEnvironmentVariable("NUKECS_DUMP") == name)
        foreach (var t in output.SyntaxTrees.Skip(1)) Console.WriteLine("=== " + t.FilePath + Environment.NewLine + t);
    check(output, diagnostics);
    Console.WriteLine("PASS " + name);
    passed++;
}

void ExpectPointerBatch(string name, Compilation output, int systems = 1)
{
    var generated = Generated(output);
    var count = generated.Split("SystemCompilationKind.PointerBatch").Length - 1;
    if (count != systems) throw new Exception(name + $": expected {systems} PointerBatch runner(s), got {count}");
    if (generated.Contains("BatchFallbackReason.") && !generated.Contains("BatchFallbackReason.None"))
        throw new Exception(name + ": unexpected fallback");
}

// Short calls to public/private helpers, a class constant, a nested type and a user type whose
// name also exists in Wargon.Nukecs (Range): the body must bind exactly as in the class.
ScopeCase("ClassScope", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public struct Range { public float Lo; } " +
    "public static partial class ProbeSystems { " +
    "  public const float Step = 2f; " +
    "  public struct Scale { public float K; } " +
    "  public static float PublicHelper(float v) => v + Step; " +
    "  static float PrivateHelper(float v) => v * 2; " +
    "  [System, BurstCompile, RequireBatch] public static void Plan(ref Query<ProbeValue> query, ref State state) { " +
    "    var r = new Range { Lo = Step }; var s = new Scale { K = PrivateHelper(r.Lo) }; " +
    "    foreach (ref var value in query) value.Value = PublicHelper(value.Value) * s.K + PrivateHelper(Step); } } }",
    (output, diagnostics) =>
    {
        ExpectClean("ClassScope", (output, diagnostics));
        ExpectPointerBatch("ClassScope", output);
        var generated = Generated(output);
        if (!generated.Contains("partial class ProbeSystems") || !generated.Contains("struct __Plan_Job"))
            throw new Exception("ClassScope: job is not nested in the partial class");
        if (!generated.Contains("#line ") || !generated.Contains("\"BatchProbe.cs\""))
            throw new Exception("ClassScope: #line mapping to the source file is missing");
        if (!generated.Contains("Name => \"ProbeSystems_Plan\""))
            throw new Exception("ClassScope: runner Name is not the stable Class_Method name");
    });

// Systems in every thread mode share one job; Main/MainRun/Single/Parallel all compile.
ScopeCase("ThreadModes", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class ProbeSystems { static float Helper(float v) => v + 1; " +
    "  [System, BurstCompile, RequireBatch] public static void Move(ref Query<ProbeValue> query, ref State state) { foreach (ref var value in query) value.Value = Helper(value.Value); } } " +
    "public static class Boot { public static void Register(Systems systems) { " +
    "  systems.Add(ProbeSystems.Move, Threads.Main).Add(ProbeSystems.Move, Threads.MainRun).Add(ProbeSystems.Move, Threads.Single).Add(ProbeSystems.Move, Threads.Parallel); } } }",
    (output, diagnostics) =>
    {
        ExpectClean("ThreadModes", (output, diagnostics));
        if (!Generated(output).Contains("new global::Probe.ProbeSystems.__Move_Job()"))
            throw new Exception("ThreadModes: Systems.Add does not instantiate the nested job");
    });

// Nested declaring classes: every enclosing class is partial; the body reaches the outer
// class's private constant by short name.
ScopeCase("NestedClass", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class Outer { const float Bonus = 3; " +
    "  internal static partial class Inner { " +
    "    [System, BurstCompile, RequireBatch] public static void Tick(ref Query<ProbeValue> query, ref State state) { foreach (ref var value in query) value.Value += Bonus; } } } }",
    (output, diagnostics) =>
    {
        ExpectClean("NestedClass", (output, diagnostics));
        ExpectPointerBatch("NestedClass", output);
        if (!Generated(output).Contains("global::Probe.Outer.Inner.__Tick_Job"))
            throw new Exception("NestedClass: runner does not reference the nested job");
    });

// Class without namespace.
ScopeCase("GlobalNamespace", "using Wargon.Nukecs; using Unity.Burst; " +
    "public struct GlobalValue : IComponent { public float Value; } " +
    "public static partial class GlobalSystems { static float Helper(float v) => v + 1; " +
    "  [System, BurstCompile, RequireBatch] public static void Tick(ref Query<GlobalValue> query, ref State state) { foreach (ref var value in query) value.Value = Helper(value.Value); } }",
    (output, diagnostics) =>
    {
        ExpectClean("GlobalNamespace", (output, diagnostics));
        ExpectPointerBatch("GlobalNamespace", output);
    });

// Changed<T> pipeline is emitted into the user's scope too (qualified Unity/framework names).
ScopeCase("ChangedScope", "using Wargon.Nukecs; using Wargon.Nukecs.Reactivity; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class ProbeSystems { static float Helper(float v) => v + 1; " +
    "  [System, BurstCompile] public static void React(ref Query<ProbeValue, Changed<ProbeValue>> query, ref State state) { foreach (ref var value in query) value.Value = Helper(value.Value); } } }",
    (output, diagnostics) =>
    {
        ExpectClean("ChangedScope", (output, diagnostics));
        if (!Generated(output).Contains("SystemCompilationKind.ChangedBatch"))
            throw new Exception("ChangedScope: expected ChangedBatch");
    });

// Not partial: warning, using static fallback, public helpers by short name still compile.
ScopeCase("NonPartialFallback", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static class LegacySystems { public static float Helper(float v) => v + 1; " +
    "  [System, BurstCompile, RequireBatch] public static void Tick(ref Query<ProbeValue> query, ref State state) { foreach (ref var value in query) value.Value = Helper(value.Value); } } }",
    (output, diagnostics) =>
    {
        ExpectClean("NonPartialFallback", (output, diagnostics), allowNonPartialWarning: true);
        var warning = diagnostics.Where(d => d.Id == "NUKECS012").ToArray();
        if (warning.Length != 1 || warning[0].Severity != DiagnosticSeverity.Warning)
            throw new Exception("NonPartialFallback: expected one NUKECS012 warning");
        ExpectPointerBatch("NonPartialFallback", output);
        if (!Generated(output).Contains("using static global::Probe.LegacySystems;"))
            throw new Exception("NonPartialFallback: missing using static");
    });

void ExpectError(string name, string id, string source, string messagePart = null)
{
    ScopeCase(name, source, (output, diagnostics) =>
    {
        var hits = diagnostics.Where(d => d.Id == id && d.Severity == DiagnosticSeverity.Error).ToArray();
        if (hits.Length == 0) throw new Exception(name + $": expected {id}, got " + string.Join("\n", diagnostics.Select(d => d.ToString())));
        if (messagePart != null && !hits[0].GetMessage().Contains(messagePart))
            throw new Exception(name + ": diagnostic message lacks " + messagePart + ": " + hits[0].GetMessage());
        if (hits[0].Location.GetLineSpan().Path != "BatchProbe.cs") throw new Exception(name + ": diagnostic without source location");
    });
}

ExpectError("GenericClass", "NUKECS010", "using Wargon.Nukecs; namespace Probe { " + Components +
    "public static partial class Generic<T> { [System] public static void Tick(ref Query<ProbeValue> query) { foreach (ref var value in query) value.Value++; } } }");
ExpectError("JobNameConflict", "NUKECS013", "using Wargon.Nukecs; namespace Probe { " + Components +
    "public static partial class ProbeSystems { public static int __Tick_Job; [System] public static void Tick(ref Query<ProbeValue> query) { foreach (ref var value in query) value.Value++; } } }",
    "__Tick_Job");

// ---------------- Any<> ----------------
ScopeCase("AnyBatch", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class ProbeSystems { " +
    "  [System, BurstCompile, RequireBatch] public static void Pick(ref Query<ProbeA, (Any<ProbeB, ProbeC>, None<ProbeD>)> query, ref State state) { foreach (ref var a in query) a.V++; } } }",
    (output, diagnostics) =>
    {
        ExpectClean("AnyBatch", (output, diagnostics));
        ExpectPointerBatch("AnyBatch", output);
        var generated = Generated(output);
        if (!generated.Contains("ComponentType<global::Probe.ProbeA>.Index"))
            throw new Exception("AnyBatch: ProbeA access missing from DependencyInfo");
        var components = generated.Substring(generated.IndexOf("Components = ", StringComparison.Ordinal));
        components = components.Substring(0, components.IndexOf('\n'));
        if (components.Contains("ProbeB") || components.Contains("ProbeC"))
            throw new Exception("AnyBatch: Any types leaked into DependencyInfo: " + components);
    });
ExpectError("AnyNoneConflict", "NUKECS020", "using Wargon.Nukecs; namespace Probe { " + Components +
    "public static partial class ProbeSystems { [System] public static void Pick(ref Query<ProbeA, (Any<ProbeB, ProbeC>, None<ProbeC>)> query) { foreach (ref var a in query) a.V++; } } }",
    "ProbeC");
ExpectError("AnyRedundantComponent", "NUKECS021", "using Wargon.Nukecs; namespace Probe { " + Components +
    "public static partial class ProbeSystems { [System] public static void Pick(ref Query<ProbeA, Any<ProbeA, ProbeB>> query) { foreach (ref var a in query) a.V++; } } }",
    "ProbeA");
ExpectError("AnyRedundantWith", "NUKECS021", "using Wargon.Nukecs; namespace Probe { " + Components +
    "public static partial class ProbeSystems { [System] public static void Pick(ref Query<ProbeA, (With<ProbeB>, Any<ProbeB, ProbeC>)> query) { foreach (ref var a in query) a.V++; } } }",
    "ProbeB");

// ---------------- reserved generated names ----------------
// User locals named state/range (no State parameter) must not collide with the generated
// OnUpdateBatched/OnUpdateBatchedParallel parameters; captured ones are forwarded.
ScopeCase("LocalStateAndRange", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class ProbeSystems { " +
    "  [System, BurstCompile, RequireBatch] public static void Tick(ref Query<ProbeValue> query) { " +
    "    var state = 2f; var range = 3f; foreach (ref var value in query) value.Value += state * range; } } }",
    (output, diagnostics) =>
    {
        ExpectClean("LocalStateAndRange", (output, diagnostics));
        ExpectPointerBatch("LocalStateAndRange", output);
    });
// A State parameter with a custom name is visible by that name inside the walkers.
ScopeCase("CustomStateName", "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
    "public static partial class ProbeSystems { " +
    "  [System, BurstCompile, RequireBatch] public static void Tick(ref Query<ProbeValue> query, ref State st) { " +
    "    foreach (ref var value in query) value.Value += st.Time.DeltaTime; } } }",
    (output, diagnostics) =>
    {
        ExpectClean("CustomStateName", (output, diagnostics));
        ExpectPointerBatch("CustomStateName", output);
    });

// ---------------- SystemDependencyInfo component access ----------------
string Access(string generated, string component)
{
    var marker = $"ComponentType<global::Probe.{component}>.Index, SystemAccessMode.";
    var at = generated.IndexOf(marker, StringComparison.Ordinal);
    if (at < 0) return "None";
    var start = at + marker.Length;
    var end = generated.IndexOf(')', start);
    return generated.Substring(start, end - start);
}

void AccessCase(string name, string parameters, string body, params (string Component, string Mode)[] expected)
{
    ScopeCase(name, "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " + Components +
        "public struct ProbeEvent { public int V; } " +
        "public static partial class ProbeSystems { " +
        "  static void Bump(ref int v) => v++; static void Take(Query<ProbeA, ProbeB> q) { } " +
        "  [System] public static unsafe void Tick(" + parameters + ") { " + body + " } } }",
        (output, diagnostics) =>
        {
            ExpectClean(name, (output, diagnostics));
            var generated = Generated(output);
            foreach (var (component, mode) in expected)
            {
                var actual = Access(generated, component);
                if (actual != mode) throw new Exception($"{name}: {component} expected {mode}, got {actual}");
            }
        });
}

AccessCase("AccessViaIter", "ref Query<ProbeA, ProbeB> query",
    "foreach (var (a, b) in query.iter()) a.Get.V += b.Read.V;",
    ("ProbeA", "ReadWrite"), ("ProbeB", "Read"));
AccessCase("AccessViaParIter", "ref Query<ProbeA, ProbeB> query",
    "foreach (var (a, b) in query.par_iter()) a.Get.V = b.Read.V;",
    ("ProbeA", "ReadWrite"), ("ProbeB", "Read"));
AccessCase("AccessAfterEventLoop", "ref Query<ProbeA, ProbeB> query, ref Events<ProbeEvent> events",
    "var n = 0; foreach (ref var ev in events) n += ev.V; foreach (var (a, b) in query) a.Get.V += n + b.Read.V;",
    ("ProbeA", "ReadWrite"), ("ProbeB", "Read"));
AccessCase("AccessPointerLoop", "ref Query<ProbeA, ProbeB> query",
    "foreach (var (a, b) in query.iter_unsafe()) a->V = b->V;",
    ("ProbeA", "ReadWrite"), ("ProbeB", "Read"));
AccessCase("AccessIncrement", "ref Query<ProbeA, ProbeB> query",
    "foreach (var (a, b) in query) { a.Get.V++; var x = b.Get.V; }",
    ("ProbeA", "ReadWrite"), ("ProbeB", "Read"));
AccessCase("AccessRefArgument", "ref Query<ProbeA, ProbeB> query",
    "foreach (var (a, b) in query) Bump(ref b.Get.V);",
    ("ProbeA", "Read"), ("ProbeB", "ReadWrite"));
AccessCase("AccessSecondQuery", "ref Query<ProbeA, ProbeB> query, ref Query<ProbeC> other",
    "foreach (var (a, b) in query) { var x = a.Read.V; } foreach (ref var c in other) c.V = 1;",
    ("ProbeA", "Read"), ("ProbeB", "Read"), ("ProbeC", "ReadWrite"));
AccessCase("AccessOtherEntity", "ref Query<ProbeA, ProbeB> query, ref State state",
    "foreach (var (a, b) in query) { } state.World.GetEntity(1).Get<ProbeD>().V = 3;",
    ("ProbeD", "ReadWrite"));
AccessCase("AccessUntrackedQuery", "ref Query<ProbeA, ProbeB> query",
    "Take(query);",
    ("ProbeA", "ReadWrite"), ("ProbeB", "ReadWrite"));
AccessCase("AccessTagFilter", "ref Query<ProbeA, ProbeTag> query",
    "foreach (var (a, t) in query) a.Get.V = 1;",
    ("ProbeA", "ReadWrite"), ("ProbeTag", "Read"));

Console.WriteLine($"{passed}/{passed} generator regressions passed.");
