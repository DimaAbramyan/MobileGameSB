"""Focused verification: decode, level/seams, duplicate recordings, preservation and HTTP byte ranges."""
import hashlib
import itertools
import json
from pathlib import Path
import subprocess
import sys
import urllib.request

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / 'dist'
WORK = ROOT / '.local/research/flamethrower'
ARCADE = '--arcade' in sys.argv
if ARCADE:
    WORK = ROOT / '.local/research/flamethrower-arcade'
EXPECTED_ROLES = [13, 7, 1] if ARCADE else [16, 4, 1]


def catalog(path): return json.loads(path.read_text(encoding='utf8').split('=', 1)[1].strip().rstrip(';'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    old = catalog(WORK / 'catalog-before.js'); current = catalog(DIST / 'catalog.js')
    by_id = {s['id']: s for s in current['sounds']}
    assert current['catalogId'] == old['catalogId']
    assert all(by_id[s['id']] == s for s in old['sounds']), 'Old entries were modified'
    assert current['categories'][:len(old['categories'])] == old['categories']
    assert current['objects'][:len(old['objects'])] == old['objects']
    baseline = json.loads((WORK / 'baseline.json').read_text())
    selection_file = ROOT / 'data/selections.json'
    assert (sha(selection_file) if selection_file.exists() else None) == baseline['selectionsSha256'], 'Selection bytes changed'
    prefix = 'fta-' if ARCADE else 'ft-'
    sounds = [s for s in current['sounds'] if s['id'].startswith(prefix)]
    assert len(sounds) == len({s['originalSha256'] for s in sounds}) == 21
    assert [sum(s['playbackKind'] == role for s in sounds) for role in ['loop', 'start', 'end']] == EXPECTED_ROLES
    checks = []
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    for sound in sounds:
        for field in ('file', 'originalFile'):
            path = DIST / sound[field]
            result = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-f', 'null', '-'], capture_output=True)
            assert result.returncode == 0, (sound['id'], field, result.stderr)
            expected = sound['sha256'] if field == 'file' else sound['originalSha256']
            assert sha(path) == expected
            request = urllib.request.Request('http://127.0.0.1:8765/' + sound[field], headers={'Range': 'bytes=0-63'})
            with opener.open(request, timeout=10) as response:
                assert response.status == 206 and response.read() == path.read_bytes()[:64]
                assert response.headers['Content-Type'].startswith('audio/')
            checks.append(dict(id=sound['id'], kind=field, decoded=True, hash=True, byteRange=True))
        assert sound['preparation']['after']['truePeakDb'] <= -7.95
    # Correlation searches for the same PCM recording even when gain/starting offset differs.
    loops = [s for s in sounds if s['playbackKind'] == 'loop']; waves = {}
    for sound in loops:
        raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(DIST / sound['file']), '-ac', '1', '-ar', '4000',
                              '-af', 'highpass=f=120', '-f', 'f32le', 'pipe:1'], capture_output=True, check=True).stdout
        waves[sound['id']] = np.frombuffer(raw, dtype='<f4').astype(np.float64)
    highest = []
    for left, right in itertools.combinations(loops, 2):
        a, b = waves[left['id']], waves[right['id']]
        short, long = (a, b) if len(a) < len(b) else (b, a)
        # The middle second avoids the deliberately crossfaded seam.
        short = short[len(short)//2:len(short)//2 + 4000]
        short = short - short.mean()
        size = 1 << (len(long) + len(short) - 1).bit_length()
        correlation = np.fft.irfft(np.fft.rfft(long, size) * np.fft.rfft(short[::-1], size), size)
        valid = correlation[len(short)-1:len(long)]
        energy = np.convolve(long ** 2, np.ones(len(short)), mode='valid')
        score = float(np.max(np.abs(valid) / np.sqrt(np.maximum(energy * np.sum(short ** 2), 1e-20))))
        highest.append(dict(a=left['id'], b=right['id'], correlation=round(score, 4)))
        assert score < .90, ('Possible reused recording', left['id'], right['id'], score)
    report = dict(sounds=len(sounds), loop=EXPECTED_ROLES[0], start=EXPECTED_ROLES[1], end=EXPECTED_ROLES[2], oldEntriesPreserved=len(old['sounds']), selectionsByteIdentical=True,
                  fileChecks=checks, loudnessLoops=[s['preparation']['after']['lufs'] for s in loops],
                  highestCorrelations=sorted(highest, key=lambda x: -x['correlation'])[:5],
                  scope='Technical checks, not an auditory comfort assessment')
    (WORK / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k: report[k] for k in ('sounds', 'loop', 'start', 'end', 'oldEntriesPreserved', 'selectionsByteIdentical', 'highestCorrelations')}, indent=2))


if __name__ == '__main__': main()
