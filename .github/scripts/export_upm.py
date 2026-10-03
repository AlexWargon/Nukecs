"""Export a committed Nukecs revision as a minimal UPM package (Python 3.10+)."""

import argparse
import io
import json
import posixpath
import re
import subprocess
import zipfile
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit


def package_file(path: str) -> bool:
    if path.startswith(("src/", "SourceGen/")):
        return True
    if "/" in path:
        return False
    return path in {"src.meta", "SourceGen.meta", "package.json", "package.json.meta"} or path.endswith((".md", ".md.meta"))


def export_package(repo: Path, revision: str, output: Path) -> str:
    repo, output = repo.resolve(), output.resolve()
    if output == repo or repo in output.parents:
        raise ValueError("Export outside the source checkout to avoid duplicate Unity assets.")
    if output.exists() and any(output.iterdir()):
        raise ValueError("Output directory must be empty; existing files are never removed.")

    def git(*args: str) -> bytes:
        return subprocess.check_output(["git", "-C", str(repo), *args])

    commit = git("rev-parse", "--verify", revision + "^{commit}").decode().strip()
    tracked = git("ls-tree", "-rz", "--name-only", commit).decode().split("\0")
    selected = [path for path in tracked if path and package_file(path)]
    required = {"package.json", "package.json.meta", "src/Nukecs.asmdef", "src.meta",
                "SourceGen/NUKECSGEN.dll", "SourceGen/NUKECSGEN.dll.meta", "SourceGen.meta", "README.md"}
    if not required.issubset(selected):
        raise ValueError("Missing package files: " + ", ".join(sorted(required - set(selected))))

    archive = git("archive", "--format=zip", commit, "--", *selected)
    with zipfile.ZipFile(io.BytesIO(archive)) as source:
        manifest = json.loads(source.read("package.json"))
        if manifest.get("name") != "com.wargon.nukecs" or not manifest.get("version"):
            raise ValueError("Invalid Nukecs package manifest.")
        output.mkdir(parents=True, exist_ok=True)
        for path in selected:
            target = (output / path).resolve()
            if output not in target.parents:
                raise ValueError("Invalid package path: " + path)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read(path))

    # Keep local package links, but direct omitted demos/tests to the exact source revision.
    source_files = set(tracked)
    for document in output.rglob("*.md"):
        relative = document.relative_to(output).as_posix()

        def replace_link(match: re.Match) -> str:
            target = match.group(2)
            url = urlsplit(target)
            if url.scheme or url.netloc or not url.path or url.path.startswith("/"):
                return match.group(0)
            resolved = posixpath.normpath(posixpath.join(posixpath.dirname(relative), unquote(url.path)))
            if (output / resolved).exists() or resolved not in source_files:
                return match.group(0)
            remote = "https://github.com/AlexWargon/Nukecs/blob/" + commit + "/" + quote(resolved, safe="/")
            if url.fragment:
                remote += "#" + url.fragment
            return match.group(1) + remote + ")"

        text = document.read_text(encoding="utf-8")
        text = re.sub(r"(\[[^\]\n]*\]\()([^\s)]+)\)", replace_link, text)
        document.write_text(text, encoding="utf-8", newline="\n")
    return commit


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--ref", default="HEAD", help="Committed source revision; uncommitted files are never exported.")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    commit = export_package(args.repo, args.ref, args.output)
    print(f"Exported com.wargon.nukecs from {commit} to {args.output.resolve()}")
