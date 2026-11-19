import os
from PIL import Image

for t in [28, 29, 30, 31, 32, 33]:
    im = Image.open(f"temp_trailer_frames/t_{t}.jpg")
    # Save preview of each second
    im.resize((960, 540)).save(f"temp_trailer_frames/preview_{t}.jpg")
print("Saved previews 28-33")

