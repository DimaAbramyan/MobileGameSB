"""Catalog persistence and recoverable file transfers shared by HTTP and import tools."""
from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import threading
import time
import uuid

CATALOG_ID = 'sci-fi-bullet-hell-sfx'
ROOT = Path(__file__).resolve().parent
LOCK = threading.RLock()
ID = re.compile(r'^[a-z0-9][a-z0-9_-]{0,119}$')
FILE_KEYS = {'file', 'originalFile', 'rapidPreviewFile', 'recipeFile', 'eventsFile', 'evidenceFile', 'attributionFile'}
AUDIO_EXTENSIONS = {'.wav', '.mp3', '.ogg', '.flac', '.aiff', '.aif', '.m4a', '.opus'}


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def atomic_write(path, payload):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temporary.open('xb') as output:
            output.write(payload)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8')


def referenced_files(value):
    result = set()
    if isinstance(value, dict):
        for key, child in value.items():
            if key in FILE_KEYS and isinstance(child, str):
                result.add(child)
            else:
                result.update(referenced_files(child))
    elif isinstance(value, list):
        for child in value:
            result.update(referenced_files(child))
    return result


def rewrite_files(value, mapping):
    if isinstance(value, str):
        return mapping.get(value, value)
    if isinstance(value, list):
        return [rewrite_files(child, mapping) for child in value]
    if isinstance(value, dict):
        return {key: rewrite_files(child, mapping) for key, child in value.items()}
    return value


