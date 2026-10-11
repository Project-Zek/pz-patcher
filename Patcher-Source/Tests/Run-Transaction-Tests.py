import pathlib,subprocess,os,threading,http.server,socketserver,json,uuid
OUT=pathlib.Path(__file__).resolve().parent / "results"
OUT.mkdir(exist_ok=True)
work=pathlib.Path(__file__).resolve().parent;root=OUT/'fx'/uuid.uuid4().hex[:8];root.mkdir(parents=True,exist_ok=False)
project=work.parent/'EQEmu Patcher/EQEmu Patcher'
compiler=r'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
binary=OUT/'PatchTransactionTests.exe'
env={k.upper():v for k,v in os.environ.items()}
p=subprocess.run([compiler,'/nologo','/target:exe','/warn:4','/warnaserror+','/reference:System.Net.Http.dll','/out:'+str(binary),str(work/'PatchTransactionTests.cs'),str(project/'PatchTransaction.cs')],env=env,capture_output=True,text=True)
assert p.returncode==0,p.stdout+p.stderr
requests=[]
class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        requests.append(self.path)
        scenario,name=self.path.strip('/').split('/')
        if scenario=='missing' and name=='b.txt':self.send_error(404);return
        payload=b'NEW-A-123' if name=='a.txt' else b'NEW-B-456'
        if scenario=='badhash' and name=='b.txt':payload=b'BAD-B-456'
        self.send_response(200);self.send_header('Content-Length',str(len(payload)));self.end_headers()
        if scenario=='truncated':payload=payload[:3]
        self.wfile.write(payload)
    def log_message(self,*args):pass
server=socketserver.TCPServer(('127.0.0.1',0),Handler)
thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
p=subprocess.run([str(binary),str(root/'client with spaces'),f'http://127.0.0.1:{server.server_address[1]}'],env=env,capture_output=True,text=True,timeout=120)
server.shutdown();server.server_close()
results=[]
for line in p.stdout.splitlines():
    parts=line.split('\t');results.append({'status':parts[0],'test':parts[1],'detail':' '.join(parts[2:])})
results.append({'test':'matching files skip the HTTP service','status':'PASS' if not any('/already/' in r for r in requests) else 'FAIL'})
results.append({'test':'invalid paths never reach the HTTP service','status':'PASS' if not any('/unsafe-' in r or '/duplicate/' in r or '/collision/' in r or '/invalid-md5/' in r or '/http/' in r for r in requests) else 'FAIL'})
(OUT/'patcher-transaction-validation.json').write_text(json.dumps({'scope':'Actual C# updater with loopback HTTP test service; no public download host or game used.','tests':results,'exit':p.returncode,'stderr':p.stderr},indent=2),encoding='utf-8')
print(json.dumps({'pass':sum(x['status']=='PASS' for x in results),'fail':sum(x['status']=='FAIL' for x in results),'exit':p.returncode,'tests':results},indent=2))
if p.returncode or any(x['status']!='PASS' for x in results):raise SystemExit(1)
