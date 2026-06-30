"""Export last 3 slides of IRETP_Technical_Presentation.pptx to PNG via PowerPoint COM."""
import os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
import win32com.client

SRC = r"C:\Users\kalmi\IRETP\IRETP_Technical_Presentation.pptx"
OUT = r"C:\Users\kalmi\IRETP\tools\_slides_png"
os.makedirs(OUT, exist_ok=True)
# clean previous
for f in os.listdir(OUT):
    if f.endswith(".png"):
        os.remove(os.path.join(OUT, f))

ppt = win32com.client.Dispatch("PowerPoint.Application")
# PowerPoint requires window to be visible on some versions; try invisible first
try:
    ppt.Visible = 0
except Exception:
    pass

pres = ppt.Presentations.Open(SRC, WithWindow=False)
total = pres.Slides.Count
print(f"Total slides: {total}")
# Export only the last 3 (the new ones)
for i in range(total-2, total+1):
    slide = pres.Slides(i)
    out_path = os.path.join(OUT, f"slide_{i:02d}.png")
    # Export at 1920x1080
    slide.Export(out_path, "PNG", 1920, 1080)
    print(f"Exported slide {i} -> {out_path}")

pres.Close()
ppt.Quit()
print("Done.")
