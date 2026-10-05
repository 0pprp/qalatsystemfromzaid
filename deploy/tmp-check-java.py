import subprocess
import time
from pathlib import Path

print(subprocess.check_output(["tasklist", "/FI", "IMAGENAME eq java.exe"], text=True, errors="replace"))
apk = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\follower_application\build\app\outputs\flutter-apk\app-release.apk")
if apk.exists():
    print("apk_size", apk.stat().st_size, "mtime", time.ctime(apk.stat().st_mtime))
else:
    print("no apk")
print("term646970", Path(r"C:\Users\Marvel\.cursor\projects\c-GitHub-SalesHaider-main\terminals\646970.txt").stat().st_size)
