# UPM distribution

Install with **Package Manager > Add package from git URL**:

```text
https://github.com/AlexWargon/Nukecs.git#upm
```

The package name is `com.wargon.nukecs`. Unity installs the dependencies declared
in `package.json`. Git must be available on the machine. See Unity's
[Git dependency documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html)
for branch, tag, and full commit-hash URLs.

## Package contents

The `upm` distribution branch contains only:

- `src/` and `SourceGen/`, with their tracked contents and `.meta` files.
- Root Markdown documentation and its `.meta` files.
- The required `package.json` manifest and its `.meta` file.

Demos, gameplay tests, benchmarks, theme presets, and repository automation are
not included. The Editor creates built-in themes from code when needed.
Links to omitted demo/test files in Markdown are rewritten to the exact source
commit on GitHub. Local links between included documents remain local.

Do not install both the UPM package and an `Assets/Nukecs` copy: they contain the
same assemblies and asset GUIDs. For demos and framework development, use the
full `dev` checkout instead.

## Publishing

Edit `dev`, including the package version in `package.json`. The
**Publish UPM branch** GitHub Actions workflow exports relevant commits pushed
to `dev` and appends a commit to `upm`. It can also be run manually on `dev`.
The workflow needs the repository's Actions service enabled and permission to
write that branch. A rejected push fails the run; it never force-pushes.

The exporter uses committed Git objects, so untracked files and local edits do
not enter a package. It requires an empty output directory outside the source
checkout. This avoids importing duplicate scripts into an open Unity project.

To inspect an export locally with Python 3.10+:

```powershell
python .github/scripts/export_upm.py --ref HEAD --output "$env:TEMP/nukecs-upm-preview"
python -m unittest discover -s .github/scripts -p 'test_*.py' -v
```

Use a new output directory for each export. Open a separate Unity project and
select the exported `package.json` with **Add package from disk** to test it.
The manifest targets Unity 6.0 and the dependency versions used in this checkout.
The package version is independent of the integer save-format version.

Unity locks Git packages to a resolved commit in `Packages/packages-lock.json`.
Update the Git dependency through Package Manager to receive a newer export;
an existing project does not follow the branch on every launch. For reproducible
releases, pin a full commit hash from `upm` in place of the branch name.
