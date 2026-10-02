using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wargon.Nukecs.NUKECSGEN;

if (args.Length != 2) throw new ArgumentException("Pass Unity Editor Data directory and Unity project directory.");
var references = Directory.GetFiles(Path.Combine(args[0], "NetStandard", "ref", "2.1.0"), "*.dll")
    .Concat(Directory.GetFiles(Path.Combine(args[0], "Managed", "UnityEngine"), "*.dll"))
    .Concat(Directory.GetFiles(Path.Combine(args[1], "Library", "ScriptAssemblies"), "*.dll")
        .Where(p => Path.GetFileName(p) == "Nukecs.dll" || Path.GetFileName(p).StartsWith("Unity.")))
    .Distinct().Select(p => MetadataReference.CreateFromFile(p)).ToArray();
var parse = new CSharpParseOptions(LanguageVersion.CSharp9);
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
};
foreach (var test in cases)
{
    var source = "using Wargon.Nukecs; using Unity.Burst; namespace Probe { " +
        "public struct ProbeValue : IComponent { public float Value; } public struct ProbePool : IPoolComponent { public float Value; } " +
        "public static class ProbeSystems { [System, BurstCompile, RequireBatch] public static void " + test.Name +
        "(ref " + test.Query + " query, ref State state) { " + test.Body + " } } }";
    var compilation = CSharpCompilation.Create("Nukecs.Tests", new[] { CSharpSyntaxTree.ParseText(source, parse) }, references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new SrcGen().AsSourceGenerator() }, parseOptions: parse);
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
    var required = diagnostics.Where(d => d.Id == "NUKECS002").ToArray();
    if (test.Reason == null)
    {
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0) throw new Exception(test.Name + ": " + string.Join("\n", errors.Select(d => d.ToString())));
        var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        if (!generated.Contains("SystemCompilationKind.PointerBatch") || !generated.Contains("BatchDispatchParallel("))
            throw new Exception(test.Name + ": missing contextual batch metadata/parallel dispatch");
    }
    else if (required.Length != 1 || required[0].Severity != DiagnosticSeverity.Error || !required[0].GetMessage().Contains(test.Reason))
        throw new Exception(test.Name + ": expected NUKECS002 / " + test.Reason + ", got " + string.Join("\n", diagnostics.Select(d => d.ToString())));
    Console.WriteLine("PASS " + test.Name);
}
Console.WriteLine($"{cases.Length}/{cases.Length} generator regressions passed.");
