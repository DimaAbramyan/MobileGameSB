"""Download public, licensed candidates; keep evidence and originals. No catalog mutation."""
import concurrent.futures
import hashlib
import html
import json
import re
import sys
import time
from pathlib import Path

import requests
from html.parser import HTMLParser

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.local/research/flamethrower'
WORK.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(ROOT / '.local/research'))
from scan_gdc import RemoteZIP
import zipfile
import subprocess
import numpy as np


class Page(HTMLParser):
    def __init__(self, data):
        super().__init__(convert_charrefs=True)
        self.links = []; self.words = []; self.title = ''; self.in_title = False; self.hidden = 0
        self.feed(data)
    def handle_starttag(self, tag, attributes):
        attrs = dict(attributes)
        if tag == 'a' and attrs.get('href'): self.links.append(attrs['href'])
        if tag == 'title': self.in_title = True
        if tag in ('script', 'style'): self.hidden += 1
    def handle_endtag(self, tag):
        if tag == 'title': self.in_title = False
        if tag in ('script', 'style'): self.hidden = max(0, self.hidden - 1)
    def handle_data(self, data):
        if self.in_title: self.title += data
        if not self.hidden and data.strip(): self.words.append(data.strip())


def get(url):
    error = None
    for attempt in range(3):
        try:
            response = requests.get(url, timeout=65)
            response.raise_for_status()
            return response
        except requests.RequestException as exc:
            error = exc
            if isinstance(exc, requests.HTTPError) and exc.response.status_code in (401, 403, 404):
                raise
            time.sleep(attempt + 1)
    raise error


