import os
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=60,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
systemctl daemon-reload
systemctl list-units --type=service --state=running --no-legend 'qalaa-delegate*' | awk '{print $1}' | while read -r s; do
  systemctl restart "$s" || true
  echo restarted "$s"
done
sleep 3
systemctl is-active qalaa-delegatehaidernajaflast.service qalaa-delegatehaiderbasralast.service
"""
stdin, stdout, stderr = c.exec_command(cmd, timeout=180)
print(stdout.read().decode("utf-8", "replace"))
print(stderr.read().decode("utf-8", "replace")[:800])
c.close()
