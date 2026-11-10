"""Exercise the Unix scripts in scripts/{linux,macos,wsl,wsl2} with fake platform and build tools, and check the
Windows scripts statically.

Run with: python3 scripts/tests/test-scripts.py

Nothing real is built, installed or started. dotnet, uname, pgrep, pkill, npm, curl, ip, cmd.exe, wslpath and
wscript.exe are fakes that log their arguments; dotnet keeps a fake "installed" state for the xeno global tool and
creates fake xeno, xeno-desktop and xeno-tray executables when it builds or publishes the desktop app. The scripts run
against a temporary copy of the repository with a temporary HOME. The WSL scripts run with WSL_SKIP_CHECK=1 except in
the OS-guard checks. The .bat files cannot run here: the test checks that the same set exists, the CRLF line endings,
and a few rules (setlocal, endlocal & exit /b, shift /1, -h). macOS Xeno.app bundling (build-app.sh) runs only on
macOS, because it needs sips, iconutil and plutil.
"""
import json
import os
import platform
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
UNIX = ['linux', 'macos', 'wsl', 'wsl2']
ALL = UNIX + ['windows']
WINDOWS_HELPERS = {'env', 'build-apps'}   # called by the other .bat files, like scripts/common/lib.sh
MACOS_ONLY = {'build-app'}                # the Xeno.app bundle helper (install.sh uses it on macOS)

FAKE = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
env = os.environ
def log(entry):
    with open(env['CALL_LOG'], 'a') as f: f.write(json.dumps(entry) + '\n')
if name == 'uname':
    if args == ['-s']: print(env['MOCK_UNAME'])
    elif args == ['-r']: print(env.get('MOCK_UNAME_R', '6.0.0-generic'))
    else: print('x86_64')
    sys.exit(0)
if name == 'pgrep':
    sys.exit(0 if env.get('MOCK_RUNNING') and '-P' not in args else 1)
if name == 'cmd.exe':
    print('C:\\Users\\test\\AppData\\Roaming\r'); sys.exit(0)
if name == 'wslpath':
    print(env['MOCK_APPDATA'] if args[0] == '-u' else 'C:\\fake\\' + pathlib.Path(args[1]).name); sys.exit(0)
if name == 'ip':
    print('default via 172.29.0.1 dev eth0 proto kernel'); sys.exit(0)
if name == 'apt-cache':
    print('libicu-dev\nlibicu74\nlibicu72'); sys.exit(0)
if name == 'apt-get':
    log([name, *args])
    if args[:1] == ['install']: pathlib.Path(env['APT_STATE']).write_text('1')
    sys.exit(0)
if name == 'curl':
    ok = [u for u in env.get('MOCK_CURL_OK', '').split(',') if u]
    sys.exit(0 if any(a.startswith(u) for a in args for u in ok) else 7)
if name in ('xeno', 'xeno-desktop', 'xeno-tray'):
    where = 'tool' if '.dotnet' in sys.argv[0] else 'built'
    log([f'{where}:{name}', *args, {'XENO_AGENT_AUTOSTART': env.get('XENO_AGENT_AUTOSTART'),
                                   'XENO_CLIENT_HOME': env.get('XENO_CLIENT_HOME'), 'PATH': env.get('PATH')}])
    if args[:2] == ['status', '--json']:
        print(json.dumps({'processId': int(env.get('MOCK_WORKER_PID', '0'))}, indent=2))
    sys.exit(0)
log([name, *args])
state = pathlib.Path(env['HOME']) / '.fake-xeno-installed'
tool = pathlib.Path(env['HOME']) / '.dotnet' / 'tools' / 'xeno'
def make_apps(folder, names):
    folder.mkdir(parents=True, exist_ok=True)
    for app in names:
        exe = folder / app
        exe.write_text(pathlib.Path(sys.argv[0]).read_text()); exe.chmod(0o755)
if name == 'dotnet':
    if args[:2] == ['tool', 'list']:
        print('Package Id      Version      Commands')
        print('-------------------------------------')
        if state.exists(): print('xeno            0.1.1        xeno')
        sys.exit(0)
    if args[:2] == ['tool', 'uninstall']:
        if not state.exists() or env.get('MOCK_UNINSTALL_FAILS'): sys.exit(1)
        if args[-1] == 'xeno':
            state.unlink(); tool.unlink(missing_ok=True)
        sys.exit(0)
    if args[:2] == ['tool', 'install']:
        state.write_text('1')
        make_apps(tool.parent, ['xeno'])
        sys.exit(0)
    if args[:1] == ['build'] and args[1].endswith('Xeno.Desktop.csproj'):
        conf = args[args.index('-c') + 1]
        make_apps(pathlib.Path(args[1]).parent / 'bin' / conf / args[args.index('-f') + 1], ['xeno', 'xeno-desktop', 'xeno-tray'])
    if args[:1] == ['publish']:
        make_apps(pathlib.Path(args[args.index('-o') + 1]), ['xeno', 'xeno-desktop', 'xeno-tray'])