def baseline():
    target = WORK / 'catalog-before.js'
    if not target.exists():
        target.write_bytes((ROOT / 'dist/catalog.js').read_bytes())
        selections = ROOT / 'data/selections.json'
        saved = {'catalogSha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                 'selectionsSha256': hashlib.sha256(selections.read_bytes()).hexdigest() if selections.exists() else None}
        (WORK / 'baseline.json').write_text(json.dumps(saved, indent=2))


def discover(query):
    url = 'https://freesound.org/search/?q=' + requests.utils.quote(query)
    response = get(url)
    soup = Page(response.text)
    (WORK / ('search-' + re.sub(r'[^a-z0-9]+', '-', query.lower()) + '.html')).write_text(response.text, encoding='utf8')
    links = list(dict.fromkeys('https://freesound.org' + href.split('?')[0]
                 for href in soup.links if re.fullmatch(r'/people/[^/]+/sounds/\d+/', href.split('?')[0])))
    return {'query': query, 'links': links}


def fetch_fs(url):
    sid = re.search(r'/sounds/(\d+)', url).group(1)
    saved = WORK / f'fs-{sid}.json'
    if saved.exists():
        return json.loads(saved.read_text(encoding='utf8'))
    response = get(url)
    page = response.text
    soup = Page(page)
    (WORK / f'fs-{sid}.html').write_text(page, encoding='utf8')
    # A sound's own license is the final CC license link, not a license cited in its description/comments.
    licenses = list(dict.fromkeys(href for href in soup.links if 'creativecommons.org/' in href))
    previews = list(dict.fromkeys(html.unescape(u) for u in re.findall(r'https?[^\s"<>]+hq\.mp3', page)))
    name = soup.title.removeprefix('Freesound - ').strip() if soup.title else sid
    info = dict(id='fs-' + sid, author=url.split('/people/')[1].split('/')[0], url=url, title=name,
                licenses=licenses, previews=previews, text='\n'.join(soup.words))
    if licenses and (('zero/1.0' in licenses[-1]) or ('/by/' in licenses[-1] and 'nc' not in licenses[-1])) and previews:
        audio = get(previews[0])
        path = WORK / f'fs-{sid}-hq.mp3'
        path.write_bytes(audio.content)
        info.update(local=path.name, sha256=hashlib.sha256(audio.content).hexdigest(), downloadUrl=previews[0])
    saved.write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf8')
    return {k: info[k] for k in ('id', 'title', 'licenses', 'local') if k in info}


def gdc_candidates():
    result = []
    for path in (ROOT / '.local/research/machine-gun-v3').glob('gdc*.json'):
        index = json.loads(path.read_text(encoding='utf8'))
        for entry in index['files']:
            if re.search(r'flame|burner|torch|fire.*(roar|blast|loop)|gas.*(burn|hiss)', entry['path'], re.I):
                result.append(dict(year=index['year'], part=index['part'], url=index['url'], **entry))
    (WORK / 'gdc-candidates.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    return result


def fetch_gdc(entry):
    key = 'gdc-' + hashlib.sha256(entry['path'].encode()).hexdigest()[:12]
    target = WORK / (key + Path(entry['path']).suffix)
    if not target.exists():
        with zipfile.ZipFile(RemoteZIP(entry['url'])) as archive:
            payload = archive.read(entry['path'])
            assert len(payload) == entry['size']
            target.write_bytes(payload)
    result = dict(**entry, id=key, local=target.name, sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    (WORK / (key + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    return {'id': key, 'path': entry['path'], 'size': target.stat().st_size}


def fetch_oga(slug, author):
    url = 'https://opengameart.org/content/' + slug
    response = get(url); page = Page(response.text)
    (WORK / f'oga-{slug}.html').write_text(response.text, encoding='utf8')
    extensions = ('.flac', '.wav')
    files = [u for extension in extensions for u in page.links if u.lower().endswith(extension) and '/files/' in u]
    if not files: raise ValueError('No public lossless audio at ' + url)
    download = requests.compat.urljoin(url, files[0])
    payload = get(download).content
    filename = 'oga-' + slug + Path(download).suffix
    (WORK / filename).write_bytes(payload)
    result = dict(id='oga-' + slug, title=page.title, author=author, url=url, local=filename, downloadUrl=download,
                  licenses=[u for u in page.links if 'creativecommons.org/licenses/' in u], sha256=hashlib.sha256(payload).hexdigest(),
                  text='\n'.join(page.words))
    (WORK / f'oga-{slug}.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    return {k: result[k] for k in ('id', 'licenses', 'downloadUrl')}


def analyze():
    rows = []
    for meta in sorted(WORK.glob('*.json')):
        info = json.loads(meta.read_text(encoding='utf8'))
        if not isinstance(info, dict) or not info.get('local'): continue
        source = WORK / info['local']
        probe = subprocess.run(['ffprobe', '-v', 'error', '-show_format', '-show_streams', '-of', 'json', str(source)], capture_output=True, check=True)
        details = json.loads(probe.stdout)
        duration = float(details['format']['duration'])
        decoded = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(source), '-t', '50', '-ac', '1', '-ar', '16000', '-f', 'f32le', 'pipe:1'], capture_output=True, check=True)
        data = np.frombuffer(decoded.stdout, dtype='<f4')
        bins = []
        for start in range(0, len(data), 8000):
            block = data[start:start + 8000]
            bins.append(round(float(20 * np.log10(np.sqrt(np.mean(block ** 2)) + 1e-10)), 1))
        fft = np.abs(np.fft.rfft(data[:min(len(data), 16000 * 15)])) ** 2
        frequencies = np.linspace(0, 8000, len(fft))
        high = float(fft[frequencies > 4000].sum() / max(1e-12, fft.sum()))
        text = info.get('text', '').split('Sound effects\n>\n')[-1].split('\nType\n')[0]
        row = dict(id=info['id'], title=info.get('title', info.get('path')), duration=round(duration, 3),
                   highRatio=round(high, 3), rmsHalfSeconds=bins,
                   description=text[:1800], licenses=info.get('licenses', ['Sonniss-GDC-2.0']))
        rows.append(row)
        print(json.dumps({k: row[k] for k in ('id', 'title', 'duration', 'highRatio')}), flush=True)
    (WORK / 'analysis.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf8')


if __name__ == '__main__':
    baseline()
    if sys.argv[1] == 'search':
        for query in sys.argv[2:]:
            print(json.dumps(discover(query)), flush=True)
    elif sys.argv[1] == 'fs':
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            futures = {pool.submit(fetch_fs, url): url for url in sys.argv[2:]}
            for future in concurrent.futures.as_completed(futures):
                try: print(json.dumps(future.result(), ensure_ascii=False), flush=True)
                except Exception as exc: print(json.dumps({'url': futures[future], 'error': str(exc)}), flush=True)
    elif sys.argv[1] == 'batch':
        urls = []
        for page in WORK.glob('search-*.html'):
            for href in Page(page.read_text(encoding='utf8')).links:
                if re.fullmatch(r'/people/[^/]+/sounds/\d+/', href.split('?')[0]):
                    url = 'https://freesound.org' + href.split('?')[0]
                    if url not in urls: urls.append(url)
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            futures = {pool.submit(fetch_fs, url): url for url in urls}
            for future in concurrent.futures.as_completed(futures):
                try:
                    result = future.result()
                    print(json.dumps({k: result[k] for k in ('id', 'title', 'licenses', 'local') if k in result}), flush=True)
                except Exception as exc: print(json.dumps({'url': futures[future], 'error': str(exc)}), flush=True)
    elif sys.argv[1] == 'gdc':
        for entry in gdc_candidates():
            if re.search(r'Gas Burner|Firestorm Flamer|torch_whoosh', entry['path'], re.I):
                print(json.dumps(fetch_gdc(entry)), flush=True)
    elif sys.argv[1] == 'oga':
        print(json.dumps(fetch_oga('fire-loop', 'qubodup')), flush=True)
        print(json.dumps(fetch_oga('torch-fire-spell', 'spookymodem')), flush=True)
    elif sys.argv[1] == 'licenses':
        for name, url in {'cc0': 'https://creativecommons.org/publicdomain/zero/1.0/',
                          'cc-by3': 'https://creativecommons.org/licenses/by/3.0/',
                          'cc-by4': 'https://creativecommons.org/licenses/by/4.0/',
                          'sonniss-license': 'https://sonniss.com/gdc-bundle-license/',
                          'sonniss': 'https://sonniss.com/gameaudiogdc/'}.items():
            (WORK / (name + '.html')).write_text(get(url).text, encoding='utf8')
            print(name, flush=True)
    elif sys.argv[1] == 'analyze': analyze()
