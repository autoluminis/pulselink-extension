"""Build a deterministic source SDK archive; excludes build artifacts."""
from pathlib import Path
import hashlib
import zipfile
root = Path(__file__).resolve().parents[2]
source = root / 'sdk/notification'
output = root / 'website/assets/PulseLink-Notification-SDK-26.10.15.003.zip'
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(source.rglob('*')):
        if not path.is_file() or any(p in ('bin', 'obj', '.gitignore') for p in path.relative_to(source).parts):
            continue
        info = zipfile.ZipInfo('PulseLink-Notification-SDK/' + path.relative_to(source).as_posix(), (2026, 10, 16, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o644 << 16
        archive.writestr(info, path.read_bytes())
output.with_suffix('.zip.sha256').write_text(hashlib.sha256(output.read_bytes()).hexdigest() + '  ' + output.name + '\n')
print(output.name)
