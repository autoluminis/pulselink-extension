"""Build a deterministic source SDK archive; excludes build artifacts."""
from pathlib import Path
import hashlib
import json
import zipfile
root = Path(__file__).resolve().parents[2]
source = root / 'sdk/notification'
manifest = json.loads((source / 'contract-manifest.json').read_text(encoding='utf-8'))
actual = {p.relative_to(source / 'src').as_posix(): hashlib.sha256(p.read_bytes().replace(b'\r\n', b'\n')).hexdigest()
          for p in (source / 'src').rglob('*.cs') if not any(part in ('bin', 'obj') for part in p.parts)}
if actual != manifest['files']:
    raise SystemExit('SDK contract sources differ from contract-manifest.json; verify host compatibility before packaging.')
output = root / 'website/assets/PulseLink-Notification-SDK-26.10.15.003.zip'
(output.parent / 'PulseLink-Notification-Development-Guide.md').write_bytes((source / 'DEVELOPING.md').read_bytes().replace(b'\r\n', b'\n'))
with zipfile.ZipFile(output, 'w', zipfile.ZIP_STORED) as archive:
    for path in sorted(source.rglob('*'), key=lambda path: path.relative_to(source).as_posix()):
        if not path.is_file() or any(p in ('bin', 'obj') for p in path.relative_to(source).parts) or path.suffix not in ('.cs', '.csproj', '.props', '.md', '.json'):
            continue
        info = zipfile.ZipInfo('PulseLink-Notification-SDK/' + path.relative_to(source).as_posix(), (2026, 10, 16, 0, 0, 0))
        # Stable bytes across Windows/Linux and different bundled zlib versions.
        info.create_system = 3
        info.compress_type = zipfile.ZIP_STORED
        info.external_attr = 0o644 << 16
        archive.writestr(info, path.read_bytes().replace(b'\r\n', b'\n'))
output.with_suffix('.zip.sha256').write_text(hashlib.sha256(output.read_bytes()).hexdigest() + '  ' + output.name + '\n', encoding='utf-8', newline='\n')
print(output.name)
