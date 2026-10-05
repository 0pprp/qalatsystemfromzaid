import os, json, urllib.request, paramiko
# smoke via server localhost to demo gateway
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
cmd=r'''
python3 - <<'PY'
import json,urllib.request
def post(url, body):
    req=urllib.request.Request(url, data=json.dumps(body).encode(), headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req, timeout=20) as r:
        return r.status, json.loads(r.read().decode())
# try common local ports
for base in ['http://127.0.0.1:8080/sales-gw/api','http://127.0.0.1:5000/api','http://127.0.0.1:5280/api']:
    try:
        st, data = post(base+'/Auth/LoginMainFollower', {'userName':'mf1','password':'Mf@12345'})
        print('LOGIN', base, st, data.get('userType'), data.get('userName'), 'token' in data)
        tok=data['token']
        req=urllib.request.Request(base+'/main-follower/missions', headers={'Authorization':'Bearer '+tok})
        with urllib.request.urlopen(req, timeout=20) as r:
            rows=json.loads(r.read().decode())
        print('MISSIONS', len(rows) if isinstance(rows,list) else rows)
        break
    except Exception as e:
        print('TRY_FAIL', base, type(e).__name__, e)
PY
'''
i,o,e=c.exec_command(cmd, timeout=60)
print(o.read().decode())
print(e.read().decode()[-800:])
c.close()
