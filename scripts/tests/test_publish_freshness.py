"""Vérifier le refus d'empaqueter un publish plus ancien que les sources (bloquant B1, 2026-09-20)."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
PWSH = shutil.which("pwsh") or str(
    Path(os.environ.get("USERPROFILE", str(Path.home()))) / ".cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe"
)

PUBLISH = 1_000_000.0
AVANT = PUBLISH - 60
APRES = PUBLISH + 60


class PublishFreshnessTests(unittest.TestCase):
    def arbre(self, directory, tardifs=()):
        """Un src/ minimal dont tout date d'avant le publish, sauf les chemins nommés."""
        src = Path(directory) / "src"
        fichiers = [
            "Program.cs",
            "AZERTYGlobal.csproj",
            "lessons.json",
            "TypingEngine.Core/KeyMapper.cs",
            "bin/Release/AZERTY Global.dll",
            "obj/Debug/AssemblyInfo.cs",
            "AZERTYGlobal.Tests/KeyMapperTests.cs",
            "TestSupport/FakeWin32Api.cs",
        ]
        for relatif in fichiers:
            chemin = src / relatif
            chemin.parent.mkdir(parents=True, exist_ok=True)
            chemin.write_text("x", encoding="utf-8")
            horodatage = APRES if relatif in tardifs else AVANT
            os.utime(chemin, (horodatage, horodatage))

        exe = Path(directory) / "publish" / "AZERTY Global.exe"
        exe.parent.mkdir(parents=True, exist_ok=True)
        exe.write_text("binaire", encoding="utf-8")
        os.utime(exe, (PUBLISH, PUBLISH))
        return src, exe

    def invoke(self, src, exe, assert_mode=False, autoriser_perime=False):
        args = [PWSH, "-NoProfile", "-File", str(ROOT / "scripts/tests/Invoke-FreshnessTest.ps1"),
                "-SourceRoot", str(src), "-ReferencePath", str(exe)]
        if assert_mode:
            args.append("-Assert")
        env = dict(os.environ)
        if autoriser_perime:
            env["AZERTYGLOBAL_ALLOW_STALE_PUBLISH"] = "1"
        else:
            env.pop("AZERTYGLOBAL_ALLOW_STALE_PUBLISH", None)
        return subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                              errors="replace", timeout=60, env=env)

    def test_publish_plus_recent_que_tout_le_pack_est_autorise(self):
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory)
            self.assertEqual([], json.loads(self.invoke(src, exe).stdout))
            resultat = self.invoke(src, exe, assert_mode=True)
            self.assertEqual(0, resultat.returncode, resultat.stderr)
            self.assertIn("PACK-AUTORISE", resultat.stdout)

    def test_une_source_modifiee_apres_le_publish_bloque_le_pack_et_se_nomme(self):
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory, tardifs=("TypingEngine.Core/KeyMapper.cs",))
            perimes = json.loads(self.invoke(src, exe).stdout)
            self.assertEqual(1, len(perimes))
            self.assertTrue(perimes[0].endswith("KeyMapper.cs"), perimes)

            resultat = self.invoke(src, exe, assert_mode=True)
            self.assertNotEqual(0, resultat.returncode)
            self.assertNotIn("PACK-AUTORISE", resultat.stdout)
            # Le message doit nommer le fichier et la commande de sortie, pas seulement compter.
            self.assertIn("KeyMapper.cs", resultat.stderr)
            self.assertIn("dotnet publish -c Release -r win-x64", resultat.stderr)

    def test_la_ressource_embarquee_compte_autant_que_le_code(self):
        # lessons.json a dérivé sans que personne ne republie : c'est exactement B1.
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory, tardifs=("lessons.json",))
            perimes = json.loads(self.invoke(src, exe).stdout)
            self.assertEqual(1, len(perimes))
            self.assertTrue(perimes[0].endswith("lessons.json"), perimes)

    def test_temoins_negatifs_bin_obj_tests_et_testsupport_ne_bloquent_pas(self):
        # Ces quatre-là ne partent pas dans le binaire publié : les compter rendrait le
        # refus si bruyant qu'il serait contourné par réflexe.
        tardifs = (
            "bin/Release/AZERTY Global.dll",
            "obj/Debug/AssemblyInfo.cs",
            "AZERTYGlobal.Tests/KeyMapperTests.cs",
            "TestSupport/FakeWin32Api.cs",
        )
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory, tardifs=tardifs)
            self.assertEqual([], json.loads(self.invoke(src, exe).stdout))
            self.assertEqual(0, self.invoke(src, exe, assert_mode=True).returncode)

    def test_l_echappatoire_degrade_le_refus_en_avertissement(self):
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory, tardifs=("Program.cs",))
            self.assertNotEqual(0, self.invoke(src, exe, assert_mode=True).returncode)
            resultat = self.invoke(src, exe, assert_mode=True, autoriser_perime=True)
            self.assertEqual(0, resultat.returncode, resultat.stderr)
            self.assertIn("PACK-AUTORISE", resultat.stdout)
            self.assertIn("assum", resultat.stderr.lower())

    def test_une_reference_absente_leve_au_lieu_de_laisser_passer(self):
        with TemporaryDirectory(prefix="azerty-freshness-") as directory:
            src, exe = self.arbre(directory)
            exe.unlink()
            resultat = self.invoke(src, exe, assert_mode=True)
            self.assertNotEqual(0, resultat.returncode)
            self.assertNotIn("PACK-AUTORISE", resultat.stdout)


if __name__ == "__main__":
    unittest.main()
