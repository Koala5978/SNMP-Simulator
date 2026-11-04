"""Independent PySNMP client against the real UDP test host; no product CLI added."""
import asyncio
import os
from pathlib import Path
import socket
import subprocess
from pysnmp.hlapi.v3arch.asyncio import (
    SnmpEngine, CommunityData, UdpTransportTarget, ContextData,
    ObjectType, ObjectIdentity, OctetString, get_cmd, next_cmd, bulk_cmd, set_cmd,
)

async def main():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reservation:
        reservation.bind(('127.0.0.1', 0))
        port = reservation.getsockname()[1]
    dll = Path('tests/SnmpSimulator.Smoke/bin/Release/net10.0/SnmpSimulator.Smoke.dll')
    process = subprocess.Popen([os.environ.get('DOTNET_EXE', 'dotnet'), str(dll), 'samples/device.json', '--serve', str(port)], stdout=subprocess.PIPE, text=True)
    engine = SnmpEngine()
    checks = 0
    def check(ok, label):
        nonlocal checks
        if not ok:
            raise AssertionError(label)
        checks += 1
        print('PASS:', label)
    try:
        ready = await asyncio.wait_for(asyncio.to_thread(process.stdout.readline), 15)
        check(ready.strip() == 'READY', 'real UDP server ready')
        target = await UdpTransportTarget.create(('127.0.0.1', port), timeout=1, retries=0)
        for version in (0, 1):
            auth = CommunityData('public', mpModel=version)
            result = await get_cmd(engine, auth, target, ContextData(), ObjectType(ObjectIdentity('1.3.6.1.2.1.1.5.0')), lookupMib=False)
            error, status, _, values = result
            check(not error and not status and str(values[0][1]) == 'demo-switch-01', f'v{version + 1} GET')
            cursor = '1.3.6.1.2.1'
            found = []
            for _ in range(10):
                error, status, _, values = await next_cmd(engine, auth, target, ContextData(), ObjectType(ObjectIdentity(cursor)), lookupMib=False)
                if status or str(values[0][1]) == 'No more variables left in this MIB View':
                    break
                check(not error, 'GETNEXT transport')
                new_oid = str(values[0][0])
                if new_oid == cursor:
                    break
                found.append(new_oid); cursor = new_oid
            check(len(found) == 7, f'v{version + 1} walk seven OIDs')
            error, status, _, _ = await set_cmd(engine, auth, target, ContextData(), ObjectType(ObjectIdentity('1.3.6.1.2.1.1.5.0'), OctetString('changed')), lookupMib=False)
            check(not error and int(status) in (4, 17), 'SET rejected')
        error, status, _, values = await bulk_cmd(engine, CommunityData('public'), target, ContextData(), 0, 10, ObjectType(ObjectIdentity('1.3.6.1.2.1')), lookupMib=False)
        check(not error and not status and len(values) >= 7, 'v2c GETBULK')
        error, _, _, _ = await get_cmd(engine, CommunityData('wrong'), target, ContextData(), ObjectType(ObjectIdentity('1.3.6.1.2.1.1.5.0')), lookupMib=False)
        check(error is not None, 'wrong community times out')
        print(f'Completed {checks} independent-client checks.')
    finally:
        engine.close_dispatcher()
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill(); process.wait()

asyncio.run(main())
