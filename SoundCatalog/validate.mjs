// Metadata, licensing evidence and offline-file checks. No dependencies.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const dist = path.join(root, 'dist');
const context = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(dist, 'catalog.js'), 'utf8'), context, { timeout: 1000 });
const catalog = context.window.SOUND_CATALOG;
const errors = [];
const requireValue = (condition, message) => { if (!condition) errors.push(message); };
const idPattern = /^[a-z0-9][a-z0-9_-]{0,119}$/;
const text = value => typeof value === 'string' && value.trim().length > 0;
const url = value => typeof value === 'string' && /^https?:\/\//.test(value);
const date = value => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(value));

function localFile(relative, prefix, label) {
  if (typeof relative !== 'string' || !relative.startsWith(prefix) || relative.includes('\\') || relative.split('/').includes('..')) {
    errors.push(`${label}: invalid local path`);
    return null;
  }
  const absolute = path.resolve(dist, relative);
  if (!absolute.startsWith(dist + path.sep) || !fs.existsSync(absolute) || !fs.statSync(absolute).isFile()) {
    errors.push(`${label}: file missing (${relative})`);
    return null;
  }
  return absolute;
}

function uniqueIds(items, label) {
  const ids = new Set();
  for (const item of items) {
    requireValue(idPattern.test(item.id), `${label}: invalid ID ${item.id}`);
    requireValue(!ids.has(item.id), `${label}: duplicate ID ${item.id}`);
    ids.add(item.id);
  }
  return ids;
}

