import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from export_upm import export_package


class ExportTests(unittest.TestCase):
    def test_committed_allowlist_and_documentation_links(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            repo, output = root / "source", root / "package"
            repo.mkdir()

            def git(*args):
                return subprocess.check_output(["git", "-C", str(repo), *args]).decode().strip()

            git("init", "--quiet")
            git("config", "user.name", "Package Test")
            git("config", "user.email", "package-test@example.invalid")
            files = {
                "package.json": json.dumps({"name": "com.wargon.nukecs", "version": "1.0.0"}),
                "package.json.meta": "guid: package",
                "src/Nukecs.asmdef": '{"name":"Nukecs"}',
                "src/Example.cs": "// committed source",
                "src/Example.cs.meta": "guid: source",
                "src/Guide.md": "[demo](../Demos/README.md#setup) [root](../README.md)",
                "src.meta": "guid: src",
                "SourceGen/NUKECSGEN.dll": b"\x00\xffbinary\x80",
                "SourceGen/NUKECSGEN.dll.meta": "labels:\n- RoslynAnalyzer",
                "SourceGen.meta": "guid: generator",
                "README.md": "[api](API_REFERENCE.md) [demo](Demos/README.md) [web](https://example.com)",
                "README.md.meta": "guid: readme",
                "API_REFERENCE.md": "# API",
                "Demos/README.md": "# Demo",
                "UnitTests/Test.cs": "// omitted",
                "Benches/Bench.cs": "// omitted",
                "EcsDebugV2Themes/Default.json": "{}",
                ".github/workflows/upm.yml": "# omitted",
                "DashboardTheme.json": "{}",
            }
            for name, content in files.items():
                path = repo / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content if isinstance(content, bytes) else content.encode())
            git("add", "--all")
            git("commit", "--quiet", "-m", "Source snapshot")
            commit = git("rev-parse", "HEAD")
            (repo / "src/Example.cs").write_text("// uncommitted changes")
            (repo / "src/Untracked.cs").write_text("// do not ship")
            self.assertEqual(commit, export_package(repo, "HEAD", output))
            self.assertEqual({"src", "SourceGen"}, {p.name for p in output.iterdir() if p.is_dir()})
            self.assertEqual("// committed source", (output / "src/Example.cs").read_text())
            self.assertFalse((output / "src/Untracked.cs").exists())
            self.assertFalse((output / "DashboardTheme.json").exists())
            self.assertEqual(files["SourceGen/NUKECSGEN.dll"], (output / "SourceGen/NUKECSGEN.dll").read_bytes())
            self.assertIn("RoslynAnalyzer", (output / "SourceGen/NUKECSGEN.dll.meta").read_text())
            self.assertIn("[api](API_REFERENCE.md)", (output / "README.md").read_text())
            self.assertIn(f"/blob/{commit}/Demos/README.md)", (output / "README.md").read_text())
            self.assertIn(f"/blob/{commit}/Demos/README.md#setup)", (output / "src/Guide.md").read_text())
            with self.assertRaisesRegex(ValueError, "must be empty"):
                export_package(repo, "HEAD", output)
            with self.assertRaisesRegex(ValueError, "outside"):
                export_package(repo, "HEAD", repo / "NestedPackage")


if __name__ == "__main__":
    unittest.main()
