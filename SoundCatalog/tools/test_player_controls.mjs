// Exercise the actual shared archive-button binding and category-independent selector.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const code = fs.readFileSync(new URL('../dist/app.js', import.meta.url), 'utf8');
const html = fs.readFileSync(new URL('../dist/index.html', import.meta.url), 'utf8');
function source(name) {
  const start = code.indexOf(`  function ${name}(`);
  assert.ok(start >= 0);
  return code.slice(start, code.indexOf('\n  function ', start + 10));
}
function button() {
  return { dataset: {}, attributes: {}, setAttribute(key, value) { this.attributes[key] = value; } };
}
const sounds = [
  { id: 'minigun', objectId: 'machine-gun-v3', title: 'Minigun' },
  { id: 'flame', objectId: 'flamethrower-arcade', title: 'Flamethrower' },
  { id: 'laser', objectId: 'continuous-laser-loop', title: 'Laser' }
];
const nodes = new Map(['auto-remove', 'auto-fire-panel', 'auto-favorite', 'auto-candidate'].map(id => [id, button()]));
nodes.get('auto-candidate').replaceChildren = (...options) => { nodes.get('auto-candidate').options = options; };
const calls = [];
const context = {
  soundsById: new Map(sounds.map(sound => [sound.id, sound])),
  libraryAvailable: true, pendingTransfers: new Set(), view: 'all', visibleSounds: sounds,
  automatic: { selected: null }, $: id => nodes.get(id),
  element: (tag, cls, title) => ({ title }), syncFavoriteButton() {},
  transferTrack: (id, remove) => calls.push({ id, remove }),
  selectAutoCandidate(id) {
    context.automatic.selected = { id };
    context.syncArchiveButton(nodes.get('auto-remove'), id);
  }
};
vm.createContext(context);
vm.runInContext(source('syncArchiveButton') + source('syncAutoCandidates'), context);

for (const id of ['player-remove', 'auto-remove']) {
  assert.match(html, new RegExp(`id="${id}"[^>]*class="archive-action"`));
  assert.ok(code.includes(`syncArchiveButton($("${id}"),`));
}
assert.ok(code.includes('syncArchiveButton(archiveAction, sound.id)'), 'Cards use the same binding');
const remove = button();
context.syncArchiveButton(remove, 'flame');
remove.onclick();
assert.deepEqual(calls.pop(), { id: 'flame', remove: true });
context.syncArchiveButton(remove, 'laser');
remove.onclick();
assert.deepEqual(calls.pop(), { id: 'laser', remove: true }, 'Use the current selection');
context.soundsById.get('laser').removedAt = '2026-10-29';
context.syncArchiveButton(remove, 'laser');
remove.onclick();
assert.deepEqual(calls.pop(), { id: 'laser', remove: false }, 'Archived playback offers restoration');
context.pendingTransfers.add('laser');
context.syncArchiveButton(remove, 'laser');
assert.equal(remove.disabled, true);
context.libraryAvailable = false;
context.syncArchiveButton(remove, 'flame');
assert.equal(remove.disabled, true, 'Direct file mode must not attempt filesystem changes');
context.libraryAvailable = true;
context.syncArchiveButton(remove, null);
assert.equal(remove.disabled, true);
remove.onclick();
assert.equal(calls.length, 0);

for (const sound of sounds) {
  context.visibleSounds = [sound];
  context.syncAutoCandidates();
  assert.equal(nodes.get('auto-fire-panel').hidden, false, `${sound.id} player available by default`);
  assert.equal(context.automatic.selected.id, sound.id);
  assert.equal(nodes.get('auto-candidate').options[0].value, sound.id);
}
context.visibleSounds = sounds;
context.automatic.selected = { id: 'flame' };
context.syncAutoCandidates();
assert.equal(context.automatic.selected.id, 'flame', 'Keep a still-visible selected track');
context.visibleSounds = [];
context.syncAutoCandidates();
assert.equal(nodes.get('auto-fire-panel').hidden, true);
assert.equal(nodes.get('auto-remove').disabled, true);
context.visibleSounds = sounds;
context.view = 'removed';
context.syncAutoCandidates();
assert.equal(nodes.get('auto-fire-panel').hidden, true, 'Keep the existing Removed view behavior');
console.log('OK: shared player/card removal, current selection, restore state, disabled states, all weapon categories.');
