import os,paramiko
c=paramiko.SSHClient();c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52",username="root",password=os.environ["SSH_PASS"],timeout=60,allow_agent=False,look_for_keys=False)
i,o,e=c.exec_command("systemctl restart qalaa-demo-backend; systemctl is-active qalaa-demo-backend",timeout=60)
print(o.read().decode()); c.close()
