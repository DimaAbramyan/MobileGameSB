"""Import unchanged CC0 Kenney Interface Sounds as pickup audition candidates."""
import hashlib, json, shutil, subprocess, sys, zipfile
from datetime import date
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from audio_identity import ImportGate

ZIP=ROOT/'.local/research/pickups/kenney-interface-sounds.zip'
RAW=ROOT/'.local/research/pickups/interface'
URL='https://kenney.nl/assets/interface-sounds'
DOWNLOAD='https://kenney.nl/download-link/6173736574732f696e746572666163652d736f756e6473'
LICENSE='https://creativecommons.org/publicdomain/zero/1.0/'

# Individual files are intentionally partitioned between distinct gameplay roles;
# no source file is reused across categories.
SETS={
 'upgrade-pickup': ['confirmation_001.ogg','confirmation_002.ogg','confirmation_003.ogg','confirmation_004.ogg','maximize_001.ogg','maximize_002.ogg','maximize_003.ogg','maximize_004.ogg','maximize_005.ogg','maximize_006.ogg','maximize_007.ogg','maximize_008.ogg','maximize_009.ogg','open_001.ogg','open_002.ogg','open_003.ogg','open_004.ogg','question_001.ogg','question_002.ogg','question_003.ogg','question_004.ogg','select_001.ogg','select_002.ogg','select_003.ogg','select_004.ogg','select_005.ogg','select_006.ogg','select_007.ogg','select_008.ogg','bong_001.ogg'],
 'coin-pickup': ['click_001.ogg','click_002.ogg','click_003.ogg','click_004.ogg','click_005.ogg','tick_001.ogg','tick_002.ogg','tick_004.ogg','toggle_001.ogg','toggle_002.ogg','toggle_003.ogg','toggle_004.ogg','scroll_001.ogg','scroll_002.ogg','scroll_003.ogg','scroll_004.ogg','scroll_005.ogg','pluck_001.ogg','pluck_002.ogg','minimize_001.ogg','minimize_002.ogg','minimize_003.ogg','minimize_004.ogg','minimize_005.ogg','minimize_006.ogg','minimize_007.ogg','minimize_008.ogg','minimize_009.ogg','back_001.ogg','back_002.ogg'],
 'metal-pickup': ['drop_001.ogg','drop_002.ogg','drop_003.ogg','drop_004.ogg','close_001.ogg','close_002.ogg','close_003.ogg','close_004.ogg','switch_001.ogg','switch_002.ogg','switch_003.ogg','switch_004.ogg','switch_005.ogg','switch_006.ogg','switch_007.ogg','glass_001.ogg','glass_002.ogg','glass_003.ogg','glass_004.ogg','glass_005.ogg','glass_006.ogg','scratch_001.ogg','scratch_002.ogg','scratch_003.ogg','scratch_004.ogg','scratch_005.ogg','error_001.ogg','error_002.ogg','error_003.ogg','error_004.ogg'],
}
RANGES={'upgrade-pickup':(.2,.8),'coin-pickup':(.08,.35),'metal-pickup':(.1,.4)}
NAMES={'upgrade-pickup':'Upgrade Pickup','coin-pickup':'Coin Pickup','metal-pickup':'Metal Pickup'}

def sha(p):
 h=hashlib.sha256(); h.update(p.read_bytes()); return h.hexdigest()
def dur(p):
 r=subprocess.run(['ffprobe','-v','error','-show_entries','format=duration','-of','json',str(p)],capture_output=True,text=True,check=True)
 return round(float(json.loads(r.stdout)['format']['duration']),3)

def main():
 RAW.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(ZIP) as z:
  for files in SETS.values():
   for name in files:
    out=RAW/name
    if not out.exists(): out.write_bytes(z.read('Audio/'+name))
 gate=ImportGate(ROOT); records=[]; report={k:{'accepted':0,'durationRejected':0,'duplicateRejected':0} for k in SETS}
 for category,files in SETS.items():
  low,high=RANGES[category]
  for n,name in enumerate(files,1):
   source=RAW/name; seconds=dur(source)
   if not low<=seconds<=high: report[category]['durationRejected']+=1; continue
   sid=f"{category[:2]}-kenney-{name[:-4].replace('_','-')}"
   info={'url':URL,'downloadUrl':DOWNLOAD,'archiveMember':'Audio/'+name,'author':'Kenney','title':name}
   if not gate.allow(sid,source,info): report[category]['duplicateRejected']+=1; continue
   rel=f'audio/{category}/{category}/{sid}/originals/source.ogg'; target=ROOT/'dist'/rel; target.parent.mkdir(parents=True,exist_ok=True)
   if not target.exists(): shutil.copy2(source,target)
   ld=ROOT/'dist/licenses'/sid; ld.mkdir(parents=True,exist_ok=True)
   sf=ld/'source.txt'; lf=ld/'license.txt'
   sf.write_text(f'Author: Kenney\nPage: {URL}\nDownload mechanism: {DOWNLOAD}\nArchive member: Audio/{name}\nSHA-256: {sha(source)}\nDuration: {seconds:.3f} s\nImported unchanged.\n',encoding='utf8')
   lf.write_text(f'Kenney Interface Sounds\nVerified: {date.today()}\nLicense: CC0 1.0 Universal\nLicense URL: {LICENSE}\nCommercial use permitted; attribution not required.\n',encoding='utf8')
   rec={'id':sid,'objectId':category,'title':f'Kenney · {name[:-4]}','description':f'Необработанный CC0-кандидат для {NAMES[category]}; длительность {seconds:.3f} с. Требует художественного прослушивания.','tags':[category,'pickup','sci-fi','arcade','unaltered'],'file':rel,'originalFile':rel,'originalFilename':'Audio/'+name,'downloadedAt':str(date.today()),'sha256':sha(target),'originalSha256':sha(source),'source':{'name':'Kenney · Interface Sounds','url':URL,'author':'Kenney','downloadUrl':DOWNLOAD,'archiveMember':'Audio/'+name},'license':{'id':'CC0','name':'CC0 1.0 Universal','url':LICENSE,'commercialUse':True,'attribution':'Атрибуция не требуется (CC0 1.0).','verifiedAt':str(date.today()),'evidenceFile':lf.relative_to(ROOT/'dist').as_posix()},'evidenceFile':sf.relative_to(ROOT/'dist').as_posix()}
   if n<=5: rec['shortlist']={'rank':n,'reason':'Короткий неизменённый интерфейсный эффект; сравните при частом подборе.'}
   records.append(rec); report[category]['accepted']+=1
 cats=[{'id':k,'name':NAMES[k]} for k in SETS]; objs=[{'id':k,'categoryId':k,'name':NAMES[k],'description':'Кандидаты для прослушивания, без обработки исходников.'} for k in SETS]
 result=gate.store.import_records(records,cats,objs); print(json.dumps({'report':report,'store':result},ensure_ascii=False,indent=2))
if __name__=='__main__': main()
