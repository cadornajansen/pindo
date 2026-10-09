"""Load application skills and evaluation cases as data; never execute them."""

from __future__ import annotations

import argparse
from datetime import date
import json
from pathlib import Path
import re
import sys
from typing import Any
from urllib.parse import urlsplit


class ValidationError(ValueError):
    """A library file or field does not satisfy the version-1 contract."""


_DOTTED_ID = re.compile(r"[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+")
_STEP_ID = re.compile(r"[a-z][a-z0-9]*(?:_[a-z0-9]+)*")
_PLATFORMS = {"macos", "windows"}
_STATUSES = {"verified", "partially_verified", "unverified"}


def _fail(location: str, message: str) -> None:
    raise ValidationError(f"{location}: {message}")


def _record(value: Any, fields: set[str], location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        _fail(location, "expected an object")
    missing = fields - value.keys()
    unknown = value.keys() - fields
    if missing:
        _fail(f"{location}.{sorted(missing)[0]}", "required field is missing")
    if unknown:
        _fail(f"{location}.{sorted(unknown)[0]}", "unknown field")
    return value


def _text(value: Any, location: str) -> None:
    if not isinstance(value, str) or not value.strip():
        _fail(location, "expected a nonempty string")


def _choice(value: Any, choices: set[str], location: str) -> None:
    if not isinstance(value, str) or value not in choices:
        _fail(location, f"expected one of: {', '.join(sorted(choices))}")


def _identifier(value: Any, pattern: re.Pattern[str], location: str) -> None:
    _text(value, location)
    if pattern.fullmatch(value) is None:
        _fail(location, "invalid lowercase identifier format")


def _list(value: Any, location: str, *, allow_empty: bool = False) -> list[Any]:
    if not isinstance(value, list) or (not value and not allow_empty):
        _fail(location, "expected a list" if allow_empty else "expected a nonempty list")
    return value


def _strings(value: Any, location: str, *, allow_empty: bool = False) -> None:
    for index, item in enumerate(_list(value, location, allow_empty=allow_empty)):
        _text(item, f"{location}[{index}]")


def _version(value: Any, location: str) -> None:
    if type(value) is not int or value != 1:
        _fail(location, "expected integer schema version 1")


def _target(value: Any, location: str) -> None:
    target = _record(value, {"role", "semantic_name"}, location)
    for field in ("role", "semantic_name"):
        _text(target[field], f"{location}.{field}")


def _validation(value: Any, location: str, *, skill: bool) -> str:
    fields = {"status", "notes"}
    if skill:
        fields |= {"documented_versions", "tested_versions"}
    validation = _record(value, fields, location)
    _choice(validation["status"], _STATUSES, f"{location}.status")
    _text(validation["notes"], f"{location}.notes")
    if skill:
        _strings(validation["documented_versions"], f"{location}.documented_versions")
        _strings(validation["tested_versions"], f"{location}.tested_versions", allow_empty=True)
        if validation["status"] != "unverified" and not validation["tested_versions"]:
            _fail(f"{location}.tested_versions", "verified status requires a tested version")
    return validation["status"]


def _skill(value: Any, location: str) -> dict[str, Any]:
    skill = _record(value, {
        "schema_version", "id", "application_id", "application", "platform", "title",
        "intents", "prerequisites", "success_criteria", "constraints", "steps",
        "recovery", "sources", "validation",
    }, location)
    _version(skill["schema_version"], f"{location}.schema_version")
    _identifier(skill["id"], _DOTTED_ID, f"{location}.id")
    _identifier(skill["application_id"], _DOTTED_ID, f"{location}.application_id")
    _choice(skill["platform"], _PLATFORMS, f"{location}.platform")
    for field in ("application", "title"):
        _text(skill[field], f"{location}.{field}")
    for field in ("intents", "prerequisites", "success_criteria", "constraints"):
        _strings(skill[field], f"{location}.{field}")
    step_ids: set[str] = set()
    for index, value in enumerate(_list(skill["steps"], f"{location}.steps")):
        step_location = f"{location}.steps[{index}]"
        step = _record(value, {"id", "objective", "target", "expected_result"}, step_location)
        _identifier(step["id"], _STEP_ID, f"{step_location}.id")
        if step["id"] in step_ids:
            _fail(f"{step_location}.id", f"duplicate step ID {step['id']!r}")
        step_ids.add(step["id"])
        _text(step["objective"], f"{step_location}.objective")
        _target(step["target"], f"{step_location}.target")
        _strings(step["expected_result"], f"{step_location}.expected_result")
    for index, value in enumerate(_list(skill["recovery"], f"{location}.recovery")):
        recovery_location = f"{location}.recovery[{index}]"
        recovery = _record(value, {"condition", "guidance"}, recovery_location)
        for field in ("condition", "guidance"):
            _text(recovery[field], f"{recovery_location}.{field}")
    for index, value in enumerate(_list(skill["sources"], f"{location}.sources")):
        source_location = f"{location}.sources[{index}]"
        source = _record(value, {"title", "url", "research_date"}, source_location)
        for field in ("title", "url", "research_date"):
            _text(source[field], f"{source_location}.{field}")
        try:
            url = urlsplit(source["url"])
            valid_url = url.scheme == "https" and bool(url.hostname)
        except ValueError:
            valid_url = False
        if not valid_url:
            _fail(f"{source_location}.url", "expected an HTTPS URL with a hostname")
        try:
            if re.fullmatch(r"\d{4}-\d{2}-\d{2}", source["research_date"]) is None:
                raise ValueError
            date.fromisoformat(source["research_date"])
        except ValueError:
            _fail(f"{source_location}.research_date", "expected a valid YYYY-MM-DD date")
    _validation(skill["validation"], f"{location}.validation", skill=True)
    return skill


def _evaluation(value: Any, location: str) -> dict[str, Any]:
    case = _record(value, {
        "schema_version", "id", "skill_id", "application_id", "platform",
        "application_version", "starting_state", "user_instruction", "expected_next_action",
        "expected_result", "failure_criteria", "language", "validation",
    }, location)
    _version(case["schema_version"], f"{location}.schema_version")
    for field in ("id", "skill_id"):
        _identifier(case[field], _DOTTED_ID, f"{location}.{field}")
    _identifier(case["application_id"], _DOTTED_ID, f"{location}.application_id")
    _choice(case["platform"], _PLATFORMS, f"{location}.platform")
    for field in ("starting_state", "user_instruction"):
        _text(case[field], f"{location}.{field}")
    if case["application_version"] is not None:
        _text(case["application_version"], f"{location}.application_version")
    action_location = f"{location}.expected_next_action"
    action = _record(case["expected_next_action"], {"objective", "target"}, action_location)
    _text(action["objective"], f"{action_location}.objective")
    _target(action["target"], f"{action_location}.target")
    for field in ("expected_result", "failure_criteria"):
        _strings(case[field], f"{location}.{field}")
    _choice(case["language"], {"english", "taglish"}, f"{location}.language")
    status = _validation(case["validation"], f"{location}.validation", skill=False)
    if status != "unverified" and case["application_version"] is None:
        _fail(f"{location}.application_version", "verified status requires an application version")
    return case


def _read(path: Path, root: Path) -> tuple[Any, str]:
    location = path.relative_to(root).as_posix()

    def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        record: dict[str, Any] = {}
        for key, value in pairs:
            if key in record:
                _fail(location, f"duplicate JSON field {key!r}")
            record[key] = value
        return record

    try:
        with path.open(encoding="utf-8") as source:
            value = json.load(source, object_pairs_hook=unique_object)
    except json.JSONDecodeError as error:
        _fail(location, f"invalid JSON at line {error.lineno}, column {error.colno}: {error.msg}")
    except (OSError, UnicodeError) as error:
        _fail(location, f"cannot read UTF-8 JSON: {error}")
    return value, location


def _files(root: Path, directory: str) -> list[Path]:
    folder = root / directory
    if not folder.is_dir():
        _fail(directory, "required directory is missing")
    paths = sorted(folder.rglob("*.json"))
    if not paths:
        _fail(directory, "expected at least one JSON file")
    return paths


def load_library(root: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Validate all records and links, returning the original JSON objects."""
    skills: list[dict[str, Any]] = []
    cases: list[dict[str, Any]] = []
    skills_by_id: dict[str, dict[str, Any]] = {}
    skill_locations: dict[str, str] = {}
    case_ids: set[str] = set()
    covered_ids: set[str] = set()
    for path in _files(root, "skills"):
        value, location = _read(path, root)
        skill = _skill(value, location)
        if skill["id"] in skills_by_id:
            _fail(f"{location}.id", f"duplicate skill ID {skill['id']!r}")
        skills_by_id[skill["id"]] = skill
        skill_locations[skill["id"]] = location
        skills.append(skill)
    for path in _files(root, "evaluations/cases"):
        value, location = _read(path, root)
        case = _evaluation(value, location)
        if case["id"] in case_ids:
            _fail(f"{location}.id", f"duplicate evaluation ID {case['id']!r}")
        case_ids.add(case["id"])
        if case["skill_id"] not in skills_by_id:
            _fail(f"{location}.skill_id", f"unknown skill ID {case['skill_id']!r}")
        skill = skills_by_id[case["skill_id"]]
        for field in ("application_id", "platform"):
            if case[field] != skill[field]:
                _fail(f"{location}.{field}", f"must match skill {case['skill_id']!r}")
        covered_ids.add(case["skill_id"])
        cases.append(case)
    for skill_id in skills_by_id.keys() - covered_ids:
        _fail(f"{skill_locations[skill_id]}.id", "skill requires at least one evaluation case")
    return skills, cases


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    arguments = parser.parse_args()
    try:
        skills, cases = load_library(arguments.root)
    except ValidationError as error:
        print(f"Validation failed: {error}", file=sys.stderr)
        return 1
    print(f"Validated {len(skills)} skills and {len(cases)} evaluation cases. No app or model was run.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
