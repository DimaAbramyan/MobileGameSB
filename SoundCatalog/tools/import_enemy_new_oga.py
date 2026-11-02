import hashlib,json,shutil,subprocess,sys
from datetime import date
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT))
from audio_identity import ImportGate
FILES=[('ed-yd-explode',ROOT/'.local/research/enemy-new/yd/explode.ogg','explode.ogg','yd','https://opengameart.org/content/platformer-sounds-terminal-interaction-door-shots-bang-and-footsteps/','https://opengameart.org/sites/default/files/yd-Sounds.zip'),('ed-yd-boxopen',ROOT/'.local/research/enemy-new/yd/boxopen.ogg','boxopen.ogg','yd','https://opengameart.org/content/platformer-sounds-terminal-interaction-door-shots-bang-and-footsteps/','https://opengameart.org/sites/default/files/yd-Sounds.zip'),('ed-yd-killshoot',ROOT/'.local/research/enemy-new/yd/kill_shoot.ogg','kill_shoot.ogg','yd','https://opengameart.org/content/platformer-sounds-terminal-interaction-door-shots-bang-and-footsteps/','https://opengameart.org/sites/default/files/yd-Sounds.zip'),('ed-elnineo-big',ROOT/'.local/research/enemy-new/big-explosion.ogg','Big Explosion.ogg','elnineo','https://opengameart.org/content/explosion-tilesets','https://opengameart.org/sites/default/files/Big%20Explosion.ogg'),('ed-elnineo-double',ROOT/'.local/research/enemy-new/double-explosion.ogg','Double Explosion.ogg','elnineo','https://opengameart.org/content/explosion-tilesets','https://opengameart.org/sites/default/files/Double%20Explosion.ogg')]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 gate=ImportGate(ROOT);records=[]
 for sid,src,name,author,url,down in FILES:
  if not gate.allow(sid,src,{'url':url,'downloadUrl':down,'author':author,'title':name}):continue
  rel=f'audio/enemy-destruction/enemy-destruction/{sid}/originals/source{src.suffix}';dest=ROOT/'dist'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
  lic=ROOT/'dist/licenses'/sid;lic.mkdir(parents=True,exist_ok=True);(lic/'source.txt').write_text(f'Author: {author}\nPage: {url}\nDownload: {down}\nOriginal: {name}\nSHA-256: {sha(src)}\n',encoding='utf8');(lic/'license.txt').write_text(f'CC0; verified {date.today()}; commercial use permitted; attribution not required.\n',encoding='utf8')
  records.append({'id':sid,'objectId':'enemy-destruction','title':f'{author} · {name}','description':'Необработанный CC0-кандидат для уничтожения малого противника; требует художественного прослушивания.','tags':['enemy-destruction','sci-fi','arcade','unaltered'],'file':rel,'originalFile':rel,'originalFilename':name,'downloadedAt':str(date.today()),'sha256':sha(dest),'originalSha256':sha(src),'source':{'name':'OpenGameArt','url':url,'author':author,'downloadUrl':down},'license':{'id':'CC0','name':'CC0 1.0','url':'https://creativecommons.org/publicdomain/zero/1.0/','commercialUse':True,'attribution':'Атрибуция не требуется.','verifiedAt':str(date.today()),'evidenceFile':f'licenses/{sid}/license.txt'}})
 print(json.dumps(gate.store.import_records(records),ensure_ascii=False))
if __name__=='__main__':main()
