# Generator maintenance patches

The runtime ships `../NUKECSGEN.dll` as a Roslyn analyzer. Full generator source
is in a separate repository; `../../../NUKECSGEN/` from the package root is the
path in this checkout, not an installation requirement or portable source path.

`LocalResources.patch` implements system-local resource parameter generation;
`LocalResourcesBurst.patch` replaces managed owner/slot lookup with numeric keys
and the framework's HashMap. Apply them in that order from the generator repo
root when working from their original baseline. They document historical changes;
do not reapply them to a generator revision that already includes those changes.

After editing the generator, build its project, replace the analyzer DLL while
preserving its Unity metadata, and run the checks in `../Tests~/README.md` plus
Unity system/Local regression fixtures. Unity ignores these `~` folders.
