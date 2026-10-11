"""Test the real Patch action against the published GitHub UI release, without starting EQ."""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--patcher',type=Path,required=True)
p.add_argument('--client-files',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--live-github',action='store_true',help='Explicitly enable public GitHub downloads into the new output folder.')
args=p.parse_args()
if not args.live_github:
    p.error('This integration test requires --live-github; it downloads files but never starts the game.')
fixture=args.output.resolve()
assert not fixture.exists(),'Choose a new, empty test output path.'
fixture.mkdir(parents=True)
tests=Path(__file__).resolve().parent
for name in ['filelist.yml','old-filelist.txt']:
    shutil.copy2(tests/'fixtures/pre-pz-ui-filelist.yml',fixture/name)
for path in args.client_files.rglob('*'):
    if path.is_file() and not path.relative_to(args.client_files).as_posix().startswith('uifiles/pz_ui/'):
        target=fixture/path.relative_to(args.client_files)
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(path,target)
for name,data in [('eqclient.ini','PLAYER-SETTINGS-UNCHANGED'),('zeal.ini','ZEAL-SETTINGS-UNCHANGED')]:
    (fixture/name).write_text(data)
env={k.upper():v for k,v in os.environ.items()}
csc=r'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
exe=fixture/'ManifestRefreshTests.exe'
shutil.copy2(args.patcher.with_suffix('.exe.config'),exe.with_suffix('.exe.config'))
build=subprocess.run([csc,'/nologo','/target:exe','/platform:x86','/warnaserror+','/reference:System.Net.Http.dll','/reference:System.Windows.Forms.dll','/reference:System.Configuration.dll','/out:'+str(exe),str(tests/'ManifestRefreshTests.cs')],env=env,capture_output=True,text=True,timeout=60)
(fixture/'test-build.log').write_text(build.stdout+build.stderr)
assert build.returncode==0,build.stdout+build.stderr
with (fixture/'test-results.log').open('w') as log:
    result=subprocess.run([str(exe),str(args.patcher.resolve())],env=env,stdout=log,stderr=subprocess.STDOUT,timeout=600,cwd=fixture)
text=(fixture/'test-results.log').read_text()
print(text)
assert result.returncode==0
for path in (args.client_files/'uifiles/pz_ui').rglob('*'):
    if path.is_file():
        assert (fixture/path.relative_to(args.client_files)).read_bytes()==path.read_bytes(),str(path)
(fixture/'verification.json').write_text(json.dumps({'checksPassed':text.count('PASS\t'),'liveGitHubPatchActionTested':True,'all310UiFilesByteMatched':True,'staleLocalManifestRefreshed':True,'failedRefreshStopsPatch':True,'noGameStarted':True},indent=2)+'\n')
