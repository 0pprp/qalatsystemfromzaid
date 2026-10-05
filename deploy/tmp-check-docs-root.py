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
set -e
echo '=== SalesDocumentsRoot ==='
grep -R SalesDocumentsRoot /etc/qalaa-production /opt/qalaa-runtime --include='*.json' --include='*.env' -n 2>/dev/null | head -40 || true
echo '=== runtime dirs ==='
ls /opt/qalaa-runtime/ 2>/dev/null | head -50 || true
echo '=== App_Data ==='
find /opt/qalaa-runtime -maxdepth 3 -type d -name App_Data 2>/dev/null | head -30 || true
echo '=== sales folders sample ==='
find /opt/qalaa-runtime -maxdepth 5 -type d -path '*/App_Data/sales' 2>/dev/null | head -20 || true
echo '=== delegate appsettings ==='
find /opt/qalaa-runtime -iname '*Delegate*' -maxdepth 2 -type d 2>/dev/null | head -20 || true
ls /opt/qalaa-runtime/DelegateHaiderNajafLast 2>/dev/null | head || true
grep -n SalesDocumentsRoot /opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings*.json 2>/dev/null || true
grep -n ContentRoot /etc/systemd/system/*delegate* 2>/dev/null | head || true
systemctl list-units --type=service --all 2>/dev/null | grep -iE 'delegate|company|qalaa' | head -40 || true
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=90)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err.strip():
    print("STDERR", err[:1000])
c.close()
