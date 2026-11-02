// Controller regression tests: async cancellation, switching and scheduled tails.
// The separate browser OfflineAudioContext check verifies actual audio rendering.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const code = fs.readFileSync(new URL('../dist/automatic-fire.js', import.meta.url), 'utf8');

function harness() {
  const intervals = new Map();
  const sources = [];
  const pending = new Map();
  let nextTimer = 0;
  class Param {
    setValueAtTime() {}
    linearRampToValueAtTime() {}
    cancelScheduledValues() {}
    cancelAndHoldAtTime() {}
    setTargetAtTime() {}
  }
  class Context {
    currentTime = 0;
    destination = {};
    createGain() { return { gain: new Param(), connect: target => target, disconnect() {} }; }
    createBufferSource() {
      const source = { stops: [], connect: target => target, disconnect() {}, start(when) { this.when = when; }, stop(when) { this.stops.push(when); } };
      sources.push(source);
      return source;
    }
    resume() { return Promise.resolve(); }
    decodeAudioData(bytes) { return Promise.resolve({ duration: 0.3, name: bytes }); }
  }
  const sandbox = {
    window: { AudioContext: Context },
    fetch: file => new Promise(resolve => pending.set(file, () => resolve({ ok: true, arrayBuffer: () => Promise.resolve(file) }))),
    setInterval: fn => { intervals.set(++nextTimer, fn); return nextTimer; },
    clearInterval: id => intervals.delete(id)
  };
  vm.runInNewContext(code, sandbox);
  const errors = [];
  const engine = new sandbox.window.AutomaticFireAuditioner({ onError: error => errors.push(error) });
  return { engine, sources, pending, intervals, errors };
}
const settle = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };

{
  const h = harness();
  h.engine.select('a', 'a.wav');
  h.engine.start();
  h.engine.stop();
  h.pending.get('a.wav')();
  await settle();
  assert.equal(h.sources.length, 0, 'Releasing during loading must not start late audio');
  assert.equal(h.intervals.size, 0);
}

{
  const h = harness();
  h.engine.setRate(30);
  h.engine.setLimit(1);
  h.engine.select('a', 'a.wav');
  h.engine.start();
  h.pending.get('a.wav')();
  await settle();
  assert.equal(h.sources.length, 3, '30/s should schedule three shots in the initial lookahead');
  assert.equal(h.engine.pool.maxConcurrent, 1, 'Oldest voice must be stolen');
  h.engine.stop();
  assert.ok(h.sources.every(source => source.stops.at(-1) <= 0.004), 'Release must cancel every future and stolen source');
  assert.equal(h.intervals.size, 0);
}

{
  const h = harness();
  h.engine.select('a', 'a.wav');
  h.engine.start();
  h.engine.select('b', 'b.wav');
  h.pending.get('b.wav')();
  await settle();
  h.pending.get('a.wav')();
  await settle();
  assert.ok(h.sources.length > 0 && h.sources.every(source => source.buffer.name === 'b.wav'), 'A stale fetch must not switch back to the old candidate');
  h.engine.select('c', 'c.wav');
  h.engine.stop();
  h.pending.get('c.wav')();
  await settle();
  assert.ok(h.sources.every(source => source.buffer.name !== 'c.wav'));
  assert.equal(h.intervals.size, 0);
}

{
  const h = harness();
  h.engine.setRate(15);
  h.engine.select('a', 'a.wav');
  h.engine.start();
  h.pending.get('a.wav')();
  await settle();
  const previous = h.sources.length;
  h.engine.context.currentTime = 20;
  for (const tick of h.intervals.values()) tick();
  assert.ok(h.sources.length - previous <= 2, 'No burst of accumulated shots after a stalled tab');
  h.engine.setRate(5);
  await settle();
  assert.equal(h.intervals.size, 1, 'Changing rate must replace the scheduler');
  h.engine.stop();
  assert.equal(h.errors.length, 0);
}
console.log('OK: release during loading; future/stolen voice cancellation; candidate races; cadence restart; no catch-up burst.');
