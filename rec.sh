#!/bin/bash
# usage: rec.sh <seconds> : records the desktop, then builds gen/sheet_N.png contact sheets of the game area
cd /c/mod/work/b84_1823
rm -f gen/rec.mp4
ffmpeg -y -loglevel error -f gdigrab -framerate 8 -t ${1:-45} -i desktop -vf "scale=1280:800" -c:v libx264 -preset ultrafast gen/rec.mp4
rm -f gen/fr_*.png
ffmpeg -y -loglevel error -i gen/rec.mp4 -vf "fps=1/${2:-2},crop=960:540:160:130,scale=480:270" gen/fr_%02d.png
python - <<'PY'
from PIL import Image
import glob
fs=sorted(glob.glob('gen/fr_*.png'))
for k in range(0,len(fs),12):
    chunk=fs[k:k+12]
    W,H=480,270
    sh=Image.new('RGB',(W*3,H*4))
    for i,f in enumerate(chunk): sh.paste(Image.open(f),((i%3)*W,(i//3)*H))
    sh.save(f'gen/sheet_{k//12}.png')
print(len(fs),'frames')
PY
