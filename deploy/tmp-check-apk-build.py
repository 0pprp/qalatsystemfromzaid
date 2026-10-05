import os
import subprocess
from pathlib import Path

print(subprocess.check_output(["tasklist", "/FI", "PID eq 11704"], text=True, errors="replace"))
try:
    print(subprocess.check_output(["tasklist", "/FI", "IMAGENAME eq java.exe"], text=True, errors="replace")[:1000])
except Exception as e:
    print("java", e)

apk_dir = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\follower_application\build\app\outputs\flutter-apk")
print("apk_dir", apk_dir.exists(), list(apk_dir.glob("*.apk")) if apk_dir.exists() else None)
