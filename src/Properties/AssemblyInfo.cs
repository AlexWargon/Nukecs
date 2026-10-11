using System.Runtime.CompilerServices;

// Test assembly needs internals (ResStorage slots, unsafe world fields) for
// stabilization regression tests.
[assembly: InternalsVisibleTo("Nukecs.Tests")]
// Unity integration (debuggers, bakers, lifecycle adapters) reads world internals.
[assembly: InternalsVisibleTo("Nukecs.Unity")]
