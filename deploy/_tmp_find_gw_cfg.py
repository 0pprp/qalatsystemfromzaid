import os, paramiko
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
cmd=r'''
ls -la /opt/qalaa-demo/sales-gateway | head -40
echo '---'
ls /etc/systemd/system/qalaa-demo-sales-gateway.service.d 2>/dev/null
ls /etc/systemd/system/qalaa-sales-gateway.service.d 2>/dev/null
echo '---'
systemctl cat qalaa-demo-sales-gateway | head -80
echo '---'
grep -R "MainFollower\|DelegatedManagerAccounts\|ASPNETCORE" /etc/qalaa-demo /etc/qalaa-production /etc/systemd/system/qalaa-*sales-gateway* 2>/dev/null | head -40
'''
i,o,e=c.exec_command(cmd, timeout=30)
print(o.read().decode())
c.close()
