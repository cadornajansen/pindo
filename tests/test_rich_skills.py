"""Regression coverage for routing, packaged resources and evaluation completeness."""
from __future__ import annotations
from copy import deepcopy
import json
from pathlib import Path
import shutil
import tempfile
import unittest

from tools.application_skills import ValidationError, bundle_data, load_library, load_profiles, match_skills, _skill

ROOT = Path(__file__).resolve().parents[1]

class RichSkillsTests(unittest.TestCase):
    def test_bundle_is_current_and_profiles_cover_every_skill(self) -> None:
        self.assertEqual((ROOT / 'PinDo/Resources/ApplicationSkills.json').read_text(encoding='utf-8'), bundle_data(ROOT))
        self.assertEqual(len(load_profiles(ROOT)), 15)

    def test_every_documented_intent_routes_to_its_skill(self) -> None:
        skills, _ = load_library(ROOT)
        for skill in skills:
            for intent in skill['intents']:
                with self.subTest(skill=skill['id'], intent=intent):
                    self.assertIn(skill['id'], match_skills(skills, skill['application_id'], intent))

    def test_matching_keeps_ambiguity_and_checks_application_and_word_boundaries(self) -> None:
        skills, _ = load_library(ROOT)
        sample = deepcopy(skills[0])
        sample['match_groups'] = [['mask'], ['layer', 'image']]
        alternate = deepcopy(sample)
        alternate['id'] = 'other.skill'
        self.assertEqual(match_skills([sample, alternate], sample['application_id'], 'mask the image'), sorted([sample['id'], alternate['id']]))
        self.assertEqual(match_skills([sample], sample['application_id'], 'unmask the image'), [])
        self.assertEqual(match_skills([sample], sample['application_id'], 'mask'), [])
        self.assertEqual(match_skills([sample], 'wrong.app', 'mask image'), [])

    def test_unsupported_verification_and_empty_search_terms_are_rejected(self) -> None:
        skills, _ = load_library(ROOT)
        for field in ('verification', 'match_groups'):
            skill = deepcopy(skills[0])
            if field == 'verification': skill['steps'][0]['verification'] = 'click'
            else: skill['match_groups'] = [['!!!']]
            with self.subTest(field=field), self.assertRaises(ValidationError):
                _skill(skill, 'fixture')

    def test_recovery_coverage_is_required(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for folder in ('skills', 'evaluations/cases'):
                shutil.copytree(ROOT / folder, root / folder)
            path = next((root / 'evaluations/cases').glob('*_recovery.json'))
            path.unlink()
            with self.assertRaisesRegex(ValidationError, 'English, Taglish and recovery'):
                load_library(root)

    def test_no_hands_on_claims_without_recorded_versions(self) -> None:
        skills, cases = load_library(ROOT)
        for item in skills + cases:
            if item['validation']['status'] != 'unverified':
                self.assertTrue(item.get('application_version') or item['validation'].get('tested_versions'))

    def test_audio_outcomes_require_confirmation(self) -> None:
        skills, _ = load_library(ROOT)
        for skill in skills:
            for step in skill['steps']:
                if any(word in step['objective'].lower() for word in ('listen', 'audition', 'against the audio')):
                    self.assertEqual(step['verification'], 'user_confirmation', skill['id'])

if __name__ == '__main__': unittest.main()