class CatalogStore:
    def __init__(self, root=ROOT):
        self.root = Path(root).resolve()
        self.dist = self.root / 'dist'
        self.catalog_path = self.dist / 'catalog.js'
        self.archive_path = self.dist / 'Removed/index.json'
        self.journal_path = self.root / 'data/catalog-transaction.json'

    @contextmanager
    def locked(self):
        # A server and command-line import must never overwrite each other's state.
        with LOCK:
            lock_path = self.root / 'data/catalog.lock'
            lock_path.parent.mkdir(parents=True, exist_ok=True)
            with lock_path.open('a+b') as handle:
                handle.seek(0, 2)
                if not handle.tell():
                    handle.write(b'0')
                    handle.flush()
                handle.seek(0)
                deadline = time.monotonic() + 15
                while True:
                    try:
                        if os.name == 'nt':
                            import msvcrt
                            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                        else:
                            import fcntl
                            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
                        break
                    except OSError:
                        if time.monotonic() >= deadline:
                            raise OSError('Catalog is busy; retry the operation')
                        time.sleep(.025)
                try:
                    self._recover()
                    yield
                finally:
                    handle.seek(0)
                    if os.name == 'nt':
                        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
                    else:
                        fcntl.flock(handle, fcntl.LOCK_UN)

    def local(self, relative):
        if not isinstance(relative, str) or '\\' in relative or not relative or ':' in relative:
            raise ValueError('Invalid catalog file path')
        relative_path = Path(relative)
        if relative_path.is_absolute() or '..' in relative_path.parts:
            raise ValueError('Path escapes the catalog')
        path = self.dist / relative_path
        if not path.resolve().is_relative_to(self.dist.resolve()):
            raise ValueError('Path escapes the catalog through a link')
        return path

    def _read(self):
        text = self.catalog_path.read_text(encoding='utf8')
        catalog = json.loads(text.split('window.SOUND_CATALOG =', 1)[1].strip().rstrip(';'))
        if catalog.get('catalogId') != CATALOG_ID or catalog.get('schemaVersion') != 1:
            raise ValueError('Unsupported catalog identity/version')
        archive = (json.loads(self.archive_path.read_text(encoding='utf8')) if self.archive_path.exists()
                   else dict(schemaVersion=1, catalogId=CATALOG_ID, entries={}))
        if archive.get('schemaVersion') != 1 or archive.get('catalogId') != CATALOG_ID or not isinstance(archive.get('entries'), dict):
            raise ValueError('Removed index is damaged; original preserved')
        return catalog, archive

    def _write_catalog(self, catalog):
        atomic_write(self.catalog_path, b'// Stable IDs preserve favorites and notes.\nwindow.SOUND_CATALOG = ' + json_bytes(catalog).rstrip() + b';\n')

    def _write_state(self, catalog, archive):
        self._write_catalog(catalog)
        atomic_write(self.archive_path, json_bytes(archive))

    def _recover(self):
        if not self.journal_path.exists():
            return
        transaction = json.loads(self.journal_path.read_text(encoding='utf8'))
        # An unfinished operation is undone, even if one of the two indexes was written.
        for op in reversed(transaction['operations']):
            source, target = self.local(op['from']), self.local(op['to'])
            partial = target.with_name(target.name + '.copying')
            if op['mode'] == 'copy' and partial.exists():
                if not source.is_file() or sha256(source) != op['sha256']:
                    raise OSError('Recovery source changed: ' + op['from'])
                partial.unlink()  # Unpublished, incomplete transaction copy only.
            if op['mode'] == 'reuse' or not target.exists():
                continue
            if sha256(target) != op['sha256']:
                raise OSError('Recovery blocked by changed file: ' + op['to'])
            if op['mode'] == 'copy':
                # Only the newly created transaction copy is removed, never a user's source.
                target.unlink()
            elif not source.exists():
                source.parent.mkdir(parents=True, exist_ok=True)
                target.rename(source)
            else:
                raise OSError('Recovery destination already exists: ' + op['from'])
        self._write_state(transaction['beforeCatalog'], transaction['beforeArchive'])
        self.journal_path.unlink()

    def _transfer(self, before_catalog, before_archive, after_catalog, after_archive, operations):
        for op in operations:
            source, target = self.local(op['from']), self.local(op['to'])
            if not source.is_file() or sha256(source) != op['sha256']:
                raise OSError('Source missing or changed: ' + op['from'])
            if target.exists() and (op['mode'] != 'reuse' or sha256(target) != op['sha256']):
                raise OSError('Destination already exists: ' + op['to'])
        atomic_write(self.journal_path, json_bytes(dict(beforeCatalog=before_catalog, beforeArchive=before_archive,
                                                       operations=operations)))
        try:
            for op in operations:
                source, target = self.local(op['from']), self.local(op['to'])
                if op['mode'] == 'reuse':
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                if op['mode'] == 'copy':
                    temporary = target.with_name(target.name + '.copying')
                    try:
                        with source.open('rb') as incoming, temporary.open('xb') as output:
                            shutil.copyfileobj(incoming, output)
                            output.flush()
                            os.fsync(output.fileno())
                        temporary.rename(target)
                    finally:
                        if temporary.exists():
                            temporary.unlink()
                else:
                    source.rename(target)
            self._write_state(after_catalog, after_archive)
            # This is the commit point. A crash before it rolls the transfer back on reopening.
            self.journal_path.unlink()
        except Exception:
            self._recover()
            raise

    @staticmethod
    def _view(catalog, archive):
        removed = []
        for entry in archive['entries'].values():
            sound = rewrite_files(entry['sound'], entry['files'])
            sound['removedAt'] = entry['removedAt']
            sound['originalPath'] = entry['sound']['file']
            sound['sourceSnapshotFile'] = entry['files'].get(f'licenses/{sound["id"]}/source.txt')
            removed.append(sound)
        return dict(catalog=catalog, removed=removed)

    def library(self):
        with self.locked():
            catalog, archive = self._read()
            if not self.archive_path.exists():
                atomic_write(self.archive_path, json_bytes(archive))
            return self._view(catalog, archive)

    def change_removed(self, sound_id, removed):
        if not isinstance(sound_id, str) or not ID.fullmatch(sound_id):
            raise ValueError('Invalid sound ID')
        with self.locked():
            before_catalog, before_archive = self._read()
            catalog, archive = deepcopy(before_catalog), deepcopy(before_archive)
            active = {s['id']: s for s in catalog['sounds']}
            if removed and sound_id in archive['entries'] or not removed and sound_id in active:
                return self._view(catalog, archive)  # Idempotent rapid/retried click.
            if removed:
                sound = active.get(sound_id)
                if sound is None:
                    raise KeyError(sound_id)
                obj = next(o for o in catalog['objects'] if o['id'] == sound['objectId'])
                category = next(c for c in catalog['categories'] if c['id'] == obj['categoryId'])
                paths = referenced_files(sound)
                for directory in (f'audio/{category["id"]}/{obj["id"]}/{sound_id}', f'licenses/{sound_id}'):
                    folder = self.local(directory)
                    if folder.is_dir():
                        paths.update(p.relative_to(self.dist).as_posix() for p in folder.rglob('*') if p.is_file())
                shared = set().union(*(referenced_files(s) for s in catalog['sounds'] if s['id'] != sound_id))
                mapping = {p: f'Removed/{sound_id}/files/{p}' for p in sorted(paths)}
                operations = []
                for source, target in mapping.items():
                    digest = sha256(self.local(source))
                    mode = 'copy' if source in shared or source.startswith('recipes/') else 'move'
                    if mode == 'copy' and self.local(target).exists() and sha256(self.local(target)) == digest:
                        mode = 'reuse'
                    operations.append(dict(**{'from': source, 'to': target}, mode=mode, sha256=digest))
                archive['entries'][sound_id] = dict(sound=sound, category=category, object=obj, files=mapping,
                    hashes={op['from']: op['sha256'] for op in operations},
                    removedAt=datetime.now(timezone.utc).isoformat(), originalIndex=catalog['sounds'].index(sound),
                    sharedFiles=[op['from'] for op in operations if op['mode'] != 'move'])
                catalog['sounds'] = [s for s in catalog['sounds'] if s['id'] != sound_id]
            else:
                entry = archive['entries'].get(sound_id)
                if entry is None:
                    raise KeyError(sound_id)
                operations = []
                for target, source in entry['files'].items():
                    mode = 'move'
                    if target in entry['sharedFiles']:
                        mode = 'reuse' if self.local(target).exists() else 'copy'
                    operations.append(dict(**{'from': source, 'to': target}, mode=mode, sha256=entry['hashes'][target]))
                for key, value in [('categories', entry['category']), ('objects', entry['object'])]:
                    if not any(item['id'] == value['id'] for item in catalog[key]):
                        catalog[key].append(value)
                catalog['sounds'].insert(min(entry['originalIndex'], len(catalog['sounds'])), entry['sound'])
                del archive['entries'][sound_id]
            self._transfer(before_catalog, before_archive, catalog, archive, operations)
            return self._view(catalog, archive)

    def import_records(self, records, categories=(), objects=()):
        # Mandatory admission gate. No importer may write catalog.js directly.
        from audio_identity import DuplicateGuard
        with self.locked():
            catalog, archive = self._read()
            guard = DuplicateGuard(self, catalog, archive)
            accepted, rejected = [], []
            for record in records:
                reason = guard.check(record)
                if reason:
                    rejected.append(dict(id=record['id'], **reason))
                else:
                    accepted.append(record)
                    guard.add(record)
            for key, values in [('categories', categories), ('objects', objects)]:
                for value in values:
                    if not any(item['id'] == value['id'] for item in catalog[key]):
                        catalog[key].append(deepcopy(value))
            catalog['sounds'].extend(deepcopy(accepted))
            if accepted:
                catalog['updatedAt'] = datetime.now(timezone.utc).date().isoformat()
                self._write_catalog(catalog)
            return dict(accepted=[s['id'] for s in accepted], rejected=rejected)
