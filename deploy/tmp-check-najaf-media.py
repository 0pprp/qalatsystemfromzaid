import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=30,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
echo '=== najaf company WorkingDirectory ==='
systemctl show qalaa-company-sharenewnajaf.service -p WorkingDirectory -p EnvironmentFiles --no-pager
echo '=== najaf delegate ==='
systemctl show qalaa-delegatehaidernajaflast.service -p WorkingDirectory -p EnvironmentFiles --no-pager
echo '=== sample shop files ==='
ls /opt/qalaa-runtime/AdminNajaf/App_Data/sales 2>/dev/null | head -5
find /opt/qalaa-runtime/AdminNajaf/App_Data/sales -type f 2>/dev/null | head -8
echo '=== sample keys from sql via env ==='
python3 - <<'PY'
import subprocess,re
text=open('/etc/qalaa-production/delegated-manager.env',encoding='utf-8',errors='replace').read()
cs=None
for line in text.splitlines():
    if line.startswith('ConnectionStrings__SalesGateway='):
        cs=line.split('=',1)[1].strip().strip('"').strip("'")
# try najaf company env
import glob
for p in glob.glob('/etc/qalaa-production/*najaf*')+glob.glob('/etc/systemd/system/qalaa-company-sharenewnajaf.service'):
    print('CFG',p)
print('---')
# read WorkingDirectory file App_Data sample key from DB if possible
envp='/etc/qalaa-production/company-sharenewnajaf.env'
import os
if os.path.exists(envp):
    print(open(envp,encoding='utf-8',errors='replace').read()[:1500])
else:
    print('no',envp)
    print(open('/etc/systemd/system/qalaa-company-sharenewnajaf.service',encoding='utf-8',errors='replace').read()[:2000])
PY
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace")[:6000])
print(stderr.read().decode("utf-8", "replace")[:1000])
c.close()
