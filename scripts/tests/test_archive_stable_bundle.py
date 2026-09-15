"""Vérifier l'archivage par version réellement embarquée, sur des bundles factices."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest
import zipfile

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
PWSH = shutil.which("pwsh") or str(
    Path(os.environ.get("USERPROFILE", str(Path.home()))) / ".cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe"
)
MANIFEST = '<Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle"><Identity Name="test" Publisher="CN=test" Version="{}"/></Bundle>'


class ArchiveStableBundleTests(unittest.TestCase):
    def invoke(self, bundle, archive, read=False):
        args = [PWSH, "-NoProfile", "-File", str(ROOT / "scripts/tests/Invoke-ArchiveTest.ps1"),
                "-BundlePath", str(bundle), "-ArchiveRoot", str(archive)]
        if read:
            args.append("-ReadVersion")
        return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", timeout=30)

    def bundle(self, directory, version):
        path = Path(directory) / "AZERTYGlobal.msixbundle"
        with zipfile.ZipFile(path, "w") as zipped:
            zipped.writestr("AppxMetadata/AppxBundleManifest.xml", MANIFEST.format(version))
        return path

    def test_archive_version_110_pendant_preparation_120_et_conserve_stable(self):
        with TemporaryDirectory(prefix="azerty-archive-") as directory:
            bundle = self.bundle(directory, "1.1.0.0")
            original = bundle.read_bytes()
            archives = Path(directory) / "archives"
            result = self.invoke(bundle, archives)
            self.assertEqual(0, result.returncode, result.stderr)
            proof = json.loads(result.stdout)
            backup = Path(proof["Path"])
            self.assertEqual(archives / "by-version/1.1.0.0", backup.parent)
            self.assertEqual("1.1.0.0", proof["Version"])
            self.assertEqual(hashlib.sha256(original).hexdigest().upper(), proof["SHA256"])
            self.assertEqual(original, backup.read_bytes())
            self.assertEqual(original, bundle.read_bytes())
            self.assertEqual(proof, json.loads(Path(str(backup) + ".json").read_text(encoding="utf-8")))
            self.assertFalse((archives / "by-version/1.2.0.0").exists())
            again = self.invoke(bundle, archives)
            self.assertEqual(0, again.returncode, again.stderr)
            self.assertNotEqual(proof["Path"], json.loads(again.stdout)["Path"])
            self.assertEqual(original, backup.read_bytes())

    def test_refuse_version_invalide_sans_toucher_au_stable(self):
        for version in ("", "1.2", "../1.2.0.0", "1.2.0.65536"):
            with self.subTest(version=version), TemporaryDirectory(prefix="azerty-archive-") as directory:
                bundle = self.bundle(directory, version)
                original = bundle.read_bytes()
                archives = Path(directory) / "archives"
                result = self.invoke(bundle, archives)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(original, bundle.read_bytes())
                self.assertFalse(archives.exists())

    def test_refuse_manifeste_absent_et_zip_corrompu(self):
        for corrupt in (False, True):
            with self.subTest(corrupt=corrupt), TemporaryDirectory(prefix="azerty-archive-") as directory:
                bundle = Path(directory) / "invalid.msixbundle"
                if corrupt:
                    bundle.write_bytes(b"archive incomplete")
                else:
                    with zipfile.ZipFile(bundle, "w"):
                        pass
                original = bundle.read_bytes()
                archives = Path(directory) / "archives"
                result = self.invoke(bundle, archives)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(original, bundle.read_bytes())
                self.assertFalse(archives.exists())


if __name__ == "__main__":
    unittest.main()

