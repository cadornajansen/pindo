"""Contract tests use disposable copies of the checked-in library."""

from __future__ import annotations

from copy import deepcopy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from typing import Any
import unittest
from unittest.mock import patch

from tools.application_skills import ValidationError, load_library


_ROOT = Path(__file__).resolve().parents[1]
_SCRIPT = _ROOT / "tools" / "application_skills.py"


class ApplicationSkillsTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        for directory in ("skills", "evaluations/cases"):
            shutil.copytree(_ROOT / directory, self.root / directory)
        self.skill_count = len(list((self.root / "skills").rglob("*.json")))
        self.case_count = len(list((self.root / "evaluations/cases").rglob("*.json")))
        self.skill_path = sorted((self.root / "skills").rglob("*.json"))[0]
        self.case_path = sorted((self.root / "evaluations/cases").rglob("*.json"))[0]

    def read(self, path: Path) -> dict[str, Any]:
        return json.loads(path.read_text(encoding="utf-8"))

    def write(self, path: Path, value: Any) -> None:
        path.write_text(json.dumps(value), encoding="utf-8")

    def assert_invalid(self, field: str) -> None:
        with self.assertRaises(ValidationError) as caught:
            load_library(self.root)
        self.assertIn(field, str(caught.exception))

    def test_checked_in_library_loads(self) -> None:
        skills, cases = load_library(_ROOT)
        self.assertTrue(skills)
        self.assertTrue(cases)
        self.assertEqual(len(skills), self.skill_count)
        self.assertEqual(len(cases), self.case_count)
        self.assertEqual({skill["id"] for skill in skills}, {case["skill_id"] for case in cases})
        self.assertTrue(all(skill["id"] for skill in skills))
        self.assertTrue(all(case["id"] for case in cases))

    def test_missing_nested_field_names_the_file_and_field(self) -> None:
        skill = self.read(self.skill_path)
        del skill["steps"][0]["target"]["semantic_name"]
        self.write(self.skill_path, skill)
        self.assert_invalid(self.skill_path.relative_to(self.root).as_posix() + ".steps[0].target.semantic_name")

    def test_invalid_nested_result(self) -> None:
        skill = self.read(self.skill_path)
        skill["steps"][0]["expected_result"] = [""]
        self.write(self.skill_path, skill)
        self.assert_invalid(".steps[0].expected_result[0]")

    def test_version_rejects_bool_and_unsupported_values(self) -> None:
        for path in (self.skill_path, self.case_path):
            original = self.read(path)
            for version in (True, False, 3, "1", 1.0, None):
                with self.subTest(path=path.name, version=version):
                    changed = deepcopy(original)
                    changed["schema_version"] = version
                    self.write(path, changed)
                    self.assert_invalid(".schema_version")
            self.write(path, original)

    def test_duplicate_skill_id(self) -> None:
        self.write(self.skill_path.with_name("duplicate.json"), self.read(self.skill_path))
        self.assert_invalid("duplicate skill ID")

    def test_duplicate_evaluation_id(self) -> None:
        self.write(self.case_path.with_name("duplicate.json"), self.read(self.case_path))
        self.assert_invalid("duplicate evaluation ID")

    def test_duplicate_step_id(self) -> None:
        skill = self.read(self.skill_path)
        skill["steps"].append(deepcopy(skill["steps"][0]))
        self.write(self.skill_path, skill)
        self.assert_invalid("duplicate step ID")

    def test_evaluation_must_reference_an_existing_skill(self) -> None:
        case = self.read(self.case_path)
        case["skill_id"] = "missing.skill"
        self.write(self.case_path, case)
        self.assert_invalid(".skill_id: unknown skill ID")

    def test_evaluation_must_match_skill_platform_and_application(self) -> None:
        original = self.read(self.case_path)
        changes = {
            "platform": "windows" if original["platform"] == "macos" else "macos",
            "application_id": "apple.preview" if original["application_id"] != "apple.preview" else "apple.finder",
        }
        for field, value in changes.items():
            with self.subTest(field=field):
                case = deepcopy(original)
                case[field] = value
                self.write(self.case_path, case)
                self.assert_invalid(f".{field}: must match skill")

    def test_every_skill_needs_evaluation_coverage(self) -> None:
        skill_id = self.read(self.case_path)["skill_id"]
        for path in (self.root / "evaluations/cases").rglob("*.json"):
            if self.read(path)["skill_id"] == skill_id:
                path.unlink()
        self.assert_invalid("skill requires at least one evaluation case")

    def test_coordinate_and_command_fields_are_rejected_in_records(self) -> None:
        original = self.read(self.skill_path)
        for field, value in (("x", 100), ("y", 200), ("point", [100, 200]), ("bbox", [1, 2, 3, 4]), ("command", "open Finder")):
            with self.subTest(field=field):
                skill = deepcopy(original)
                skill["steps"][0]["target"][field] = value
                self.write(self.skill_path, skill)
                self.assert_invalid(f".target.{field}: unknown field")

    def test_unknown_evaluation_action_field_is_rejected(self) -> None:
        case = self.read(self.case_path)
        case["expected_next_action"]["command"] = "run something"
        self.write(self.case_path, case)
        self.assert_invalid(".expected_next_action.command: unknown field")

    def test_verified_skill_requires_tested_versions(self) -> None:
        original = self.read(self.skill_path)
        for status in ("verified", "partially_verified"):
            with self.subTest(status=status):
                skill = deepcopy(original)
                skill["validation"]["status"] = status
                skill["validation"]["tested_versions"] = []
                self.write(self.skill_path, skill)
                self.assert_invalid(".validation.tested_versions")

    def test_verified_evaluation_requires_application_version(self) -> None:
        original = self.read(self.case_path)
        for status in ("verified", "partially_verified"):
            with self.subTest(status=status):
                case = deepcopy(original)
                case["validation"]["status"] = status
                case["application_version"] = None
                self.write(self.case_path, case)
                self.assert_invalid(".application_version")

    def test_verified_records_accept_explicit_tested_versions(self) -> None:
        skill = self.read(self.skill_path)
        linked_case_path = next(
            path for path in (self.root / "evaluations/cases").rglob("*.json")
            if self.read(path)["skill_id"] == skill["id"]
        )
        case = self.read(linked_case_path)
        for status in ("verified", "partially_verified"):
            with self.subTest(status=status):
                skill["validation"]["status"] = status
                skill["validation"]["tested_versions"] = ["test-version"]
                case["validation"]["status"] = status
                case["application_version"] = "test-version"
                self.write(self.skill_path, skill)
                self.write(linked_case_path, case)
                skills, cases = load_library(self.root)
                loaded_skill = next(item for item in skills if item["id"] == skill["id"])
                loaded_case = next(item for item in cases if item["id"] == case["id"])
                self.assertEqual(loaded_skill["validation"]["status"], status)
                self.assertEqual(loaded_case["validation"]["status"], status)

    def test_additional_application_identifier_needs_no_code_change(self) -> None:
        skill = self.read(self.skill_path)
        skill["application_id"] = "microsoft.excel"
        self.write(self.skill_path, skill)
        for path in (self.root / "evaluations/cases").rglob("*.json"):
            case = self.read(path)
            if case["skill_id"] == skill["id"]:
                case["application_id"] = skill["application_id"]
                self.write(path, case)
        skills, _ = load_library(self.root)
        self.assertIn("microsoft.excel", [item["application_id"] for item in skills])

    def test_invalid_source_url_and_date(self) -> None:
        original = self.read(self.skill_path)
        for field, value in (("url", "http://example.com"), ("url", "https://"), ("research_date", "2026-02-30"), ("research_date", "20261010")):
            with self.subTest(field=field, value=value):
                skill = deepcopy(original)
                skill["sources"][0][field] = value
                self.write(self.skill_path, skill)
                self.assert_invalid(f".sources[0].{field}")

    def test_missing_and_empty_directories(self) -> None:
        for directory in ("skills", "evaluations/cases"):
            with self.subTest(directory=directory):
                folder = self.root / directory
                backup = folder.with_name(folder.name + "_backup")
                folder.rename(backup)
                self.assert_invalid(f"{directory}: required directory is missing")
                folder.mkdir()
                self.assert_invalid(f"{directory}: expected at least one JSON file")
                folder.rmdir()
                backup.rename(folder)

    def test_malformed_json_reports_location(self) -> None:
        self.skill_path.write_text('{"id":', encoding="utf-8")
        self.assert_invalid(self.skill_path.relative_to(self.root).as_posix() + ": invalid JSON at line 1")

    def test_duplicate_json_keys_report_the_filename(self) -> None:
        self.skill_path.write_text('{"id": "first.skill", "id": "second.skill"}', encoding="utf-8")
        self.assert_invalid(self.skill_path.relative_to(self.root).as_posix() + ": duplicate JSON field 'id'")

    def test_invalid_encoding_is_reported(self) -> None:
        self.skill_path.write_bytes(b"\xff\xfe\xff")
        self.assert_invalid("cannot read UTF-8 JSON")

    def test_read_error_is_reported(self) -> None:
        with patch.object(Path, "open", side_effect=PermissionError("access denied")):
            self.assert_invalid("cannot read UTF-8 JSON: access denied")

    def test_instruction_strings_remain_data(self) -> None:
        case = self.read(self.case_path)
        instruction = "__import__('os').system('this must never execute')"
        case["user_instruction"] = instruction
        self.write(self.case_path, case)
        _, cases = load_library(self.root)
        self.assertIn(instruction, [item["user_instruction"] for item in cases])

    def test_cli_success_and_helpful_failure_exit_codes(self) -> None:
        command = [sys.executable, "-B", str(_SCRIPT), "--root", str(self.root)]
        success = subprocess.run(command, capture_output=True, text=True, check=False)
        self.assertEqual(success.returncode, 0, success.stderr)
        self.assertIn(f"Validated {self.skill_count} skills and {self.case_count} evaluation cases", success.stdout)
        self.assertIn("No app or model was run.", success.stdout)
        self.skill_path.write_text("{", encoding="utf-8")
        failure = subprocess.run(command, capture_output=True, text=True, check=False)
        self.assertEqual(failure.returncode, 1)
        self.assertIn("Validation failed:", failure.stderr)
        self.assertIn(self.skill_path.relative_to(self.root).as_posix(), failure.stderr)
        self.assertNotIn("Traceback", failure.stderr)


if __name__ == "__main__":
    unittest.main()
