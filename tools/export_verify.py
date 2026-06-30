import os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
import win32com.client

SRC = r"C:\Users\kalmi\IRETP\IRETP_Technical_Presentation.pptx"
OUT = r"C:\Users\kalmi\IRETP\tools\_slides_png"
os.makedirs(OUT, exist_ok=True)

ppt = win32com.client.Dispatch("PowerPoint.Application")
pres = ppt.Presentations.Open(SRC, WithWindow=False)
for i in [1, 17]:
    pres.Slides(i).Export(os.path.join(OUT, f"slide_{i:02d}.png"), "PNG", 1280, 720)
    print(f"Exported slide {i}")
pres.Close()
ppt.Quit()
