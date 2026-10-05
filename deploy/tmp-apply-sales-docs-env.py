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
echo '=== drop-ins ==='
ls /etc/systemd/system/qalaa-delegatehaidernajaflast.service.d/ 2>/dev/null || echo none
cat /etc/systemd/system/qalaa-delegatehaidernajaflast.service.d/sales-docs.conf 2>/dev/null || echo no-conf
echo '=== systemctl show ==='
systemctl show qalaa-delegatehaidernajaflast.service -p Environment --no-pager | tr ' ' '\n' | grep -i SalesDocuments || echo 'SalesDocumentsRoot not in Environment'
# re-apply drop-ins quickly
python3 <<'PY'
from pathlib import Path
pairs = {
  'qalaa-delegatehaidernajaflast.service': '/opt/qalaa-runtime/AdminNajaf/App_Data',
  'qalaa-delegatehaiderbasralast.service': '/opt/qalaa-runtime/AdminBasra/App_Data',
  'qalaa-delegatehaideralanwarlast.service': '/opt/qalaa-runtime/AdminBasraAlanwar/App_Data',
  'qalaa-delegatehaiderdiyalalast.service': '/opt/qalaa-runtime/AdminDiyala/App_Data',
  'qalaa-delegatehaideralmuthannalast.service': '/opt/qalaa-runtime/AdminMuthanna/App_Data',
  'qalaa-delegatehaideralqadisiyyahlast.service': '/opt/qalaa-runtime/AdminDiwaniyah/App_Data',
  'qalaa-delegatehaiderbabillast.service': '/opt/qalaa-runtime/AdminHilla/App_Data',
  'qalaa-delegatehaiderkarbalalast.service': '/opt/qalaa-runtime/AdminKarbala/App_Data',
  'qalaa-delegatehaiderkirkuklast.service': '/opt/qalaa-runtime/AdminKirkuk/App_Data',
  'qalaa-delegatehaiderdhiqarlast.service': '/opt/qalaa-runtime/AdminNasiriyah/App_Data',
  'qalaa-delegatehaiderninevehlast.service': '/opt/qalaa-runtime/AdminMosul/App_Data',
  'qalaa-delegatehaiderkaraklast.service': '/opt/qalaa-runtime/AdminBaghdadKarkh/App_Data',
  'qalaa-delegate-haider-rusafa.service': '/opt/qalaa-runtime/AdminBaghdadRusafa/App_Data',
  'qalaa-delegatehaiderrusafalast.service': '/opt/qalaa-runtime/AdminBaghdadRusafa/App_Data',
  'qalaa-delegateaqeelkaraklast.service': '/opt/qalaa-runtime/AdminKarakAqeel/App_Data',
  'qalaa-delegateaqeelrusafalast.service': '/opt/qalaa-runtime/AdminRusafaAqeel/App_Data',
  'qalaa-delegateaqeelwasitlast.service': '/opt/qalaa-runtime/AdminKot/App_Data',
  'qalaa-delegateaqeelmaysanlast.service': '/opt/qalaa-runtime/AdminMaysan/App_Data',
}
n=0
for svc, root in pairs.items():
  unit = Path('/etc/systemd/system')/svc
  if not unit.exists():
    print('skip', svc); continue
  drop = Path('/etc/systemd/system')/(svc + '.d')
  drop.mkdir(parents=True, exist_ok=True)
  (drop/'sales-docs.conf').write_text('[Service]\nEnvironment=SalesDocumentsRoot='+root+'\n')
  print('ok', svc)
  n+=1
print('applied', n)
PY
systemctl daemon-reload
systemctl restart qalaa-delegatehaidernajaflast.service
sleep 2
systemctl show qalaa-delegatehaidernajaflast.service -p Environment --no-pager | tr ' ' '\n' | grep -i SalesDocuments || echo still-missing
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=120)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[:500])
c.close()
