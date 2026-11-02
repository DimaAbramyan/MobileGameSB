// The real transfer handler is run with deferred HTTP, without requiring a browser package.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const code = fs.readFileSync(new URL('../dist/app.js', import.meta.url), 'utf8');
const start = code.indexOf('  function transferTrack(');
const handler = code.slice(start, code.indexOf('\n  function ', start + 10));
assert.ok(handler.includes('pendingTransfers'));

function harness() {
  const requests = [], frames = [], errors = [], released = [], selected = [], played = [];
  const context = {
    libraryAvailable: true, pendingTransfers: new Set(), transferChain: Promise.resolve(),
    visibleSounds: [], activeId: null, players: new Map(), automatic: { selected: null },
    selectAutoCandidate: id => { selected.push(id); context.automatic.selected = { id }; },
    playSound: id => played.push(id), updatePlayer() {},
    releaseTrack: id => released.push(id), render: () => frames.push([...context.pendingTransfers]),
    toast: text => errors.push(text), applyLibrary: library => { context.latest = library; },
    fetch: (url, options) => new Promise((resolve, reject) => requests.push({ url, options, resolve, reject }))
  };
  vm.createContext(context); vm.runInContext(handler, context);
  return { context, requests, frames, errors, released, selected, played };
}
const settle = async () => { for (let i = 0; i < 15; i++) await Promise.resolve(); };
{
  const h = harness();
  h.context.transferTrack('alpha', true);
  assert.deepEqual(h.frames[0], ['alpha'], 'Hide synchronously, before disk/HTTP finishes');
  assert.deepEqual(h.released, ['alpha'], 'Release playback immediately');
  h.context.transferTrack('alpha', true);
  await settle();
  assert.equal(h.requests.length, 1, 'Double click must not submit twice');
  h.requests[0].resolve({ ok: true, json: async () => ({ removed: ['alpha'] }) });
  await h.context.transferChain;
  assert.equal(h.context.pendingTransfers.size, 0);
  assert.deepEqual(h.context.latest, { removed: ['alpha'] });
}
{
  const h = harness();
  h.context.transferTrack('alpha', true); await settle();
  h.requests[0].resolve({ ok: false, json: async () => ({ error: 'File locked' }) });
  await h.context.transferChain;
  assert.equal(h.context.pendingTransfers.size, 0, 'Failure must return track to the visible list');
  assert.equal(h.errors.length, 1);
  assert.match(h.errors[0], /File locked/);
  h.context.transferTrack('beta', false); await settle();
  h.requests[1].resolve({ ok: true, json: async () => ({ restored: ['beta'] }) });
  await h.context.transferChain;
  assert.equal(h.requests[1].url, '/api/restore', 'An earlier failure must not block restoration');
}
{
  const h = harness();
  h.context.transferTrack('alpha', true); h.context.transferTrack('beta', true); await settle();
  assert.equal(h.requests.length, 1, 'Serialize commits to avoid old responses overwriting a newer index');
  assert.equal(h.context.pendingTransfers.size, 2, 'Both cards disappear immediately');
  h.requests[0].resolve({ ok: true, json: async () => ({ version: 1 }) }); await settle();
  assert.equal(h.requests.length, 2);
  h.requests[1].resolve({ ok: true, json: async () => ({ version: 2 }) });
  await h.context.transferChain;
  assert.equal(h.context.latest.version, 2);
}
{
  const h = harness();
  h.context.visibleSounds = [{ id: 'favorite' }, { id: 'current' }, { id: 'next' }];
  h.context.automatic.selected = { id: 'current' };
  h.context.activeId = 'current';
  h.context.players.set('current', { paused: false, loop: true });
  h.context.players.set('next', {});
  h.context.transferTrack('current', true);
  assert.deepEqual(h.selected, ['next'], 'Advance in the current list, not to the first/favorite candidate');
  assert.deepEqual(h.played, ['next'], 'Continue normal playback');
  assert.equal(h.context.players.get('next').loop, true);
  assert.equal(h.context.activeId, 'next');
  await settle();
  h.requests[0].resolve({ ok: false, json: async () => ({ error: 'File locked' }) });
  await h.context.transferChain;
  assert.deepEqual(h.selected, ['next'], 'A failed move must not rewind selection');
}
{
  const h = harness();
  h.context.visibleSounds = [{ id: 'first' }, { id: 'pending' }, { id: 'last' }];
  h.context.pendingTransfers.add('pending');
  h.context.automatic.selected = { id: 'last' };
  h.context.activeId = 'last';
  h.context.players.set('last', { paused: true, loop: false });
  h.context.players.set('first', {});
  h.context.transferTrack('last', true);
  assert.deepEqual(h.selected, ['first'], 'Wrap from the end of the filtered list');
  assert.equal(h.context.activeId, 'first');
  assert.deepEqual(h.played, [], 'Paused playback stays paused');
}
{
  const h = harness();
  h.context.visibleSounds = [{ id: 'current' }, { id: 'pending' }, { id: 'next' }];
  h.context.pendingTransfers.add('pending');
  h.context.automatic.selected = { id: 'current' };
  h.context.transferTrack('current', true);
  assert.deepEqual(h.selected, ['next'], 'Skip another track already being removed');
}
{
  const h = harness();
  h.context.visibleSounds = [{ id: 'only' }];
  h.context.automatic.selected = { id: 'only' };
  h.context.transferTrack('only', true);
  assert.deepEqual(h.selected, [], 'No successor when the list is empty');
}
{
  const h = harness();
  h.context.visibleSounds = [{ id: 'other' }, { id: 'current' }, { id: 'next' }];
  h.context.automatic.selected = { id: 'current' };
  h.context.transferTrack('other', true);
  assert.deepEqual(h.selected, [], 'Deleting an unrelated card does not change the selected track');
  assert.deepEqual(h.played, []);
}
console.log('OK: instant removal, playback release, double click, error rollback, restoration, serialized responses, next-track navigation.');
