import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
paths = [p for folder in ('Assets/Editor', 'Assets/Scripts/Gameplay/Enemies',
    'Assets/Scripts/Gameplay/EnemyProjectiles', 'Assets/Scripts/Gameplay/Player')
    for p in (root / folder).glob('*.cs')]
target = root / 'AgentArtifacts/laser-existing-source.json'
if target.exists():
    raise RuntimeError('Laser baseline already exists')
target.write_text(json.dumps({str(p.relative_to(root)).replace('\\', '/'): p.read_text(encoding='utf-8-sig')
    for p in paths}, ensure_ascii=False), encoding='utf-8')
print(f'Snapshotted {len(paths)} source files')
