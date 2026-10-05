import os, paramiko, pathlib
HOST="169.58.236.52"; SSH_PASS=os.environ["SSH_PASS"]
body=pathlib.Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\deploy\_remote_basra_payments_activity.py").read_text(encoding="utf-8")
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=SSH_PASS,timeout=60,allow_agent=False,look_for_keys=False)
sftp=c.open_sftp(); f=sftp.file("/tmp/basra_act.py","w"); f.write(body); f.close(); sftp.close()
i,o,e=c.exec_command("python3 /tmp/basra_act.py",timeout=180)
print(o.read().decode("utf-8","replace"))
err=e.read().decode("utf-8","replace")
if err.strip(): print("STDERR",err[-800:])
print("RC",o.channel.recv_exit_status()); c.close()
