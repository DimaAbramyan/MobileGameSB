"""Conservative source/byte/PCM identity checks, never a genre or timbre classifier.

NumPy + ffmpeg are import-tool dependencies; the catalogue HTTP player stays stdlib-only.
PCM fingerprints are cached by SHA-256, independently of names and archive location.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess
from collections import OrderedDict
from urllib.parse import parse_qsl, unquote, urlencode, urlsplit, urlunsplit

from catalog_store import AUDIO_EXTENSIONS, CatalogStore, ROOT, sha256

RATE = 8000
VERSION = 'pcm-8k-hp35-v1'


def normalized_url(url):
    parts = urlsplit(url or '')
    query = [(k, v) for k, v in parse_qsl(parts.query) if not k.lower().startswith('utm_') and k not in ('fbclid', 'gclid')]
    return urlunsplit(('https', parts.netloc.lower().removeprefix('www.'), unquote(parts.path).rstrip('/'), urlencode(sorted(query)), ''))


def source_keys(sound):
    source = sound.get('source', {})
    if source.get('kind') in ('procedural', 'layered'):
        return set()  # Reusing a component does not make two finished designs identical.
    page, download = normalized_url(source.get('url')), normalized_url(source.get('downloadUrl'))
    keys = set()
    member = source.get('archiveMember')
    if member:
        keys.add(('archive', download, unquote(member).replace('\\', '/').casefold()))
    elif Path(urlsplit(download).path).suffix.lower() in AUDIO_EXTENSIONS:
        keys.add(('download', download))
    match = re.search(r'freesound\.org/people/[^/]+/sounds/(\d+)', page)
    if match:
        keys.add(('freesound', match[1]))
    for key in ('id', 'soundId', 'sourceId'):
        if source.get(key):
            keys.add(('source-id', urlsplit(page).netloc, str(source[key])))
    # A bundle's landing page / ZIP alone must never reject every sound in that bundle.
    if page and download and not member and Path(urlsplit(download).path).suffix.lower() in AUDIO_EXTENSIONS:
        keys.add(('page-and-download', page, download))
    return keys


def metadata_key(sound):
    source = sound.get('source', {})
    normalize = lambda s: re.sub(r'[^\w]+', ' ', str(s).casefold()).strip()
    return normalize(source.get('author')), normalize(source.get('recordingTitle') or sound.get('originalFilename') or sound.get('title'))


def identity_paths(sound):
    # Generated rate demonstrations / shared layer stems are not a finished track's identity.
    paths = [sound.get('originalFile'), sound.get('file')]
    paths += [v.get('file') for v in sound.get('variants', [])]
    return list(dict.fromkeys(p for p in paths if p))


def fingerprint(path, cache):
    import numpy as np
    digest = sha256(path)
    cache = Path(cache)
    cache.mkdir(parents=True, exist_ok=True)
    target = cache / (VERSION + '-' + digest + '.npz')
    if target.exists():
        with np.load(target, allow_pickle=False) as saved:
            return saved['pcm'].astype(np.float64)
    # Limit work on accidentally supplied music/huge files. Full byte hash still covers those.
    result = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-t', '600', '-vn', '-ac', '1', '-ar', str(RATE),
                             '-af', 'highpass=f=35', '-f', 'f32le', 'pipe:1'], capture_output=True, timeout=60, check=True)
    data = np.frombuffer(result.stdout, dtype='<f4').copy()
    if len(data) < 80 or not np.isfinite(data).all():
        raise ValueError('Invalid audio for duplicate check: ' + str(path))
    # The cache is disposable; a partly written cache is never published.
    from catalog_store import atomic_write
    import io
    buffer = io.BytesIO()
    np.savez_compressed(buffer, pcm=data)
    atomic_write(target, buffer.getvalue())
    return data.astype(np.float64)


def pcm_match(left, right):
    """Match the actual waveform with a small time shift; similar spectra are insufficient."""
    import numpy as np
    short, long = (left, right) if len(left) <= len(right) else (right, left)
    # Re-encoding, padding and gain changes are supported. Arbitrary tiny excerpts, pitch/time
    # transformations are deliberately not auto-rejected on approximate spectral similarity.
    if len(short) < RATE * .08 or len(short) / len(long) < .85:
        return None
    short = short - short.mean()
    energy = float(np.sum(short * short))
    if energy < 1e-12:
        return None  # Silence is not proof of the same recording.
    length = 1 << (len(long) + len(short) - 1).bit_length()
    correlation = np.fft.irfft(np.fft.rfft(long, length) * np.fft.rfft(short[::-1], length), length)
    valid = correlation[len(short)-1:len(long)]
    sums = np.concatenate(([0.], np.cumsum(long)))
    squares = np.concatenate(([0.], np.cumsum(long * long)))
    windows_sum = sums[len(short):] - sums[:-len(short)]
    windows_energy = squares[len(short):] - squares[:-len(short)] - windows_sum * windows_sum / len(short)
    scores = np.abs(valid) / np.sqrt(np.maximum(windows_energy * energy, 1e-24))
    offset = int(np.argmax(scores))
    score = float(scores[offset])
    # Verify disjoint chunks too: a common attack or long steady note alone is not enough.
    aligned = long[offset:offset + len(short)]
    chunk_scores = []
    for a, b in zip(np.array_split(short, 4), np.array_split(aligned, 4)):
        a, b = a - a.mean(), b - b.mean()
        denominator = np.sqrt(np.sum(a * a) * np.sum(b * b))
        if denominator > 1e-9:
            chunk_scores.append(float(abs(np.sum(a * b)) / denominator))
    if score >= .985 and len(chunk_scores) >= 3 and min(chunk_scores) >= .975:
        return dict(correlation=round(score, 6), offsetSeconds=round(offset / RATE, 6),
                    coverage=round(len(short) / len(long), 6), method=VERSION)
    return None


class DuplicateGuard:
    def __init__(self, store, catalog=None, archive=None):
        self.store = store
        if catalog is None:
            with store.locked():
                catalog, archive = store._read()
        self.entries = []
        self.cache = store.root / 'data/audio-fingerprints'
        self.decoded = OrderedDict()
        self.decoded_bytes = 0
        for record in catalog['sounds']:
            self.add(record)
        for entry in archive['entries'].values():
            self.add(entry['sound'], 'removed', entry['files'])

    def add(self, sound, state='active', mapping=None):
        self.entries.append(dict(sound=sound, state=state, mapping=mapping or {},
                                 sources=source_keys(sound), metadata=metadata_key(sound)))

    def check(self, sound, media_paths=None):
        keys, metadata = source_keys(sound), metadata_key(sound)
        paths = list(map(Path, media_paths)) if media_paths is not None else [self.store.local(p) for p in identity_paths(sound)]
        for entry in self.entries:
            existing = entry['sound']
            reason = None
            if sound.get('id') == existing['id']:
                reason = 'stable-id'
            elif keys & entry['sources']:
                reason = 'source-identity'
            if reason:
                return dict(reason=reason, matchedId=existing['id'], state=entry['state'])
        # Always hash actual bytes, not only self-reported metadata from a candidate.
        hashes = {sha256(p) for p in paths}
        for entry in self.entries:
            existing = entry['sound']
            reason = None
            old_hashes = {existing.get('sha256'), existing.get('originalSha256')}
            old_hashes.update(v.get('sha256') for v in existing.get('variants', []))
            if hashes & (old_hashes - {None}):
                reason = 'sha256'
            if reason:
                return dict(reason=reason, matchedId=existing['id'], state=entry['state'])
        for path in paths:
            a = self._decode(path)
            for entry in self.entries:
                for relative in identity_paths(entry['sound']):
                    existing_path = self.store.local(entry['mapping'].get(relative, relative))
                    match = pcm_match(a, self._decode(existing_path))
                    if match:
                        return dict(reason='audio-content', matchedId=entry['sound']['id'], state=entry['state'],
                                    metadataHint=metadata == entry['metadata'], **match)
        return None

    def _decode(self, path):
        digest = sha256(path)
        if digest not in self.decoded:
            data = fingerprint(path, self.cache)
            while self.decoded and self.decoded_bytes + data.nbytes > 128 * 1024 * 1024:
                _, evicted = self.decoded.popitem(last=False)
                self.decoded_bytes -= evicted.nbytes
            self.decoded[digest] = data
            self.decoded_bytes += data.nbytes
        self.decoded.move_to_end(digest)
        return self.decoded[digest]


class ImportGate:
    """Preflight before creating any active files; commit repeats checks under the store lock."""
    def __init__(self, root=ROOT):
        self.store = CatalogStore(root)
        self.guard = DuplicateGuard(self.store)

    def allow(self, sound_id, source_path, info=None):
        info = info or {}
        gdc = bool(info.get('path'))
        source = dict(url='https://sonniss.com/gameaudiogdc/' if gdc else info.get('url', ''),
                      downloadUrl=info.get('url', '') if gdc else info.get('downloadUrl') or next(iter(info.get('previews', [])), ''),
                      author=info.get('author', ''), archiveMember=info.get('path') or info.get('archiveMember'))
        record = dict(id=sound_id, title=info.get('title', ''), source=source)
        reason = self.guard.check(record, [source_path])
        if reason:
            print(f'SKIP {sound_id}: {json.dumps(reason, ensure_ascii=False)}', flush=True)
            return False
        return True

    def known_id(self, sound_id):
        return next((e['state'] for e in self.guard.entries if e['sound']['id'] == sound_id), None)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('file', type=Path)
    parser.add_argument('--id', default='candidate')
    parser.add_argument('--source-url', default='')
    parser.add_argument('--download-url', default='')
    parser.add_argument('--archive-member', default='')
    parser.add_argument('--title', default='')
    args = parser.parse_args()
    guard = DuplicateGuard(CatalogStore())
    match = guard.check(dict(id=args.id, title=args.title, source=dict(url=args.source_url,
        downloadUrl=args.download_url, archiveMember=args.archive_member)), [args.file])
    print(json.dumps(dict(allowed=match is None, match=match), ensure_ascii=False, indent=2))
    raise SystemExit(2 if match else 0)
