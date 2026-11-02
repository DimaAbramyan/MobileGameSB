"""Read-only validation of Unity v22 SerializedFile script metadata inside APKs.
Scope: script type table references only, not arbitrary gameplay PPtrs or execution.
"""
import io, json, pathlib, struct, zipfile, collections

class Reader:
    def __init__(self,b):self.s=io.BytesIO(b)
    def n(self,n):
        b=self.s.read(n)
        if len(b)!=n:raise ValueError('Unexpected end of metadata')
        return b
    def val(self,f):return struct.unpack('<'+f,self.n(struct.calcsize('<'+f)))[0]
    def cstr(self):
        b=bytearray()
        while True:
            c=self.n(1)
            if c==b'\0':return b.decode('utf-8',errors='replace')
            b+=c
            if len(b)>1048576:raise ValueError('Invalid string length')
    def align(self):self.s.seek((self.s.tell()+3)&~3)
    def count(self):
        n=self.val('i')
        if n<0 or n>1000000:raise ValueError('Invalid collection length '+str(n))
        return n

def parse(b):
    if len(b)<48 or struct.unpack_from('>I',b,8)[0]!=22:return None
    if b[16]!=0:raise ValueError('Unsupported big endian file')
    expected=struct.unpack_from('>Q',b,24)[0]
    if expected!=len(b):raise ValueError('Unexpected file length')
    r=Reader(b);r.s.seek(48);version=r.cstr();platform=r.val('i');trees=r.val('?');types=[]
    for _ in range(r.count()):
        cid=r.val('i');stripped=r.val('?');script=r.val('h')
        if cid==114 or cid<0 or script>=0:r.n(16)
        r.n(16)
        if trees:
            nodes=r.count();buf=r.count();r.n(nodes*32);r.n(buf)
            r.n(r.count()*4)
        types.append((cid,script))
    objects={};object_count=r.count()
    for _ in range(object_count):
        r.align();pid=r.val('q');start=r.val('q');size=r.val('I');tid=r.val('i')
        if tid<0 or tid>=len(types):raise ValueError('Invalid type index')
        objects[pid]=types[tid][0]
    refs=[]
    for _ in range(r.count()):
        fileid=r.val('i');r.align();pid=r.val('q');refs.append((fileid,pid))
    externals=[]
    for _ in range(r.count()):
        r.cstr();r.n(16);r.val('i');externals.append(r.cstr())
    return {'version':version,'platform':platform,'types':types,'objects':objects,'scripts':refs,'externals':externals}

def scan(apk):
    files={};unparsed=[]
    with zipfile.ZipFile(apk) as z:
        names=z.namelist();parts=sorted([n for n in names if '/globalgamemanagers.assets.split' in n],key=lambda n:int(n.rsplit('split',1)[1]))
        candidates=[n for n in names if n.startswith('assets/bin/Data/') and '.split' not in n]
        for name in candidates:
            b=z.read(name)
            try:
                data=parse(b)
                if data:files[name.removeprefix('assets/bin/Data/')]=data
            except ValueError as e:unparsed.append({'file':name,'error':str(e)})
        if parts:
            files['globalgamemanagers.assets']=parse(b''.join(z.read(n) for n in parts))
    issues=[];checked=0;builtin=[];scriptobjects=sum(sum(v==115 for v in f['objects'].values()) for f in files.values())
    for name,f in files.items():
        for index,(fileid,pid) in enumerate(f['scripts']):
            target=name if fileid==0 else f['externals'][fileid-1] if 0<fileid<=len(f['externals']) else None
            target=target.removeprefix('archive:/') if target else None
            if target not in files:
                if target and ('unity default resources' in target or 'unity_builtin_extra' in target):builtin.append({'file':name,'index':index,'target':target,'pathID':pid});continue
                issues.append({'file':name,'index':index,'fileID':fileid,'pathID':pid,'unresolvedExternal':target});continue
            actual=files[target]['objects'].get(pid)
            checked+=1
            if actual!=115:issues.append({'file':name,'index':index,'fileID':fileid,'pathID':pid,'target':target,'actualClassID':actual})
        for typeindex,(cid,script) in enumerate(f['types']):
            if script>=len(f['scripts']):issues.append({'file':name,'typeIndex':typeindex,'invalidScriptTypeIndex':script})
    return {'apk':str(apk),'serializedFiles':len(files),'monoScriptObjects':scriptobjects,'scriptReferencesChecked':checked,'builtinReferencesUnresolved':builtin,'issues':issues,'unparsed':unparsed,'nativeClassCounts':dict(collections.Counter(cid for f in files.values() for cid in f['objects'].values()))}

if __name__=='__main__':
    root=pathlib.Path(__file__).resolve().parents[1];out=root/'AgentArtifacts/PPtrDiagnostics'
    result=[scan(out/'original-suite.apk'),scan(root/'AgentArtifacts/Performance/Build/AndroidPerformanceSuite.apk')]
    (out/'apk-script-reference-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    for r in result:print({k:v for k,v in r.items() if k not in ['nativeClassCounts','issues','builtinReferencesUnresolved','unparsed']},'issues',len(r['issues']),'unparsed',len(r['unparsed']));print(r['issues'][:3],r['unparsed'][:3])

