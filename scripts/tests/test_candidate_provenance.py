"""Integridade e minimização de dados da proveniência unsigned do CI."""

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("candidate_provenance", ROOT / "scripts/candidate-provenance.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class CandidateProvenanceTests(unittest.TestCase):
    def setUp(self):
        private = ROOT / ".runlogs"
        private.mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="provenance-", dir=private)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        archive = b"candidate-archive-fixture"
        (self.directory / "prisma-image.tar.gz").write_bytes(archive)
        self.digest = hashlib.sha256(archive).hexdigest()
        (self.directory / "prisma-image.sha256").write_text(f"{self.digest}  prisma-image.tar.gz\n")
        (self.directory / "migrations.json").write_text(json.dumps({"migrations": []}))
        sbom = {"spdxVersion": "SPDX-2.3", "packages": [{"name": "fixture", "versionInfo": "1.0"}]}
        for name in MODULE.INPUT_FILES[2:]:
            (self.directory / name).write_text(json.dumps(sbom))
        self.environment = {
            "GITHUB_REPOSITORY": "example/prisma",
            "GITHUB_SHA": "a" * 40,
            "GITHUB_WORKFLOW_SHA": "b" * 40,
            "GITHUB_REF": "refs/pull/12/merge",
            "GITHUB_SERVER_URL": "https://github.com",
            "GITHUB_RUN_ID": "123",
            "GITHUB_RUN_ATTEMPT": "2",
            "GITHUB_WORKFLOW_REF": "example/prisma/.github/workflows/ci.yml@refs/pull/12/merge",
            "UNRELATED_SECRET": "never-copy-this-secret",
        }

    def create(self):
        return MODULE.create_statement(self.directory, self.environment)

    def test_subjects_bind_all_four_artifacts_and_keep_workflow_commit_distinct(self):
        statement = self.create()
        subjects = {item["name"]: item["digest"]["sha256"] for item in statement["subject"]}
        self.assertEqual({"prisma-image.tar.gz", "migrations.json", "sbom-runtime.spdx.json", "sbom-frontend.spdx.json"}, set(subjects))
        self.assertEqual(self.digest, subjects["prisma-image.tar.gz"])
        self.assertIn("b" * 40, statement["predicate"]["runDetails"]["builder"]["id"])
        dependency = statement["predicate"]["buildDefinition"]["resolvedDependencies"][0]
        self.assertEqual("a" * 40, dependency["digest"]["gitCommit"])
        self.assertNotIn("never-copy-this-secret", json.dumps(statement))
        self.assertNotIn("UNRELATED_SECRET", json.dumps(statement))

    def test_changed_archive_is_rejected_by_original_checksum(self):
        (self.directory / "prisma-image.tar.gz").write_bytes(b"tampered")
        with self.assertRaisesRegex(ValueError, "checksum"):
            self.create()

    def test_changed_inventory_changes_its_subject_digest(self):
        first = self.create()
        path = self.directory / "sbom-runtime.spdx.json"
        document = json.loads(path.read_text())
        document["packages"][0]["versionInfo"] = "2.0"
        path.write_text(json.dumps(document))
        second = self.create()
        first_hashes = {item["name"]: item["digest"] for item in first["subject"]}
        second_hashes = {item["name"]: item["digest"] for item in second["subject"]}
        self.assertNotEqual(first_hashes["sbom-runtime.spdx.json"], second_hashes["sbom-runtime.spdx.json"])

    def test_missing_inventory_is_rejected(self):
        (self.directory / "sbom-frontend.spdx.json").unlink()
        with self.assertRaises(ValueError):
            self.create()

    def test_empty_inventory_is_rejected(self):
        (self.directory / "sbom-runtime.spdx.json").write_text('{"spdxVersion":"SPDX-2.3","packages":[]}')
        with self.assertRaisesRegex(ValueError, "Inventário"):
            self.create()

    def test_invalid_json_is_rejected(self):
        (self.directory / "migrations.json").write_text("invalid")
        with self.assertRaises(json.JSONDecodeError):
            self.create()

    def test_symbolic_link_artifact_is_rejected(self):
        path = self.directory / "sbom-frontend.spdx.json"
        path.unlink()
        path.symlink_to(self.directory / "sbom-runtime.spdx.json")
        with self.assertRaises(ValueError):
            self.create()

    def test_missing_commit_does_not_invent_source_identity(self):
        del self.environment["GITHUB_SHA"]
        with self.assertRaisesRegex(ValueError, "Commit"):
            self.create()

    def test_foreign_workflow_identity_is_rejected(self):
        self.environment["GITHUB_WORKFLOW_REF"] = "other/repo/.github/workflows/ci.yml@refs/heads/main"
        with self.assertRaisesRegex(ValueError, "Workflow"):
            self.create()

    def test_server_credentials_are_rejected(self):
        self.environment["GITHUB_SERVER_URL"] = "https://user:secret@github.com"
        with self.assertRaisesRegex(ValueError, "servidor"):
            self.create()


if __name__ == "__main__":
    unittest.main()
