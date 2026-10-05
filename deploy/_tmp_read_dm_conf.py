import os, paramiko
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
cmd=r'''
echo '=== zz-delegated ==='
cat /etc/systemd/system/qalaa-demo-sales-gateway.service.d/zz-delegated-manager.conf 2>/dev/null || cat /etc/systemd/system/qalaa-sales-gateway.service.d/zz-delegated-manager.conf
echo '=== env file head ==='
head -80 /etc/qalaa-demo/sales-gateway.env
echo '=== prod dropins ==='
ls /etc/systemd/system/qalaa-sales-gateway.service.d/
cat /etc/systemd/system/qalaa-sales-gateway.service.d/zz-delegated-manager.conf 2>/dev/null | head -40
'''
i,o,e=c.exec_command(cmd, timeout=30)
print(o.read().decode())
c.close()
