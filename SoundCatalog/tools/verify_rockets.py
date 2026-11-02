"""Focused file/preservation checks and archive tests on copies, never real user tracks."""
from collections import Counter
from copy import deepcopy
import json
import shutil
import sys
import urllib.parse
import urllib.request
import uuid

from import_rockets import ROOT, DIST, WORK, run, sha

sys.path.insert(0,str(ROOT))
from catalog_store import CatalogStore
from audio_identity import DuplicateGuard


def catalog(path):
    text=path.read_text(encoding='utf8')
    return json.loads(text[text.index('{'):text.rindex('}')+1])


def main():
    old=catalog(WORK/'catalog-before.js');current=catalog(DIST/'catalog.js')
    for key in ('sounds','categories','objects'):
        lookup={x['id']:x for x in current[key]}
        assert all(lookup[x['id']]==x for x in old[key]),key
    baseline=json.loads((WORK/'baseline.json').read_text())
    assert sha(ROOT/'data/selections.json')==baseline['selectionsSha256']
    sounds=[s for s in current['sounds'] if s['id'].startswith(('rl-','re-'))]
    assert Counter(s['objectId'] for s in sounds)=={'rocket-launch':20,'rocket-explosion':20}
    assert len({s['originalSha256'] for s in sounds})==40
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
    checked=[]
    for s in sounds:
        for key,digest in [('file','sha256'),('originalFile','originalSha256')]:
            file=DIST/s[key]
            assert sha(file)==s[digest]
            run(['ffmpeg','-v','error','-i',str(file),'-f','null','-'])
            request=urllib.request.Request('http://127.0.0.1:8765/'+urllib.parse.quote(s[key]),headers={'Range':'bytes=0-63'})
            with opener.open(request,timeout=5) as response:assert response.status==206 and len(response.read())==64
            checked.append(dict(id=s['id'],file=s[key],sha256=True,decoded=True,httpRange=206))
        assert s['preparation']['after']['truePeakDb']<=-9.9
        assert -27.5<s['preparation']['after']['lufs']<-26.4
        assert s['duration']<=(1.5 if s['objectId']=='rocket-launch' else 2.0)
    # Representative actual assets, isolated from the user's running catalogue.
    fixture=WORK/'archive-check'/uuid.uuid4().hex
    assert fixture.resolve().is_relative_to(WORK.resolve())
    store=CatalogStore(fixture)
    selected=[next(s for s in sounds if s['objectId']==oid) for oid in ['rocket-launch','rocket-explosion']]
    for s in selected:
        folder=f'audio/{s["objectId"]}/{s["objectId"]}/{s["id"]}'
        for relative in (folder,'licenses/'+s['id']):
            shutil.copytree(DIST/relative,store.local(relative),dirs_exist_ok=True)
    copy=deepcopy(current);copy['sounds']=deepcopy(selected);store._write_catalog(copy)
    for s in selected:
        store.change_removed(s['id'],True)
        moved=CatalogStore(fixture).library()
        assert any(x['id']==s['id'] for x in moved['removed'])
        assert not store.local(s['file']).exists()
        assert not store.local(s['file']).with_name('preparation.json').exists()
        archived=store.local('Removed/'+s['id']+'/files/'+s['originalFile'])
        assert sha(archived)==s['originalSha256']
        renamed=dict(s,id='renamed-'+s['id'],source={})
        blocked=DuplicateGuard(store).check(renamed,[DIST/s['originalFile']])
        assert blocked and blocked['state']=='removed'
        store.change_removed(s['id'],False)
        assert sha(store.local(s['file']))==s['sha256']
        assert store.local(s['file']).with_name('preparation.json').exists()
    report=dict(newSounds=len(sounds),roles=dict(Counter(s['objectId'] for s in sounds)),
                files=checked,oldRecordsUnchanged=len(old['sounds']),notesUnchanged=True,
                representativeArchiveMoveRestore=True,removedContentReimportBlocked=True,
                fixture=str(fixture.relative_to(ROOT)))
    (WORK/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print('PASS: 40 new sounds / 80 audio files; 95 previous records and notes unchanged; actual-asset archive copies move, block reimport and restore.')


if __name__=='__main__':main()
