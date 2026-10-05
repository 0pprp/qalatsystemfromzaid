import os, paramiko
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
cmd=r'''
python3 - <<'PY'
import json, urllib.request, subprocess, time

def ensure_accounts(path):
    lines=open(path,'r',encoding='utf-8').read().splitlines()
    # remove old MainFollower lines
    lines=[ln for ln in lines if not ln.startswith('MainFollowerAccounts__')]
    block=[
        'MainFollowerAccounts__0__UserName=mf1',
        'MainFollowerAccounts__0__Password=Mf@12345',
        'MainFollowerAccounts__0__DisplayName=متابع رئيسي 1',
        'MainFollowerAccounts__1__UserName=mf2',
        'MainFollowerAccounts__1__Password=Mf@12345',
        'MainFollowerAccounts__1__DisplayName=متابع رئيسي 2',
    ]
    if lines and lines[-1].strip():
        lines.append('')
    lines.extend(block)
    open(path,'w',encoding='utf-8').write('\n'.join(lines)+'\n')
    print('WROTE', path)

ensure_accounts('/etc/qalaa-demo/sales-gateway.env')
# production may use env file too
prod='/etc/qalaa-production/sales-gateway.env'
import os
if os.path.isfile(prod):
    ensure_accounts(prod)
else:
    # write dedicated file + drop-in
    os.makedirs('/etc/qalaa-production', exist_ok=True)
    open('/etc/qalaa-production/main-follower.env','w',encoding='utf-8').write(
        'MainFollowerAccounts__0__UserName=mf1\n'
        'MainFollowerAccounts__0__Password=Mf@12345\n'
        'MainFollowerAccounts__0__DisplayName=متابع رئيسي 1\n'
        'MainFollowerAccounts__1__UserName=mf2\n'
        'MainFollowerAccounts__1__Password=Mf@12345\n'
        'MainFollowerAccounts__1__DisplayName=متابع رئيسي 2\n'
    )
    drop='/etc/systemd/system/qalaa-sales-gateway.service.d/zz-main-follower.conf'
    open(drop,'w').write('[Service]\nEnvironmentFile=/etc/qalaa-production/main-follower.env\n')
    print('WROTE drop-in', drop)
    # also demo drop-in not needed if env file has it
subprocess.check_call(['systemctl','daemon-reload'])
subprocess.check_call(['systemctl','restart','qalaa-demo-sales-gateway','qalaa-sales-gateway'])
time.sleep(3)

def login(base):
    body=json.dumps({'userName':'mf1','password':'Mf@12345'}).encode()
    req=urllib.request.Request(base+'/Auth/LoginMainFollower', data=body, headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req, timeout=20) as r:
        return json.loads(r.read().decode())

for base in ['http://127.0.0.1:5403/api','http://127.0.0.1:5280/api']:
    try:
        data=login(base)
        print('LOGIN_OK', base, data.get('userType'), data.get('userName'))
        tok=data['token']
        # create committee as DM then list as MF - need DM login
        dm_body=json.dumps({'userName':'dm','password':'dm'}).encode()
        req=urllib.request.Request(base+'/Auth/LoginDelegatedManager', data=dm_body, headers={'Content-Type':'application/json'})
        with urllib.request.urlopen(req, timeout=20) as r:
            dm=json.loads(r.read().decode())
        dmt=dm['token']
        cb=json.dumps({'name':'لجنة اختبار','memberUserNames':['mf1','mf2']}).encode()
        req=urllib.request.Request(base+'/delegated-manager/committees', data=cb, headers={'Content-Type':'application/json','Authorization':'Bearer '+dmt})
        with urllib.request.urlopen(req, timeout=20) as r:
            committee=json.loads(r.read().decode())
        print('COMMITTEE', committee.get('id'))
        mb=json.dumps({'committeeId':committee['id'],'cityValues':['DatabaseCompanyBasra','DatabaseCompanyNajaf'],'note':'مرحلة 1'}).encode()
        req=urllib.request.Request(base+'/delegated-manager/committees/missions', data=mb, headers={'Content-Type':'application/json','Authorization':'Bearer '+dmt})
        with urllib.request.urlopen(req, timeout=20) as r:
            mission=json.loads(r.read().decode())
        print('MISSION', mission.get('id'), mission.get('cityValues'))
        req=urllib.request.Request(base+'/main-follower/missions', headers={'Authorization':'Bearer '+tok})
        with urllib.request.urlopen(req, timeout=20) as r:
            rows=json.loads(r.read().decode())
        print('MF_MISSIONS', len(rows), rows[0].get('committeeName') if rows else None)
        break
    except Exception as e:
        print('FAIL', base, e)
        if hasattr(e, 'read'):
            print(e.read().decode()[:400])
PY
'''
i,o,e=c.exec_command(cmd, timeout=120)
print(o.read().decode())
print(e.read().decode()[-800:])
c.close()
