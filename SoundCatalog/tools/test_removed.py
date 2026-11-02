"""Isolated archive/admission regressions. Does not remove real catalogue tracks."""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import threading
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from catalog_store import CATALOG_ID, CatalogStore, atomic_write, json_bytes, sha256
from audio_identity import DuplicateGuard, ImportGate, pcm_match, source_keys
from serve import CatalogServer


class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.root = (ROOT / '.local/tests/removed' / uuid.uuid4().hex).resolve()
        assert self.root.is_relative_to((ROOT / '.local/tests/removed').resolve())
        self.root.mkdir(parents=True)
        self.store = CatalogStore(self.root)
        self.records = [self.sound('alpha', 14), self.sound('beta', 81, 'flamethrower')]
        self.catalog = dict(schemaVersion=1, catalogId=CATALOG_ID,
            categories=[dict(id='weapons', name='Minigun'), dict(id='flamethrower', name='Flamethrower')],
            objects=[dict(id='weapons-object', categoryId='weapons', name='Minigun'),
                     dict(id='flamethrower-object', categoryId='flamethrower', name='Flamethrower')],
            sounds=self.records)
        self.store._write_catalog(self.catalog)
        self.notes = self.root / 'data/selections.json'
        atomic_write(self.notes, json_bytes(dict(favorite='user data', note='Never reset')))
        self.notes_bytes = self.notes.read_bytes()

    def sound(self, sid, seed, category='weapons'):
        relative = f'audio/{category}/{category}-object/{sid}/originals/source.wav'
        path = self.store.local(relative)
        path.parent.mkdir(parents=True)
        # Test fixture only: distinct recordings with the same broad genre / spectrum.
        data = np.random.default_rng(seed).normal(0, .13, 16000)
        data *= np.linspace(.5, 1, len(data))
        with wave.open(str(path), 'wb') as output:
            output.setparams((1, 2, 16000, 0, 'NONE', 'not compressed'))
            output.writeframes((data * 32767).astype('<i2').tobytes())
        license_path = self.store.local(f'licenses/{sid}/license.txt')
        license_path.parent.mkdir(parents=True)
        license_path.write_text('CC0 fixture', encoding='utf8')
        self.store.local(f'licenses/{sid}/source.txt').write_text('Test fixture provenance', encoding='utf8')
        return dict(id=sid, objectId=f'{category}-object', title='Arcade Energy', file=relative,
            originalFile=relative, sha256=sha256(path), originalSha256=sha256(path),
            originalFilename='source.wav', source=dict(name='Test', author='Fixture author',
                url=f'https://freesound.org/people/test/sounds/{seed}/', downloadUrl=f'https://example.test/{seed}.wav'),
            license=dict(id='CC0', name='CC0', url='https://creativecommons.org/publicdomain/zero/1.0/',
                commercialUse=True, evidenceFile=f'licenses/{sid}/license.txt'))

    def test_remove_restore_restart_all_categories_and_notes(self):
        for sound in self.records:
            before = self.store.local(sound['file']).read_bytes()
            view = self.store.change_removed(sound['id'], True)
            self.assertNotIn(sound['id'], [s['id'] for s in view['catalog']['sounds']])
            archived = next(s for s in view['removed'] if s['id'] == sound['id'])
            self.assertFalse(self.store.local(sound['file']).exists())
            self.assertEqual(self.store.local(archived['file']).read_bytes(), before)
            self.assertEqual(archived['source'], sound['source'])
            self.assertTrue(archived['removedAt'])
            self.assertEqual(len(CatalogStore(self.root).library()['removed']), 1)
            restored = CatalogStore(self.root).change_removed(sound['id'], False)
            self.assertFalse(restored['removed'])
            self.assertEqual(self.store.local(sound['file']).read_bytes(), before)
            self.assertEqual(next(s for s in restored['catalog']['sounds'] if s['id'] == sound['id']), sound)
        self.assertEqual(self.notes.read_bytes(), self.notes_bytes)

    def test_rapid_repeated_requests_are_idempotent(self):
        with ThreadPoolExecutor(max_workers=4) as pool:
            list(pool.map(lambda _: self.store.change_removed('alpha', True), range(4)))
        self.assertEqual(len(self.store.library()['removed']), 1)
        self.store.change_removed('alpha', False)
        self.store.change_removed('alpha', False)
        self.assertEqual(len(self.store.library()['catalog']['sounds']), 2)

    def test_failure_during_move_rolls_back_without_loss(self):
        original_rename = Path.rename
        calls = []
        def fail_once(source, destination):
            calls.append(source)
            if len(calls) == 2:
                raise PermissionError('Simulated locked audio')
            return original_rename(source, destination)
        with patch.object(Path, 'rename', fail_once):
            with self.assertRaises(PermissionError):
                self.store.change_removed('alpha', True)
        self.assertEqual(self.store.library()['catalog'], self.catalog)
        self.assertFalse(self.store.library()['removed'])
        self.assertEqual(sha256(self.store.local(self.records[0]['file'])), self.records[0]['sha256'])

    def test_crash_recovery_after_partial_index_commit(self):
        # Simulate power loss after files moved and active index written, before archive commit.
        before_catalog, before_archive = self.store._read()
        def crash_after_catalog(catalog, archive):
            self.store._write_catalog(catalog)
            raise KeyboardInterrupt('Simulated process death')
        with patch.object(self.store, '_write_state', crash_after_catalog):
            with self.assertRaises(KeyboardInterrupt):
                self.store.change_removed('alpha', True)
        self.assertTrue(self.store.journal_path.exists())
        recovered = CatalogStore(self.root).library()
        self.assertEqual(recovered['catalog'], before_catalog)
        self.assertFalse(recovered['removed'])
        self.assertFalse(self.store.journal_path.exists())
        self.assertTrue(self.store.local(self.records[0]['file']).exists())

    def test_restore_collision_keeps_archive_intact(self):
        self.store.change_removed('alpha', True)
        conflict = self.store.local(self.records[0]['file'])
        conflict.write_bytes(b'User file at the original path')
        with self.assertRaises(OSError):
            self.store.change_removed('alpha', False)
        self.assertEqual(conflict.read_bytes(), b'User file at the original path')
        self.assertEqual(len(self.store.library()['removed']), 1)

    def test_failure_writing_metadata_rolls_back_files_and_indexes(self):
        original_write = self.store._write_state
        calls = []
        def fail_once(catalog, archive):
            calls.append(True)
            if len(calls) == 1:
                self.store._write_catalog(catalog)
                raise OSError('Simulated disk-full while writing archive index')
            return original_write(catalog, archive)
        with patch.object(self.store, '_write_state', fail_once):
            with self.assertRaises(OSError): self.store.change_removed('alpha', True)
        self.assertEqual(self.store.library()['catalog'], self.catalog)
        self.assertFalse(self.store.journal_path.exists())

    def test_partial_copy_failure_leaves_shared_source_untouched(self):
        catalog, _ = self.store._read()
        catalog['sounds'][1]['originalFile'] = catalog['sounds'][0]['file']
        self.store._write_catalog(catalog)
        def fail_copy(incoming, output):
            output.write(incoming.read(64))
            raise OSError('Simulated full disk')
        with patch('catalog_store.shutil.copyfileobj', fail_copy):
            with self.assertRaises(OSError): self.store.change_removed('alpha', True)
        self.assertEqual(self.store.library()['catalog'], catalog)
        self.assertFalse(list((self.root / 'dist/Removed').rglob('*.copying')))
        self.assertEqual(sha256(self.store.local(self.records[0]['file'])), self.records[0]['sha256'])

    def test_shared_source_is_archived_without_breaking_other_track(self):
        catalog, _ = self.store._read()
        catalog['sounds'][1]['originalFile'] = catalog['sounds'][0]['file']
        catalog['sounds'][1]['originalSha256'] = catalog['sounds'][0]['sha256']
        self.store._write_catalog(catalog)
        view = self.store.change_removed('alpha', True)
        self.assertTrue(self.store.local(catalog['sounds'][1]['originalFile']).exists())
        self.assertTrue(self.store.local(view['removed'][0]['file']).exists())
        self.store.change_removed('alpha', False)
        self.store.change_removed('alpha', True)
        self.store.change_removed('alpha', False)

    def test_guard_ids_urls_bytes_renamed_and_reencoded_audio(self):
        original = self.store.local(self.records[0]['file'])
        candidate = deepcopy(self.records[0])
        candidate['id'] = 'new-title'
        self.store.change_removed('alpha', True)
        guard = DuplicateGuard(self.store)
        archived = self.store.local(self.store.library()['removed'][0]['file'])
        reason = guard.check(candidate, [archived])
        self.assertEqual(reason['reason'], 'source-identity')
        self.assertEqual(reason['state'], 'removed')
        candidate['source'] = dict(name='Another website', author='Different upload', url='https://other.test/sound/2')
        renamed = self.root / 'renamed.wav'
        shutil.copyfile(archived, renamed)
        self.assertEqual(guard.check(candidate, [renamed])['reason'], 'sha256')
        flac = self.root / 'totally-different.flac'
        subprocess.run(['ffmpeg', '-v', 'error', '-i', str(archived), '-af', 'volume=0.6', '-ar', '44100', str(flac)], check=True)
        result = guard.check(candidate, [flac])
        self.assertEqual(result['reason'], 'audio-content')
        self.assertGreater(result['correlation'], .985)
        mp3 = self.root / 'different-name.mp3'
        subprocess.run(['ffmpeg', '-v', 'error', '-i', str(archived), '-c:a', 'libmp3lame', '-b:a', '192k', str(mp3)], check=True)
        self.assertEqual(guard.check(candidate, [mp3])['reason'], 'audio-content')
        # Similar spectrum, identical name/author, different noise recording: allow it.
        distinct = deepcopy(self.records[1]); distinct['id'] = 'unrelated'
        distinct['source'] = dict(candidate['source'], author='Fixture author'); distinct['title'] = candidate['title']
        archive_only = DuplicateGuard(self.store, dict(sounds=[]), self.store._read()[1])
        self.assertIsNone(archive_only.check(distinct, [self.store.local(distinct['file'])]))
        self.assertFalse(original.exists())

    def test_import_gate_rechecks_removed_and_active_duplicates(self):
        self.store.change_removed('alpha', True)
        blocked = deepcopy(self.records[0])
        archived = self.store.local(self.store.library()['removed'][0]['file'])
        # A stale importer copies a rejected file back before calling commit. Commit still rejects.
        destination = self.store.local(blocked['file'])
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(archived, destination)
        result = self.store.import_records([blocked, self.records[1]])
        self.assertEqual(result['accepted'], [])
        self.assertEqual({r['state'] for r in result['rejected']}, {'active', 'removed'})
        self.assertEqual(len(self.store.library()['catalog']['sounds']), 1)
        gate = ImportGate(self.root)
        self.assertFalse(gate.allow('alpha', archived))

    def test_bundle_url_does_not_reject_unrelated_members(self):
        a = dict(source=dict(url='https://sonniss.com/gameaudiogdc/', downloadUrl='https://example.test/bundle.zip', archiveMember='A.wav'))
        b = dict(source=dict(url='https://sonniss.com/gameaudiogdc/', downloadUrl='https://example.test/bundle.zip', archiveMember='B.wav'))
        self.assertFalse(source_keys(a) & source_keys(b))

    def test_admit_new_record_and_filter_same_batch_duplicate(self):
        fresh = self.sound('gamma', 139)
        duplicate = deepcopy(fresh); duplicate['id'] = 'delta'
        result = self.store.import_records([fresh, duplicate])
        self.assertEqual(result['accepted'], ['gamma'])
        self.assertEqual(result['rejected'][0]['matchedId'], 'gamma')
        self.assertEqual(len(self.store.library()['catalog']['sounds']), 3)

    def test_http_archive_playback_restart_and_invalid_path(self):
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        for restore in (False, True):
            server = CatalogServer(0, root=self.root)
            thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
            url = f'http://127.0.0.1:{server.server_port}'
            try:
                if not restore:
                    request = urllib.request.Request(url + '/api/remove', data=b'{"id":"alpha"}', headers={'Content-Type':'application/json'})
                    with opener.open(request) as response: library = json.load(response)
                else:
                    with opener.open(url + '/api/library') as response: library = json.load(response)
                self.assertEqual(len(library['removed']), 1)
                sound = library['removed'][0]
                request = urllib.request.Request(url + '/' + sound['file'], headers={'Range':'bytes=0-63'})
                with opener.open(request) as response:
                    self.assertEqual(response.status, 206)
                    self.assertEqual(len(response.read()), 64)
                if restore:
                    request = urllib.request.Request(url + '/api/restore', data=b'{"id":"alpha"}', headers={'Content-Type':'application/json'})
                    with opener.open(request) as response: self.assertFalse(json.load(response)['removed'])
                bad = urllib.request.Request(url + '/api/remove', data=b'{"id":"../escape"}', headers={'Content-Type':'application/json'})
                with self.assertRaises(urllib.error.HTTPError): opener.open(bad)
            finally:
                server.shutdown(); server.server_close(); thread.join(timeout=3)


if __name__ == '__main__':
    unittest.main(verbosity=2)
