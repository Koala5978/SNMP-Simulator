"""Fail-closed security report gates. Standard library only; project MIT license."""
import argparse
import json
import os
from pathlib import Path
import urllib.request
import urllib.parse
import subprocess
import sys
import xml.etree.ElementTree as ET


def nuget_gate(data):
    if data.get('version') != 1 or not isinstance(data.get('projects'), list):
        raise ValueError('Unrecognized NuGet audit report')
    if data.get('problems') or data.get('errors'):
        raise ValueError('NuGet audit reported errors/problems')
    high = []
    for project in data['projects']:
        if project.get('problems') or project.get('errors'):
            raise ValueError('Project audit could not complete')
        for framework in project.get('frameworks', []):
            for key in ('topLevelPackages', 'transitivePackages'):
                for package in framework.get(key, []):
                    for vuln in package.get('vulnerabilities', []):
                        severity = vuln.get('severity', '').lower()
                        if severity not in ('low', 'moderate', 'high', 'critical'):
                            raise ValueError('Unknown advisory severity')
                        if severity in ('high', 'critical'):
                            high.append((package['id'], vuln))
    return high


def sarif_gate(data):
    runs = data.get('runs')
    if data.get('version') != '2.1.0' or not isinstance(runs, list) or not runs:
        raise ValueError('Missing SARIF analysis runs')
    high = []
    for run in runs:
        if any(not i.get('executionSuccessful', True) for i in run.get('invocations', [])):
            raise ValueError('SAST invocation failed')
        rules = {r['id']: r for r in run['tool']['driver'].get('rules', [])}
        for result in run.get('results', []):
            rule = rules.get(result.get('ruleId'))
            if rule is None:
                raise ValueError('Cannot classify a SAST result without its rule metadata')
            score = rule.get('properties', {}).get('security-severity')
            # CodeQL security severity: High >=7.0, Critical >=9.0.
            if (score is not None and float(score) >= 7) or result.get('level') == 'error':
                high.append(result.get('ruleId', 'unknown'))
    return high


def command(args, output):
    result = subprocess.run(args, text=True, capture_output=True)
    output.write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'{args[0]} failed ({result.returncode}); see {output}')
    return result.stdout


def sca(output):
    output.mkdir(parents=True, exist_ok=True)
    dotnet = os.environ.get('DOTNET_EXE', 'dotnet')
    # Runtime packs are not normal PackageReferences: restore an audit-only project.
    app = ET.parse('src/SnmpSimulator.App/SnmpSimulator.App.csproj')
    version = app.findtext('.//RuntimeFrameworkVersion')
    framework = app.findtext('.//TargetFramework').split('-')[0]
    if not version:
        raise ValueError('An exact runtime version must be inventoried')
    audit = output / 'runtime-project'
    audit.mkdir(exist_ok=True)
    (audit / 'runtime.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>{framework}</TargetFramework><EnableWindowsTargeting>true</EnableWindowsTargeting>
</PropertyGroup><ItemGroup>
<PackageReference Include="Microsoft.NETCore.App.Runtime.win-x64" Version="{version}" PrivateAssets="all" ExcludeAssets="all" />
<PackageReference Include="Microsoft.WindowsDesktop.App.Runtime.win-x64" Version="{version}" PrivateAssets="all" ExcludeAssets="all" />
</ItemGroup></Project>''', encoding='utf-8')
    projects = sorted(Path('src').rglob('*.csproj')) + sorted(Path('tests').rglob('*.csproj')) + [audit / 'runtime.csproj']
    blockers = []
    for index, project in enumerate(projects):
        restore = command([dotnet, 'restore', str(project), '-p:NuGetAudit=true', '-p:NuGetAuditMode=all'], output / f'restore-{index}.log')
        if any(code in restore for code in ('NU1900', 'NU1905')):
            raise RuntimeError('NuGet advisory source could not be queried')
        inventory_text = command([dotnet, 'package', 'list', '--project', str(project), '--include-transitive', '--format', 'json', '--output-version', '1'], output / f'inventory-{index}.log')
        inventory = json.loads(inventory_text)
        (output / f'inventory-{index}.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
        if project == audit / 'runtime.csproj':
            packages = {p['id'].lower() for pr in inventory['projects'] for f in pr.get('frameworks', []) for p in f.get('topLevelPackages', [])}
            expected = {'microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64'}
            if not expected.issubset(packages):
                raise ValueError('Runtime packages missing from audit inventory')
        stdout = command([dotnet, 'package', 'list', '--project', str(project), '--vulnerable', '--include-transitive', '--format', 'json', '--output-version', '1'], output / f'nuget-{index}.log')
        # Parse the report strictly; scanner/feed failures are never a clean result.
        data = json.loads(stdout)
        (output / f'nuget-{index}.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
        blockers.extend(nuget_gate(data))
    # Resolve all wheel dependencies without installing or executing source builds.
    report = output / 'python-resolution.json'
    command([sys.executable, '-m', 'pip', 'install', '--dry-run', '--ignore-installed',
             '--only-binary=:all:', '--report', str(report), '-r',
             'scripts/interop-requirements.txt'], output / 'pip-resolve.log')
    resolution = json.loads(report.read_text(encoding='utf-8'))
    if resolution.get('version') != '1' or not resolution.get('install'):
        raise ValueError('Python resolver produced no dependency inventory')
    python_report = []
    for package in resolution['install']:
        metadata = package['metadata']
        name, package_version = metadata['name'], metadata['version']
        url = 'https://pypi.org/pypi/' + urllib.parse.quote(name, safe='') + '/' + urllib.parse.quote(package_version, safe='') + '/json'
        request = urllib.request.Request(url, headers={'User-Agent': 'SNMP-Simulator-security-audit'})
        with urllib.request.urlopen(request, timeout=30) as response:
            advisory = json.load(response)
        if 'vulnerabilities' not in advisory or not isinstance(advisory['vulnerabilities'], list):
            raise ValueError('PyPI advisory data unavailable')
        vulns = [v for v in advisory['vulnerabilities'] if not v.get('withdrawn')]
        python_report.append({'name': name, 'version': package_version, 'vulnerabilities': vulns})
        # PyPI advisories do not uniformly supply severity: block every active advisory.
        blockers.extend((name, v['id']) for v in vulns)
    (output / 'python.json').write_text(json.dumps(python_report, indent=2), encoding='utf-8')
    (output / 'summary.json').write_text(json.dumps({'runtime_version': version, 'projects': [str(p) for p in projects], 'blockers': blockers}, indent=2), encoding='utf-8')
    if blockers:
        raise RuntimeError(f'{len(blockers)} dependency findings block this check; inspect reports')
    print('SCA passed: NuGet/runtime High/Critical gate and Python PyPI all-advisory gate.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('sca', 'sarif'))
    parser.add_argument('--output', default='artifacts/security/sca')
    parser.add_argument('--sarif-dir', default='artifacts/security/codeql')
    args = parser.parse_args()
    if args.mode == 'sca':
        sca(Path(args.output))
    else:
        paths = list(Path(args.sarif_dir).rglob('*.sarif'))
        if not paths:
            raise ValueError('SAST produced no SARIF files')
        blockers = [r for p in paths for r in sarif_gate(json.loads(p.read_text(encoding='utf-8')))]
        if blockers:
            raise RuntimeError(f'SAST blocked by {len(blockers)} findings: {blockers}')
        print(f'SAST gate passed for {len(paths)} SARIF reports.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, RuntimeError, OSError) as ex:
        print(f'Security check failed: {ex}', file=sys.stderr)
        sys.exit(1)
