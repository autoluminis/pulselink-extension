"""Validate the downloadable artifact, not only the working source tree."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SDK = ROOT / 'sdk/notification'
ZIP = ROOT / 'website/assets/PulseLink-Notification-SDK-26.10.17.001.zip'


class SdkArchiveTests(unittest.TestCase):
    def test_checksum_and_contents(self):
        self.assertEqual(ZIP.with_suffix('.zip.sha256').read_text().split()[0], hashlib.sha256(ZIP.read_bytes()).hexdigest())
        with zipfile.ZipFile(ZIP) as archive:
            names = archive.namelist()
            self.assertEqual(names, sorted(names), 'Archive order must use case-sensitive POSIX paths on every OS')
            for required in ['README.md', 'DEVELOPING.md', 'Directory.Build.props', 'examples/plugin.json', 'examples/marketplace.json', 'tests/Program.cs']:
                self.assertIn('PulseLink-Notification-SDK/' + required, names)
            for name in names:
                info = archive.getinfo(name)
                self.assertEqual(info.create_system, 3)
                self.assertEqual(info.compress_type, zipfile.ZIP_STORED)
                relative = name.removeprefix('PulseLink-Notification-SDK/')
                self.assertFalse(set(Path(relative).parts) & {'bin', 'obj', '..'})
                self.assertIn(Path(relative).suffix, {'.cs', '.csproj', '.props', '.md', '.json'})
                self.assertEqual(archive.read(name), (SDK / relative).read_bytes().replace(b'\r\n', b'\n'))

    def test_frozen_contract_hashes(self):
        manifest = json.loads((SDK / 'contract-manifest.json').read_text())
        with zipfile.ZipFile(ZIP) as archive:
            actual = {name.removeprefix('PulseLink-Notification-SDK/src/'): hashlib.sha256(archive.read(name)).hexdigest()
                      for name in archive.namelist() if name.startswith('PulseLink-Notification-SDK/src/') and name.endswith('.cs')}
        self.assertEqual(actual, manifest['files'])

    def test_deterministic_package_and_guide(self):
        before = ZIP.read_bytes()
        subprocess.run([sys.executable, str(ROOT / 'sdk/tools/package-notification-sdk.py')], check=True, capture_output=True)
        self.assertEqual(before, ZIP.read_bytes())
        self.assertEqual((ZIP.parent / 'PulseLink-Notification-Development-Guide.md').read_bytes(), (SDK / 'DEVELOPING.md').read_bytes().replace(b'\r\n', b'\n'))


if __name__ == '__main__':
    unittest.main()
