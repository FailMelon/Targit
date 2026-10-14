"""Read and roll CHANGELOG.md for releases.

  notes <version>                      Print the notes for a version, falling back to Unreleased.
  versions                             Print every released version, newest first.
  roll <version> <date> [tag-file]     Move Unreleased entries under "## <version> - <date>" and start a
                                       new empty Unreleased. With tag-file (the changelog at the release
                                       tag), only entries that were already in its Unreleased section move,
                                       so anything added to the branch after the tag stays unreleased.
"""

import os
import re
import sys
from pathlib import Path

CHANGELOG = Path(os.environ.get("CHANGELOG") or Path(__file__).resolve().parent.parent / "CHANGELOG.md")
HEADING = re.compile(r"^## (.+?)\s*$")
VERSION = re.compile(r"^(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)(?:\s+-\s+.*)?$")


def split(text):
    """Return (preamble lines, [(heading text, body lines)])."""
    preamble, sections = [], []
    for line in text.splitlines():
        match = HEADING.match(line)
        if match:
            sections.append((match.group(1), []))
        elif sections:
            sections[-1][1].append(line)
        else:
            preamble.append(line)
    return preamble, sections


def join(preamble, sections):
    lines = list(preamble)
    for heading, body in sections:
        lines.append(f"## {heading}")
        lines.extend(body)
    return "\n".join(lines).rstrip("\n") + "\n"


def entries(body):
    """Group a section body into entries: a bullet plus any continuation lines."""
    groups = []
    for line in body:
        if line.startswith("- ") or not groups:
            if line.strip():
                groups.append([line])
        elif line.strip():
            groups[-1].append(line)
    return ["\n".join(group) for group in groups]


def section(sections, name):
    for heading, body in sections:
        if heading.lower() == name.lower() or VERSION.match(heading) and VERSION.match(heading).group(1) == name:
            return body
    return None


def notes(version):
    _, sections = split(CHANGELOG.read_text(encoding="utf-8"))
    body = section(sections, version)
    if body is None:
        body = section(sections, "Unreleased") or []
    print("\n".join(entries(body)))


def versions():
    _, sections = split(CHANGELOG.read_text(encoding="utf-8"))
    for heading, _ in sections:
        match = VERSION.match(heading)
        if match:
            print(match.group(1))


def roll(version, date, tag_file=None):
    preamble, sections = split(CHANGELOG.read_text(encoding="utf-8"))
    if section(sections, version) is not None:
        print(f"CHANGELOG.md already has a {version} section.")
        return
    index = next((i for i, (heading, _) in enumerate(sections) if heading.lower() == "unreleased"), None)
    current = entries(sections[index][1]) if index is not None else []
    if tag_file:
        _, tagged = split(Path(tag_file).read_text(encoding="utf-8"))
        released = set(entries(section(tagged, "Unreleased") or []))
    else:
        released = set(current)
    moved = [entry for entry in current if entry in released]
    kept = [entry for entry in current if entry not in released]

    def body(items):
        return [""] + ("\n".join(items).splitlines() + [""] if items else [])

    new_sections = [("Unreleased", body(kept)), (f"{version} - {date}", body(moved))]
    if index is None:
        sections[0:0] = new_sections
    else:
        sections[index : index + 1] = new_sections
    if preamble and preamble[-1].strip():
        preamble.append("")
    CHANGELOG.write_text(join(preamble, sections), encoding="utf-8", newline="\n")
    print(f"Moved {len(moved)} entries to {version}; {len(kept)} remain unreleased.")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    command, *args = sys.argv[1:] or ["help"]
    if command == "notes" and len(args) == 1:
        notes(*args)
    elif command == "versions" and not args:
        versions()
    elif command == "roll" and len(args) in (2, 3):
        roll(*args)
    else:
        print(__doc__)
        sys.exit(2)
