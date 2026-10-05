import os
import subprocess
import sys
import time
from pathlib import Path

# kill previous
subprocess.run(["taskkill", "/IM", "java.exe", "/F"], check=False)
subprocess.run(["taskkill", "/PID", "10340", "/T", "/F"], check=False)
time.sleep(2)

os.environ["JAVA_HOME"] = r"C:\Program Files\Android\Android Studio\jbr"
os.environ["PATH"] = os.environ["JAVA_HOME"] + r"\bin;" + os.environ.get("PATH", "")

cwd = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\follower_application")
log = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\deploy\tmp-apk-build.log")
flutter = r"C:\src\flutter\bin\flutter.bat"

with log.open("w", encoding="utf-8", errors="replace") as f:
    proc = subprocess.Popen(
        [flutter, "build", "apk", "--release", "-v"],
        cwd=str(cwd),
        stdout=f,
        stderr=subprocess.STDOUT,
        env=os.environ.copy(),
    )
    print("pid", proc.pid)
    last = 0
    for i in range(120):  # up to ~10 min, check every 5s
        time.sleep(5)
        size = log.stat().st_size
        print(f"t={i*5}s log={size} delta={size-last} alive={proc.poll()}")
        last = size
        if proc.poll() is not None:
            break
        # if no growth for 90s after first 60s, abort
        if i >= 12 and size == last:
            # check stagnation: need previous size comparison differently
            pass

    # stagnation: if still running after 8 min with tiny log growth in last 2 min
    if proc.poll() is None:
        # read tail
        text = log.read_text(encoding="utf-8", errors="replace")
        print("TAIL:\n", text[-2500:])
        print("still running, leaving process", proc.pid)
    else:
        print("exit", proc.returncode)
        text = log.read_text(encoding="utf-8", errors="replace")
        print("TAIL:\n", text[-2000:])
        apk = cwd / "build" / "app" / "outputs" / "flutter-apk" / "app-release.apk"
        if apk.exists() and proc.returncode == 0:
            dest = cwd / "follower.apk"
            dest.write_bytes(apk.read_bytes())
            print("copied", dest, dest.stat().st_size)
