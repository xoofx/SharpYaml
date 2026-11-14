"""Import upstream YAML Test Suite source using its inheritance/unescape rules.

Development-only dependency: PyYAML. Tests use the checked-in JSON fixture.
See src/SharpYaml.Tests/files/yaml-test-suite.README.md for the pinned revision.
"""

import json
from pathlib import Path
import re
import sys

import yaml


def unescape(text):
    text = text or ""
    text = text.replace("\u2423", " ").replace("\u2190", "\r")
    text = text.replace("\u21d4", "\ufeff").replace("\u21b5", "")
    return re.sub(r"\u220e\n\Z", "", re.sub("\u2014*\u00bb", "\t", text))


cases = []
for source in sorted((Path(sys.argv[1]) / "src").glob("*.yaml")):
    entries = yaml.safe_load(source.read_text(encoding="utf-8"))
    if entries[0].get("skip"):
        continue
    previous = {}
    width = len(str(len(entries) - 1)) + 1
    for index, entry in enumerate(entries):
        current = {**previous, **entry}
        current["fail"] = "fail" in entry
        previous = current.copy()
        events = re.sub(r"^\s+", "", current.get("tree") or "", flags=re.M).rstrip("\n") + "\n"
        cases.append({
            "id": source.stem + ("/" + str(index).zfill(width) if len(entries) > 1 else ""),
            "name": current["name"],
            "yaml": unescape(current["yaml"]),
            "error": current["fail"],
            "events": unescape(events),
            "json": unescape(current.get("json")),
        })

target = Path(__file__).resolve().parents[1] / "src/SharpYaml.Tests/files/yaml-test-suite.json"
target.write_text(json.dumps(cases, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
print(f"Imported {len(cases)} cases ({sum(case['error'] for case in cases)} invalid).")