sys.exit(0)
'''

count = 0


def check(condition, *context):
    global count
    count += 1
    assert condition, context


def unix_names(p):
    return sorted(f.stem for f in (root / 'scripts' / p).glob('*.sh')
                  if not f.name.startswith('_') and f.stem not in MACOS_ONLY)


# Same user-facing set in all five directories.
names = {p: unix_names(p) for p in UNIX}
for p in UNIX:
    check(names[p] == names['linux'], p, set(names[p]) ^ set(names['linux']))
windows = sorted(f.stem for f in (root / 'scripts/windows').glob('*.bat') if f.stem not in WINDOWS_HELPERS)
check(windows == names['linux'], set(windows) ^ set(names['linux']))
for helper in ['macos/build-app.sh', 'windows/env.bat', 'windows/build-apps.bat', 'windows/local-stack.ps1',
               'wsl/_wsl-common.sh', 'wsl2/_wsl-common.sh']:
    check((root / 'scripts' / helper).exists(), helper)
    check(Path(helper).name in (root / 'scripts/README.md').read_text(), 'helper not documented', helper)
check((root / 'scripts/wsl/_wsl-common.sh').read_bytes() == (root / 'scripts/wsl2/_wsl-common.sh').read_bytes())

# WSL scripts are complete implementations: they never run the Linux, macOS or common scripts.
for p in ['wsl', 'wsl2']:
    for f in (root / 'scripts' / p).glob('*.sh'):
        if f.name.startswith('_'):
            continue
        text = f.read_text()
        check(not re.search(r'\.\./linux|\.\./macos|scripts/linux/|common/(?!lib\.sh)[\w-]+\.sh|wsl_run ', text), f)
        check(f'WSL_FLAVOR={p}\n' in text and 'wsl_require\n' in text and 'wsl_sanitize_path\n' in text
              and 'wsl_check_repo "$REPO_ROOT"' in text, 'preamble', f)
        if p == 'wsl':
            check('mirrored' not in text and 'NAT' not in text, 'WSL 2 networking text in WSL 1 script', f)

# Every Unix script: bash strict mode and a die(); every .bat: CRLF on disk and the batch rules.
for p in UNIX:
    for f in (root / 'scripts' / p).glob('*.sh'):
        if f.name.startswith('_'):
            continue
        text = f.read_text()
        check(text.startswith('#!/usr/bin/env bash\n') and 'set -euo pipefail\n' in text and 'die() {' in text, f)
        check(os.access(f, os.X_OK), 'not executable', f)
for f in (root / 'scripts/windows').glob('*.bat'):
    data = f.read_bytes()
    check(data.count(b'\n') == data.count(b'\r\n'), 'not CRLF', f)
    text = data.decode().replace('\r\n', '\n')
    check(text.startswith('@echo off\n'), f)
    if f.stem != 'env':
        check('\nsetlocal' in text, 'setlocal', f)
        check(text.rstrip().splitlines()[-1].startswith('endlocal & exit /b') or f.stem == 'build-apps', 'ending', f)
    check(not re.search(r'^\s*shift\s*$', text, re.M), 'plain shift moves %0 and breaks %~dp0', f)
    if f.stem not in WINDOWS_HELPERS | {'build', 'test', 'run-cli'}:
        check('"--help" goto :usage' in text and ':usage' in text, '-h/--help', f)
    check('npm.cmd' not in text, 'call npm, not npm.cmd, so .exe shims work too', f)
# update.bat: the client state keeps the last processId after the worker stops, so the process must be checked.
update_bat = (root / 'scripts/windows/update.bat').read_text()
check('tasklist /FI "PID eq %WORKER_PID%"' in update_bat and '*[1-9]' not in update_bat, 'update.bat PID check')
local_stack = (root / 'scripts/windows/local-stack.ps1').read_text()
check("'npm.cmd'" not in local_stack and 'net10.0\\Xeno.Server.dll' not in local_stack, 'local-stack.ps1')


def make_repo(temp):
    repo = temp / 'repo'
    shutil.copytree(root / 'scripts', repo / 'scripts', ignore=shutil.ignore_patterns('__pycache__', 'tests'))
    for project in ['Xeno.Desktop', 'Xeno.Client', 'Xeno.Server']:
        csproj = repo / 'src' / project / f'{project}.csproj'
        csproj.parent.mkdir(parents=True)
        csproj.write_text('<Project><PropertyGroup><Version>0.1.1</Version></PropertyGroup></Project>')
    (repo / 'src/Test.Automated').mkdir(parents=True)
    (repo / 'src/Xeno.sln').write_text('')
    (repo / 'assets').mkdir()
    shutil.copy(root / 'assets/logo.png', repo / 'assets/logo.png')
    shutil.copy(root / 'xeno.json', repo / 'xeno.json')
    (repo / 'dashboard').mkdir()
    return repo


with tempfile.TemporaryDirectory(prefix='xeno scripts ') as temp:
    temp = Path(temp)
    repo = make_repo(temp)
    bin_dir = temp / 'bin'
    bin_dir.mkdir()
    for name in ['dotnet', 'uname', 'pgrep', 'pkill', 'npm', 'curl', 'ip', 'cmd.exe', 'wslpath', 'wscript.exe',
                 'apt-get', 'apt-cache']:
        (bin_dir / name).write_text(FAKE)
        (bin_dir / name).chmod(0o755)
    (bin_dir / 'sudo').write_text('#!/bin/sh\nexec "$@"\n')
    (bin_dir / 'sudo').chmod(0o755)
    apt_state = temp / 'apt-installed'
    tools = temp / 'bin-tools'   # clipboard and browser tools; left out of PATH where a test needs them missing
    tools.mkdir()
    for name in ['xclip', 'wslview', 'xdg-open']:
        (tools / name).write_text('#!/bin/sh\nexit 0\n')
        (tools / name).chmod(0o755)
    no_tools_path = f'{bin_dir}:/usr/bin:/bin'
    log = temp / 'calls.jsonl'
    home = temp / 'home'

    # Fake ldconfig -p output (XENO_LDCONFIG on Linux, WSL_LDCONFIG on WSL), so the native-library checks do not
    # depend on the machine running the tests.
    ICU = '\tlibicuuc.so.74 (libc6,x86-64) => /lib/x86_64-linux-gnu/libicuuc.so.74'
    AVALONIA = ['libX11.so.6', 'libICE.so.6', 'libSM.so.6', 'libXext.so.6', 'libXrandr.so.2', 'libXi.so.6',
                'libXcursor.so.1', 'libXfixes.so.3', 'libfontconfig.so.1']

    gui_lines = ['\t%s (libc6,x86-64) => /lib/x86_64-linux-gnu/%s' % (n, n) for n in AVALONIA]

    def ldconfig(tag, lines):
        # After the fake apt-get has "installed" packages, everything is present.
        f = temp / ('fake-ldconfig-' + tag)
        everything = '\n'.join([ICU] + gui_lines)
        f.write_text(f'#!/bin/sh\nif [ -f "$APT_STATE" ]; then\ncat <<"EOF"\n{everything}\nEOF\nelse\ncat <<"EOF"\n'
                     + '\n'.join(lines) + '\nEOF\nfi\n')
        f.chmod(0o755)
        return str(f)
    full = ldconfig('full', [ICU] + gui_lines)
    no_gui = ldconfig('nogui', [ICU, '\tlibICE.so.6 (libc6) => /lib/i386-linux-gnu/libICE.so.6'])  # wrong arch only
    no_icu = ldconfig('noicu', gui_lines)
    no_ldconfig = str(temp / 'no-such-ldconfig')
    base_env = {k: v for k, v in os.environ.items()
                if not k.startswith(('XENO_', 'WSL_', 'DOTNET_')) and k not in ('DISPLAY', 'WAYLAND_DISPLAY')}
    base_env.update(PATH=f'{bin_dir}:{tools}:/usr/bin:/bin', CALL_LOG=str(log), HOME=str(home), WSL_SKIP_CHECK='1',
                    DISPLAY=':0', WSL_DISTRO_NAME='Test', MOCK_APPDATA=str(temp / 'appdata'),
                    XENO_LDCONFIG=full, WSL_LDCONFIG=full, APT_STATE=str(apt_state))

    def fresh_home():
        shutil.rmtree(home, ignore_errors=True)
        home.mkdir()
        apt_state.unlink(missing_ok=True)

    def run(plat, script, *args, unset=(), stdin='', **env):
        log.unlink(missing_ok=True)
        e = dict(base_env, MOCK_UNAME='Darwin' if plat == 'macos' else 'Linux', **env)
        for key in unset:
            e.pop(key, None)
        p = subprocess.run(['bash', str(repo / f'scripts/{plat}/{script}.sh'), *args], env=e, input=stdin,
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=60)
        calls = [json.loads(x) for x in log.read_text().splitlines()] if log.exists() else []
        return p, calls

    def dotnet(calls, *prefix):
        return [c for c in calls if c[0] == 'dotnet' and c[1:1 + len(prefix)] == list(prefix)]

    fresh_home()

    # OS guards.
    for plat, wrong in [('linux', 'Darwin'), ('macos', 'Linux')]:
        for script in names['linux']:
            log.unlink(missing_ok=True)
            pr = subprocess.run(['bash', str(repo / f'scripts/{plat}/{script}.sh')], env=dict(base_env, MOCK_UNAME=wrong),
                                capture_output=True, text=True)
            check(pr.returncode == 1 and 'This script is for' in pr.stderr and not log.exists(), plat, script, pr.stderr)
    pr = subprocess.run(['bash', str(repo / 'scripts/macos/build-app.sh')], env=dict(base_env, MOCK_UNAME='Linux'),
                        capture_output=True, text=True)
    check(pr.returncode == 1 and 'This script is for macOS' in pr.stderr, pr.stderr)
    for plat in ['wsl', 'wsl2']:
        for script in names['linux']:
            p, calls = run(plat, script, unset=['WSL_SKIP_CHECK', 'WSL_DISTRO_NAME'])
            check(p.returncode == 1 and 'This script is for WSL' in p.stderr and not calls, plat, script, p.stderr)
    p, _ = run('wsl', 'build', unset=['WSL_SKIP_CHECK'], MOCK_UNAME_R='5.15.153.1-microsoft-standard-WSL2')
    check(p.returncode == 1 and 'runs WSL 2' in p.stderr and 'scripts/wsl2' in p.stderr, p.stderr)
    p, _ = run('wsl2', 'build', unset=['WSL_SKIP_CHECK'], MOCK_UNAME_R='4.4.0-19041-Microsoft')
    check(p.returncode == 1 and 'runs WSL 1' in p.stderr and 'scripts/wsl ' in p.stderr, p.stderr)

    # Argument handling: -h prints the usage, unknown arguments exit 2, and neither does any work.
    for plat in UNIX:
        for script in ['run-desktop', 'run-tray', 'run-tui']:
            for flag in ['-h', '--help']:
                p, calls = run(plat, script, flag, unset=['DISPLAY'])
                check(p.returncode == 0 and 'Usage:' in p.stdout and not calls, plat, script, flag, p.stdout, p.stderr)
    strict = {'install': ['--bogus'], 'reinstall': ['--bogus'], 'update': ['--bogus'], 'remove': ['extra'],
              'factory-reset': ['--bogus'], 'run-client': ['--bogus'], 'run-dashboard': ['extra'],
              'run-server': ['a.json', 'extra'], 'run-local': None}
    for plat in UNIX:
        for script, bad in strict.items():
            for flag in ['-h', '--help']:
                p, calls = run(plat, script, flag)
                check(p.returncode == 0 and 'Usage:' in p.stdout and not calls, plat, script, flag, p.stdout, p.stderr, calls)
            if bad:
                p, calls = run(plat, script, *bad)
                check(p.returncode == 2 and ('Unknown argument' in p.stderr or 'Unexpected argument' in p.stderr)
                      and not calls, plat, script, p.stderr, calls)
        p, calls = run(plat, 'run-client', '--server')
        check(p.returncode == 2 and '--server requires a value' in p.stderr and not calls, plat, p.stderr)

    # Missing dotnet is reported before anything runs.
    nodotnet = temp / 'bin-nodotnet'
    nodotnet.mkdir()
    for f in bin_dir.iterdir():
        if f.name != 'dotnet':
            os.symlink(f, nodotnet / f.name)
    for plat in UNIX:
        p, calls = run(plat, 'build', PATH=f'{nodotnet}:/usr/bin:/bin')
        check(p.returncode == 1 and "'dotnet' was not found" in p.stderr and not calls, plat, p.stderr)

    # Key commands of the build and run scripts.
    app_dir = repo / 'src/Xeno.Desktop/bin/Debug/net10.0'
    for plat in UNIX:
        p, calls = run(plat, 'build', '-v', 'minimal')
        check(p.returncode == 0 and calls == [['dotnet', 'build', str(repo / 'src/Xeno.sln'), '-c', 'Debug', '-v', 'minimal']],
              plat, calls, p.stderr)
        p, calls = run(plat, 'test', '--suite', 'Tui')
        check(p.returncode == 0 and calls[-1] == ['dotnet', 'run', '--project', str(repo / 'src/Test.Automated'), '-c',
                                                    'Debug', '--no-build', '--', '--suite', 'Tui'], plat, calls)
        p, calls = run(plat, 'run-cli', 'status')
        check(p.returncode == 0 and calls[-1][:2] == ['built:xeno', 'status'] and p.stdout == '', plat, calls, p.stdout)
        p, calls = run(plat, 'run-tui', '--page', 'models')
        check(p.returncode == 0 and dotnet(calls, 'build', str(repo / 'src/Xeno.Desktop/Xeno.Desktop.csproj'))
              and calls[-1][:4] == ['built:xeno', 'tui', '--page', 'models'], plat, calls)
        p, calls = run(plat, 'run-desktop', '--server', 'cloud')
        check(p.returncode == 0 and calls[-1][:3] == ['built:xeno-desktop', '--server', 'cloud'], plat, calls, p.stderr)
        # WSL has no notification area, so the desktop app does not start the tray agent unless asked to.
        check(calls[-1][-1]['XENO_AGENT_AUTOSTART'] == ('0' if plat.startswith('wsl') else None), plat, calls[-1])
        p, calls = run(plat, 'run-desktop', XENO_AGENT_AUTOSTART='1')
        check(calls[-1][-1]['XENO_AGENT_AUTOSTART'] == '1', plat, calls)
        p, calls = run(plat, 'run-client', '--server', 'http://example.test:9000')
        tool_calls = [c[1:-1] for c in calls if c[0] == 'built:xeno']
        check(p.returncode == 0 and tool_calls == [['server', 'use', 'http://example.test:9000'], ['stop'],
                                                   ['connect', '--foreground']], plat, tool_calls, p.stderr)
        shutil.rmtree(repo / 'artifacts', ignore_errors=True)
        p, calls = run(plat, 'run-server')
        check(p.returncode == 0 and (repo / 'artifacts/local-server/xeno.json').exists()
              and calls[-1] == ['dotnet', 'run', '--project', str(repo / 'src/Xeno.Server/Xeno.Server.csproj'), '-c',
                                'Debug', '--no-build', '--', 'xeno.json'], plat, calls)
        shutil.rmtree(repo / 'dashboard/node_modules', ignore_errors=True)
        p, calls = run(plat, 'run-dashboard')
        check(p.returncode == 0 and calls == [['npm', 'install'], ['npm', 'run', 'dev']], plat, calls, p.stderr)
        p, calls = run(plat, 'run-local', 'tui', '--page', 'server', MOCK_CURL_OK='http://localhost:9000',
                       XENO_SKIP_DASHBOARD='1')
        built = [c for c in calls if c[0].startswith('built:')]
        check(p.returncode == 0 and 'already answering' in p.stdout
              and [c[:-1] for c in built] == [['built:xeno', 'server', 'use', 'http://localhost:9000'],
                                               ['built:xeno', 'tui', '--page', 'server']]
              and built[-1][-1]['XENO_CLIENT_HOME'] == str(home / '.xeno-local'), plat, calls, p.stderr)
        p, calls = run(plat, 'run-tray')
        if plat.startswith('wsl'):
            check(p.returncode == 1 and 'notification area' in p.stderr and 'run-tray.bat' in p.stderr and not calls, plat)
            p, calls = run(plat, 'run-tray', XENO_WSL_FORCE_TRAY='1')
        check(p.returncode == 0 and calls[-1][0] == 'built:xeno-tray', plat, calls, p.stderr)

    # GUI apps need a display under WSL: WSLg on WSL 2 (DISPLAY: Avalonia uses X11; WAYLAND_DISPLAY alone is not enough), an X server (DISPLAY) on WSL 1.
    for plat in ['wsl', 'wsl2']:
        for script, args in [('run-desktop', []), ('run-local', []), ('run-local', ['desktop']), ('run-local', ['tray'])]:
            p, calls = run(plat, script, *args, unset=['DISPLAY'], XENO_WSL_FORCE_TRAY='1')
            check(p.returncode == 1 and 'scripts\\windows\\' in p.stderr and not calls, plat, script, args, p.stderr)
        for script, args in [('run-desktop', []), ('run-tray', []), ('run-local', ['desktop'])]:
            p, calls = run(plat, script, *args, unset=['DISPLAY'], WAYLAND_DISPLAY='wayland-0', XENO_WSL_FORCE_TRAY='1')
            check(p.returncode == 1 and 'DISPLAY' in p.stderr and not calls, plat, script, p.stderr)
        fresh_home()
        for script in ['install', 'reinstall', 'update']:
            p, calls = run(plat, script, '--no-dashboard', unset=['DISPLAY'], WAYLAND_DISPLAY='wayland-0')
            check(p.returncode == 0 and 'only the xeno tool is installed' in p.stderr and not dotnet(calls, 'publish'),
                  plat, script, p.stderr)
        p, calls = run(plat, 'run-local', 'tray', MOCK_CURL_OK='http://localhost:9000')
        check(p.returncode == 1 and 'notification area' in p.stderr and not calls, plat, p.stderr)

    # WSL 2 NAT networking: a localhost --server that only Windows answers is pointed at the Windows host.
    wsl2_env = dict(MOCK_UNAME_R='5.15.153.1-microsoft-standard-WSL2', MOCK_CURL_OK='http://172.29.0.1:9000')
    for plat in ['wsl', 'wsl2']:
        expected = 'http://172.29.0.1:9000' if plat == 'wsl2' else 'http://localhost:9000'
        p, calls = run(plat, 'run-tui', '--server', 'http://localhost:9000', '--page', 'models', **wsl2_env)
        check(p.returncode == 0 and calls[-1][:5] == ['built:xeno', 'tui', '--server', expected, '--page'], plat, calls)
        check(('can change when Windows restarts' in p.stderr) == (plat == 'wsl2'), plat, p.stderr)
        p, calls = run(plat, 'run-client', '--server', 'localhost:9000', **wsl2_env)
        check(['built:xeno', 'server', 'use', expected if plat == 'wsl2' else 'localhost:9000']
              == calls[next(i for i, c in enumerate(calls) if c[0] == 'built:xeno')][:-1], plat, calls)
        p, calls = run(plat, 'run-desktop', '--server', 'http://localhost:9000',
                       MOCK_UNAME_R=wsl2_env['MOCK_UNAME_R'], MOCK_CURL_OK='http://localhost:9000,http://172.29.0.1:9000')
        check(calls[-1][:3] == ['built:xeno-desktop', '--server', 'http://localhost:9000'], plat, calls)

    # install / reinstall / update / remove. The rule on every OS: install replaces an existing install (stopping the
    # worker and the apps first); reinstall and update check their arguments before removing anything; remove
    # succeeds with "nothing to uninstall"; a failed uninstall is an error.
    applications = home / '.local/share/applications'
    installed_apps = home / '.local/share/xeno/app'
    for plat in UNIX:
        fresh_home()
        extra = ['--no-apps'] if plat == 'macos' else []   # Xeno.app bundling is checked below, on macOS only
        p, calls = run(plat, 'install', '--no-dashboard', *extra)
        check(p.returncode == 0 and '[install] Completed.' in p.stdout, plat, p.stdout, p.stderr)
        steps = [c[1:3] for c in calls if c[0] == 'dotnet' and c[1] in ('pack', 'tool', 'publish')]
        check(steps[0] == ['pack', str(repo / 'src/Xeno.Client/Xeno.Client.csproj')]
              and steps.index(['tool', 'install']) > steps.index(['tool', 'uninstall']), plat, steps)
        check(not [c for c in calls if c[0] == 'npm'], plat, calls)
        check((home / '.fake-xeno-installed').exists(), plat)
        if plat != 'macos':
            check(dotnet(calls, 'publish') and (installed_apps / 'xeno-desktop').exists(), plat, calls)
            desktop = applications / 'xeno.desktop'
            agent = applications / 'xeno-agent.desktop'
            if plat == 'linux':
                check(desktop.exists() and agent.exists(), plat)
            elif plat == 'wsl2':
                text = desktop.read_text()
                check(f'Exec=env XENO_AGENT_AUTOSTART=0 "{installed_apps}/xeno-desktop"' in text and not agent.exists(), text)
            else:
                check(not desktop.exists() and not agent.exists(), plat)
        # Installed already: install replaces it and stops the running worker first.
        p, calls = run(plat, 'install', '--no-dashboard', '--no-apps')
        check(p.returncode == 0 and calls[0][0] == 'tool:xeno' and calls[0][1] == 'stop', plat, calls)
        # An old tool that cannot be uninstalled (a running xeno.exe on Windows) stops the install with remove's error.
        p, calls = run(plat, 'install', '--no-dashboard', '--no-apps', MOCK_UNINSTALL_FAILS='1')
        check(p.returncode == 1 and 'could not uninstall the xeno global tool' in p.stderr
              and not dotnet(calls, 'tool', 'install'), plat, p.stderr)
        # A typo must not uninstall anything.
        for script in ['reinstall', 'update']:
            p, calls = run(plat, script, '--no-dashbord')
            check(p.returncode == 2 and not calls and (home / '.fake-xeno-installed').exists(), plat, script, calls)
        p, calls = run(plat, 'reinstall', '--no-dashboard', '--no-apps')
        steps = [c[1:3] for c in calls if c[0] == 'dotnet' and c[1] in ('pack', 'tool')]
        check(p.returncode == 0 and steps.index(['tool', 'uninstall']) < steps.index(['pack', str(repo / 'src/Xeno.Client/Xeno.Client.csproj')])
              < steps.index(['tool', 'install']), plat, steps)
        check('[reinstall] Removing' in p.stdout and '[remove] Completed' in p.stdout and '[install] Completed.' in p.stdout, plat, p.stdout)
        # update restarts a worker that was running.
        p, calls = run(plat, 'update', '--no-dashboard', '--no-apps', MOCK_WORKER_PID=str(os.getpid()))
        check(p.returncode == 0 and calls[-1][:2] == ['tool:xeno', 'start'], plat, calls[-3:], p.stderr)
        p, calls = run(plat, 'update', '--no-dashboard', '--no-apps')
        check(p.returncode == 0 and not [c for c in calls if c[1:2] == ['start']], plat, calls)
        # remove: refuses to report success when the uninstall fails; then removes everything; then has nothing to do.
        p, calls = run(plat, 'remove', MOCK_UNINSTALL_FAILS='1')
        check(p.returncode == 1 and 'could not uninstall' in p.stderr, plat, p.stderr)
        if plat != 'macos':
            installed_apps.mkdir(parents=True, exist_ok=True)
            applications.mkdir(parents=True, exist_ok=True)
            (applications / 'xeno-agent.desktop').write_text('')
        p, calls = run(plat, 'remove')
        check(p.returncode == 0 and dotnet(calls, 'tool', 'uninstall') and not (home / '.fake-xeno-installed').exists(),
              plat, p.stderr)
        check(['pkill', '-x', 'xeno-desktop'] in calls and ['pkill', '-x', 'xeno-tray'] in calls, plat, calls)
        if plat != 'macos':
            check(not installed_apps.exists() and not (applications / 'xeno-agent.desktop').exists(), plat)
        p, calls = run(plat, 'remove')
        check(p.returncode == 0 and 'nothing to uninstall' in p.stdout and not dotnet(calls, 'tool', 'uninstall'), plat)
        # reinstall tolerates "not installed".
        p, calls = run(plat, 'reinstall', '--no-dashboard', '--no-apps')
        check(p.returncode == 0 and 'nothing to uninstall' in p.stdout and (home / '.fake-xeno-installed').exists(), plat)

    # WSL: the dashboard build uses only a Linux npm, and without a display only the tool is installed.
    for plat in ['wsl', 'wsl2']:
        fresh_home()
        p, calls = run(plat, 'install', unset=['DISPLAY'])
        check(p.returncode == 0 and 'only the xeno tool is installed' in p.stderr and not dotnet(calls, 'publish')
              and ['npm', 'run', 'build:local'] in calls, plat, p.stderr, calls)
    # A user-local .NET (DOTNET_ROOT) is passed to the WSLg launcher entry, which does not read ~/.bashrc.
    fresh_home()
    p, _ = run('wsl2', 'install', '--no-dashboard', DOTNET_ROOT='/opt/dotnet')
    check('Exec=env XENO_AGENT_AUTOSTART=0 DOTNET_ROOT=/opt/dotnet ' in (applications / 'xeno.desktop').read_text(), p.stderr)

    for plat in UNIX:
        fresh_home()
        if plat == 'macos':
            leftovers = [home / 'Applications/Xeno.app']
        else:
            leftovers = [installed_apps, applications / 'xeno.desktop', applications / 'xeno-agent.desktop']
        for path in leftovers:
            if path.suffix == '.desktop':
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('')
            else:
                path.mkdir(parents=True)
        p, calls = run(plat, 'remove', PATH=f'{nodotnet}:/usr/bin:/bin')
        check(p.returncode == 0 and 'uninstall was skipped' in p.stderr and not any(x.exists() for x in leftovers)
              and not dotnet(calls), plat, p.stdout, p.stderr)

    # Native libraries (WSLg and minimal distros): the GUI launchers stop before building when the Avalonia libraries
    # are missing, the terminal app and install only warn, a missing ICU stops every dotnet script, and without
    # ldconfig nothing is guessed. macOS has none of these checks.
    def lib_env(fake):
        return dict(XENO_LDCONFIG=fake, WSL_LDCONFIG=fake, XENO_WSL_FORCE_TRAY='1', MOCK_CURL_OK='http://localhost:9000',
                    XENO_SKIP_DASHBOARD='1')
    for plat in UNIX:
        native = plat != 'macos'
        cases = [(full, 'run-desktop', [], True, None), (no_ldconfig, 'run-desktop', [], True, None),
                 (no_gui, 'run-desktop', [], not native, 'cannot start without these shared libraries: libX11.so.6 libICE.so.6'),
                 (no_gui, 'run-tray', [], not native, 'libice6'),
                 (no_gui, 'run-local', ['desktop'], not native, 'Unable to load shared library'),
                 (no_gui, 'run-local', ['tray'], not native, 'libfontconfig1'),
                 (no_gui, 'run-local', ['tui'], True, 'cannot start until these shared libraries are installed'),
                 (no_gui, 'run-tui', [], True, 'cannot start until these shared libraries are installed'),
                 (no_icu, 'run-tui', [], not native, 'needs ICU'), (no_icu, 'build', [], not native, 'needs ICU'),
                 (no_icu, 'run-server', [], not native, "Couldn't find a valid ICU package")]
        for fake, script, args, ok, text in cases:
            fresh_home()
            p, calls = run(plat, script, *args, **lib_env(fake))
            check((p.returncode == 0) == ok, plat, script, args, fake, p.stderr)
            if text and native:
                check(text in p.stderr, plat, script, text, p.stderr)
            if not native:
                check('shared libraries' not in p.stderr and 'ICU' not in p.stderr, plat, script, p.stderr)
            if not ok:
                check(not dotnet(calls, 'build') and not dotnet(calls, 'run') and not dotnet(calls, 'pack')
                      and not [c for c in calls if c[0].startswith('built:')], plat, script, calls)
        fresh_home()
        p, _ = run(plat, 'build', **dict(lib_env(no_icu), DOTNET_SYSTEM_GLOBALIZATION_INVARIANT='1'))
        check(p.returncode == 0, plat, 'invariant globalization needs no ICU', p.stderr)
        # Launchers only check; they never install packages.
        for fake, script, args in [(no_gui, 'run-desktop', []), (no_gui, 'run-tui', []), (no_icu, 'run-tui', []),
                                   (no_gui, 'run-local', ['tui']), (no_icu, 'build', [])]:
            fresh_home()
            p, calls = run(plat, script, *args, **lib_env(fake))
            check(not [c for c in calls if c[0] == 'apt-get'], plat, script, calls)

    # Install-type scripts install the missing system packages themselves (apt-get through sudo), before the dotnet
    # check and before removing anything. Linux: ICU, the Avalonia libraries, a clipboard tool; WSL adds wslu and
    # xdg-utils. INSTALL_SYSTEM_PACKAGES=0 only reports. --help never installs.
    GUI_DEBS = ['libx11-6', 'libice6', 'libsm6', 'libxext6', 'libxrandr2', 'libxi6', 'libxcursor1', 'libxfixes3', 'libfontconfig1']
    have_clip = any(shutil.which(t, path='/usr/bin:/bin') for t in ['wl-copy', 'xclip', 'xsel'])
    for plat in UNIX:
        native = plat != 'macos'
        for script in ['install', 'reinstall', 'update']:
            for fake, args, want in [(no_gui, ['--no-dashboard'], GUI_DEBS), (no_icu, ['--no-dashboard', '--no-apps'], ['libicu74'])]:
                fresh_home()
                extra = ['--no-apps'] if plat == 'macos' else []
                p, calls = run(plat, script, *args, *extra, **dict(lib_env(fake), PATH=no_tools_path))
                installs = [c for c in calls if c[:2] == ['apt-get', 'install']]
                check(p.returncode == 0, plat, script, fake, p.stderr)
                if not native:
                    check(not installs, plat, script, calls)
                    continue
                pkgs = [x for c in installs for x in c[2:] if not x.startswith('-')]
                check(set(want) <= set(pkgs) and ['apt-get', 'update', '-qq'] in calls, plat, script, pkgs)
                check(('xclip' in pkgs) == (not have_clip), plat, script, pkgs)
                check(('wslu' in pkgs and 'xdg-utils' in pkgs) == plat.startswith('wsl'), plat, script, pkgs)
                # Packages come first: before the tool is packed or the old install removed.
                first_dotnet = next(i for i, c in enumerate(calls) if c[0] == 'dotnet' or c[0].startswith('tool:'))
                check(calls.index(installs[0]) < first_dotnet, plat, script, calls)
                check('--no-apps' not in args or not set(GUI_DEBS) & set(pkgs), plat, script, pkgs)
            # Nothing missing: nothing is installed.
            fresh_home()
            p, calls = run(plat, script, '--no-dashboard', '--no-apps', **lib_env(full))
            check(p.returncode == 0 and not [c for c in calls if c[0] == 'apt-get'], plat, script, calls)
            # INSTALL_SYSTEM_PACKAGES=0 reports the packages and stops when required ones are missing.
            fresh_home()
            p, calls = run(plat, script, '--no-dashboard', **dict(lib_env(no_gui), INSTALL_SYSTEM_PACKAGES='0'))
            if native:
                check(p.returncode == 1 and 'were not installed automatically' in p.stderr and 'libice6' in p.stderr
                      and not [c for c in calls if c[0] in ('apt-get', 'dotnet')], plat, script, p.stderr, calls)
            # --help installs nothing.
            for flag in ['-h', '--help']:
                fresh_home()
                p, calls = run(plat, script, '--no-apps', flag, **lib_env(no_gui))
                check(p.returncode == 0 and 'Usage:' in p.stdout and not calls, plat, script, flag, calls)

    # The terminal app notes a missing clipboard tool (wl-copy, xclip, xsel).
    if not have_clip:
        for plat in ['linux', 'wsl', 'wsl2']:
            for script, args in [('run-tui', [])]:
                fresh_home()
                p, _ = run(plat, script, *args, PATH=no_tools_path)
                check(p.returncode == 0 and 'copy to clipboard will not work' in p.stderr and 'xclip' in p.stderr,
                      plat, script, p.stderr)
    # WSL apps get the Windows PATH entries back (clip.exe, explorer.exe, wslview); the build does not use them.
    for plat in ['wsl', 'wsl2']:
        for script, args in [('run-tui', []), ('run-desktop', []), ('run-tray', []), ('run-local', ['tui'])]:
            p, calls = run(plat, script, *args, **dict(lib_env(full), PATH=no_tools_path + ':/mnt/c/Windows/System32'))
            app = [c for c in calls if c[0].startswith('built:') and c[1:2] != ['server']]
            check(p.returncode == 0 and '/mnt/c/Windows/System32' in app[-1][-1]['PATH'], plat, script, calls, p.stderr)
            check('links may not open from the app until wslview' in p.stderr, plat, script, p.stderr)

    # factory-reset keeps client.json unless --all; asks first; nothing to do without a client home.
    for plat in UNIX:
        fresh_home()
        p, calls = run(plat, 'factory-reset', '-y')
        check(p.returncode == 0 and 'Nothing to reset' in p.stdout, plat, p.stdout)
        client = home / '.xeno'
        (client / 'logs').mkdir(parents=True)
        (client / 'client.json').write_text('{}')
        (client / 'history.db').write_text('')
        p, calls = run(plat, 'factory-reset')
        check(p.returncode == 1 and 'Aborted' in p.stdout and (client / 'history.db').exists(), plat, p.stdout)
        p, calls = run(plat, 'factory-reset', stdin='YES\n')
        check(p.returncode == 0 and sorted(x.name for x in client.iterdir()) == ['client.json'], plat, p.stdout, p.stderr)
        p, calls = run(plat, 'factory-reset', '--all', '--yes')
        check(p.returncode == 0 and not client.exists(), plat, p.stderr)

    # macOS: install builds and installs Xeno.app (needs the macOS image tools).
    if platform.system() == 'Darwin' and shutil.which('iconutil') and shutil.which('sips'):
        fresh_home()
        e = dict(PATH=f'{bin_dir}:/usr/bin:/bin:/usr/sbin')
        p, calls = run('macos', 'install', '--no-dashboard', **e)
        app = home / 'Applications/Xeno.app'
        check(p.returncode == 0 and (app / 'Contents/MacOS/xeno-desktop').exists()
              and 'xeno-desktop' in (app / 'Contents/Info.plist').read_text(), p.stdout, p.stderr)
        check(dotnet(calls, 'publish') and dotnet(calls, 'publish')[0][dotnet(calls, 'publish')[0].index('-r') + 1].startswith('osx-'), calls)
        p, calls = run('macos', 'remove', **e)
        check(p.returncode == 0 and not app.exists(), p.stderr)

    # Shell syntax and shellcheck for every script; PowerShell parse check when pwsh is available.
    for path in sorted(root.glob('scripts/**/*.sh')):
        subprocess.run(['bash', '-n', str(path)], check=True)
    shellcheck = os.environ.get('SHELLCHECK') or shutil.which('shellcheck')
    if shellcheck:
        r = subprocess.run([shellcheck, '-S', 'warning', '-x', '-P', 'SCRIPTDIR',
                            *sorted(str(p) for p in root.glob('scripts/**/*.sh'))], cwd=root, capture_output=True, text=True)
        check(r.returncode == 0, r.stdout)
    pwsh = shutil.which('pwsh')
    if pwsh:
        for ps1 in root.glob('scripts/**/*.ps1'):
            path = str(ps1).replace("'", "''")
            r = subprocess.run([pwsh, '-NoProfile', '-Command',
                                f"$e = $null; [System.Management.Automation.Language.Parser]::ParseFile('{path}', [ref]$null, [ref]$e) | Out-Null; exit $e.Count"],
                               capture_output=True, text=True)
            check(r.returncode == 0, ps1, r.stdout, r.stderr)

print(f'{count} checks passed (shellcheck: {"yes" if shellcheck else "not found"}, pwsh: {"yes" if pwsh else "not found"}).')
