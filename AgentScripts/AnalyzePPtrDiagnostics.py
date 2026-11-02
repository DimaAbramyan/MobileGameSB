"""Summarize preserved PPtr investigation evidence; never changes Unity assets."""
import collections
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1] / 'AgentArtifacts' / 'PPtrDiagnostics'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def summarize():
    original = (ROOT / 'editor-before.log').read_text(encoding='utf-8', errors='replace').splitlines()
    errors = collections.Counter(line for line in original if 'PPtr cast failed' in line)
    builds = []
    for path in sorted(ROOT.glob('*-build.json')):
        if not isinstance(read(path), dict) or 'summary' not in read(path):
            continue
        report = read(path)
        stages = []
        for step in report['steps']:
            messages = collections.Counter(m['content'] for m in step['messages'] if m['type'] == 'Error')
            if messages:
                stages.append({'stage': step['name'], 'messages': dict(messages)})
        settings_path = path.with_name(path.name.replace('-build.json', '-settings.json'))
        settings = read(settings_path) if settings_path.exists() else None
        builds.append({'name': path.stem, **report['summary'], 'errorStages': stages,
                       'settingsRestored': settings['before'] == settings['after'] if settings else None,
                       'pluginFlagsRestored': settings['pluginsRestored'] if settings else None})
    result = {'originalErrors': dict(errors), 'builds': builds}
    (ROOT / 'experiment-summary.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    return result

if __name__ == '__main__':
    print(json.dumps(summarize(), indent=2, ensure_ascii=False))
