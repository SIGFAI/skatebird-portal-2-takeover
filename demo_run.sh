#!/bin/bash
# plays the demo like the operators: start with -Demo, wait for READY, raise the flag, then screenshot
cd /c/mod/work/b84_1823
powershell -NoProfile -ExecutionPolicy Bypass -File kits/skatebird/stop.ps1 >/dev/null 2>&1
echo 0 > /c/mod/work/skatebird-rec.txt
powershell -NoProfile -ExecutionPolicy Bypass -File kits/skatebird/play.ps1 -Mod C:/mod/work/b84_1823/release -Demo >/dev/null 2>&1
for i in $(seq 1 80); do grep -q SIGF_READY /c/mod/work/skatebird/game/BepInEx/LogOutput.log 2>/dev/null && break; sleep 2; done
sleep 3
echo 1 > /c/mod/work/skatebird-rec.txt
echo started
