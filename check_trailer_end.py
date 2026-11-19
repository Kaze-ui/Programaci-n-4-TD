import os
from PIL import Image

for f in sorted(os.listdir("temp_trailer_frames")):
    p = os.path.join("temp_trailer_frames", f)
    im = Image.open(p)
    # Check top
    t = im.crop((0, 0, 1920, 300)).resize((1, 1)).getpixel((0, 0))
    # Check center
    c = im.crop((500, 300, 1400, 800)).resize((1, 1)).getpixel((0, 0))
    print(f"{f}: top={t}, center={c}")