requireValue(catalog?.schemaVersion === 1 && catalog?.catalogId === 'sci-fi-bullet-hell-sfx', 'Invalid catalogue identity/version');
if (![catalog?.categories, catalog?.objects, catalog?.sounds].every(Array.isArray)) throw new Error('Catalogue lists are missing');
const categoryIds = uniqueIds(catalog.categories, 'category');
const objectIds = uniqueIds(catalog.objects, 'object');
uniqueIds(catalog.sounds, 'sound');
const shortlistRanks = new Map();
for (const category of catalog.categories) requireValue(text(category.name), `${category.id}: category name missing`);
for (const object of catalog.objects) {
  requireValue(text(object.name) && categoryIds.has(object.categoryId), `${object.id}: invalid name/category`);
}
for (const sound of catalog.sounds) {
  const label = sound.id;
  requireValue(text(sound.title) && objectIds.has(sound.objectId), `${label}: invalid title/object`);
  requireValue(Array.isArray(sound.tags) && sound.tags.every(text), `${label}: tags must be strings`);
  if (sound.shortlist !== undefined) {
    const rank = sound.shortlist?.rank;
    requireValue(Number.isInteger(rank) && rank >= 1 && rank <= 5, `${label}: shortlist rank must be 1..5`);
    requireValue(text(sound.shortlist?.reason), `${label}: shortlist explanation required`);
    const key = `${sound.objectId}:${rank}`;
    requireValue(!shortlistRanks.has(key), `${label}: duplicate shortlist rank within object`);
    shortlistRanks.set(key, label);
  }
  const procedural = sound.source?.kind === 'procedural';
  requireValue(text(sound.source?.name) && text(sound.source?.author) && (procedural || (url(sound.source?.url) && url(sound.source?.downloadUrl))), `${label}: source/author/download URL required`);
  if (procedural) {
    localFile(sound.source.url, 'recipes/', `${label}: procedural source`);
    localFile(sound.source.downloadUrl, 'recipes/', `${label}: procedural recipe`);
  }
  requireValue(text(sound.license?.id) && text(sound.license?.name) && url(sound.license?.url), `${label}: license identity/URL required`);
  requireValue(sound.license?.commercialUse === true, `${label}: commercial use not verified`);
  requireValue(date(sound.license?.verifiedAt), `${label}: license verification date required`);
  requireValue(typeof sound.license?.attribution === 'string', `${label}: attribution conditions required`);
  requireValue(sound.license?.id === 'CC0' || text(sound.license?.attribution), `${label}: non-CC0 attribution/conditions must be explicit`);
  requireValue(!/NC|personal use|non.?commercial/i.test(sound.license?.id || ''), `${label}: noncommercial license is not accepted`);
  const evidence = localFile(sound.license?.evidenceFile, `licenses/${label}/`, `${label}: license evidence`);
  if (evidence) requireValue(fs.statSync(evidence).size > 0, `${label}: empty license evidence`);
  localFile(`licenses/${label}/source.txt`, `licenses/${label}/`, `${label}: source snapshot`);
  const object = catalog.objects.find(item => item.id === sound.objectId);
  const audio = localFile(sound.file, `audio/${object?.categoryId}/${sound.objectId}/${label}/`, `${label}: audio`);
  requireValue(text(sound.originalFilename) && date(sound.downloadedAt), `${label}: original filename/download date required`);
  requireValue(typeof sound.sha256 === 'string' && /^[a-f0-9]{64}$/.test(sound.sha256), `${label}: SHA-256 required`);
  if (audio) {
    requireValue(fs.statSync(audio).size > 44, `${label}: audio is empty`);
    requireValue(crypto.createHash('sha256').update(fs.readFileSync(audio)).digest('hex') === sound.sha256, `${label}: audio checksum mismatch`);
  }
  if (sound.originalFile) {
    const original = localFile(sound.originalFile, `audio/${object?.categoryId}/${sound.objectId}/${label}/`, `${label}: original audio`);
    if (original) requireValue(crypto.createHash('sha256').update(fs.readFileSync(original)).digest('hex') === sound.originalSha256, `${label}: original checksum mismatch`);
  }
  if (sound.rapidPreviewFile) localFile(sound.rapidPreviewFile, `audio/${object?.categoryId}/${sound.objectId}/${label}/`, `${label}: rapid-fire preview`);
  if (sound.preparation?.loopCheck) {
    const check = sound.preparation.loopCheck;
    const file = localFile(check.file, `audio/${object?.categoryId}/${sound.objectId}/${label}/`, `${label}: loop demonstration`);
    if (file) requireValue(crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') === check.sha256, `${label}: loop demonstration checksum mismatch`);
    requireValue(Number.isFinite(check.duration) && check.duration > 0, `${label}: loop demonstration duration required`);
  }
  if (sound.recipeFile) localFile(sound.recipeFile, 'recipes/', `${label}: recipe`);
  for (const group of ['variants', 'auditions', 'stemFiles']) {
    for (const item of sound[group] || []) {
      const file = localFile(item.file, `audio/${object?.categoryId}/${sound.objectId}/${label}/`, `${label}: ${group}`);
      if (file) requireValue(crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') === item.sha256, `${label}: ${group} checksum mismatch`);
    }
  }
  if (sound.auditions) {
    requireValue(sound.variants?.length === 5, `${label}: five variants required`);
    requireValue(JSON.stringify(sound.auditions.map(item => item.rate).sort((a, b) => a - b)) === '[5,10,15]', `${label}: demonstrations for 5/10/15 required`);
    for (const demo of sound.auditions) {
      const eventFile = localFile(demo.eventsFile, `audio/${object?.categoryId}/${sound.objectId}/${label}/demos/`, `${label}: shot events`);
      requireValue(demo.duration === 12, `${label}: demonstration duration must be 12s`);
      requireValue(Number.isFinite(demo.metrics?.lufs) && (demo.rate !== 10 || Math.abs(demo.metrics.lufs + 26) < 0.15), `${label}: comparison loudness outside target`);
      requireValue(demo.metrics?.truePeakDb < -7, `${label}: insufficient peak headroom`);
      if (eventFile) {
        const events = JSON.parse(fs.readFileSync(eventFile, 'utf8'));
        requireValue(events.length === demo.rate * demo.duration, `${label}: incorrect shot count`);
        events.forEach((event, index) => {
          requireValue(event.sample === Math.round(index * 48000 / demo.rate), `${label}: inaccurate cadence`);
          requireValue(event.take >= 1 && event.take <= 5 && (!index || event.take !== events[index - 1].take), `${label}: invalid/repeated adjacent take`);
        });
      }
    }
  }
  for (const source of sound.layerSources || []) {
    requireValue(text(source.author) && url(source.url) && url(source.downloadUrl), `${label}: layer provenance missing`);
    requireValue(source.license?.commercialUse === true && date(source.license?.verifiedAt), `${label}: layer license unverified`);
    const original = localFile(source.originalFile, 'audio/_sources/', `${label}: layer original`);
    if (original) requireValue(crypto.createHash('sha256').update(fs.readFileSync(original)).digest('hex') === source.sha256, `${label}: layer original changed`);
  }
  if (sound.license?.attributionFile) localFile(sound.license.attributionFile, `licenses/${label}/`, `${label}: attribution text`);
}
localFile('audio/_diagnostics/playback-check.wav', 'audio/', 'Diagnostic WAV');
if (errors.length) {
  for (const error of errors) console.error(error);
  process.exitCode = 1;
} else {
  console.log(`OK: ${catalog.sounds.length} sounds, ${catalog.objects.length} objects, ${catalog.categories.length} categories. Local files and required provenance validated.`);
}
