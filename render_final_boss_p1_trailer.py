import os
import subprocess
import shutil
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

WIDTH = 1920
HEIGHT = 1080
FFMPEG_EXE = imageio_ffmpeg.get_ffmpeg_exe()
RAW_VIDEO = r"c:\Users\gamer\Videos\Captures\Proyecto final Ilán Fischer 2026-11-18 15-28-44.mp4"

OUTPUT_PROJECT = os.path.abspath("Trailer_Space_Wars.mp4")
OUTPUT_DESKTOP = os.path.abspath(r"C:\Users\gamer\OneDrive\Escritorio\Trailer_Space_Wars.mp4")

# Load fonts
def get_font(path, sz):
    try:
        return ImageFont.truetype(path, sz)
    except:
        return ImageFont.load_default()

f_huge = get_font(r"C:\Windows\Fonts\impact.ttf", 115)
f_sub = get_font(r"C:\Windows\Fonts\segoeuib.ttf", 34)
f_card_title = get_font(r"C:\Windows\Fonts\segoeuib.ttf", 30)
f_card_sub = get_font(r"C:\Windows\Fonts\segoeuib.ttf", 22)
f_card_tag = get_font(r"C:\Windows\Fonts\segoeuib.ttf", 16)
f_btn = get_font(r"C:\Windows\Fonts\segoeuib.ttf", 28)

SCENES = [
    {
        "name": "c1_sector1_intro",
        "start": "00:00:00.8",
        "dur": "8.2",  # +5 segundos adicionales para leer con calma el aviso "Sector 1, incursión hostil detectada"
        "tag": "SECTOR 1 // INCURSIÓN HOSTIL",
        "title": "CUANDO LA GALAXIA CAE EN SILENCIO...",
        "subtitle": "UNA INVASIÓN HOSTIL HA DESPERTADO."
    },
    {
        "name": "c2_wave1",
        "start": "00:00:20.0",
        "dur": "5.5",
        "tag": "DESPLIEGUE DEFENSIVO",
        "title": "DESPLIEGA TU NAVE EN COMBATE",
        "subtitle": "ENFRÉNTATE AL ENJAMBRE ALIENÍGENA"
    },
    {
        "name": "c3_wave_mid",
        "start": "00:02:41.5",
        "dur": "5.5",
        "tag": "FUEGO CRUZADO",
        "title": "MANIOBRAS AL LÍMITE DE LA SUPERVIVENCIA",
        "subtitle": "ESQUIVA BARRERAS Y DEFIENDE TU NAVE"
    },
    {
        "name": "c4_wave_heavy",
        "start": "00:04:22.0",
        "dur": "6.0",
        "tag": "ASALTO MASIVO",
        "title": "CADA OLEADA SERÁ MÁS DESPIADADA",
        "subtitle": "RALENTIZA EL TIEMPO CON TU CAMPO DE FUERZA"
    },
    {
        "name": "c5_scream_shop",
        "start": "00:05:41.0",
        "dur": "5.5",
        "tag": "ARSENAL & MECÁNICA DE VOZ",
        "title": "¡GRITA PARA DESBLOQUEAR EL COMBATE!",
        "subtitle": "MEJORA TU NAVE Y ACTIVA EL MICRÓFONO PARA CONTINUAR"
    },
    {
        "name": "c6_boss_phase1_clean",
        "start": "00:06:28.0",
        "dur": "15.0",  # Exactamente minuto 6:28 hasta 6:43 pedido por el usuario (Fase 1 limpia)
        "tag": "AMENAZA TITÁNICA // FASE 1",
        "title": "UNA FUERZA COLOSAL EMERGE DE LAS SOMBRAS...",
        "subtitle": "SOBREVIVE AL PODER DEL LEVIATÁN"
    }
]

WORKDIR = "temp_trailer_p1_clean"
os.makedirs(WORKDIR, exist_ok=True)
processed_clips = []

print("=== 1. Extracting and Overlaying Balanced Clips (Boss Phase 1 Clean) ===")
bar_h = 44
for i, sc in enumerate(SCENES):
    raw_cut = f"{WORKDIR}/raw_{i}.mp4"
    ov_png = f"{WORKDIR}/ov_{i}.png"
    out_clip = f"{WORKDIR}/proc_{i}.mp4"

    # Step A: Cut clip directly from raw gameplay
    cmd_cut = [
        FFMPEG_EXE, "-y",
        "-ss", sc["start"],
        "-i", RAW_VIDEO,
        "-t", sc["dur"],
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
        "-c:a", "aac", "-b:a", "192k",
        raw_cut
    ]
    subprocess.run(cmd_cut, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    # Step B: Create narrative overlay image
    ov_img = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov_img)

    # Cinematic Letterbox
    d.rectangle([0, 0, WIDTH, bar_h], fill=(0, 0, 0, 255))
    d.rectangle([0, HEIGHT - bar_h, WIDTH, HEIGHT], fill=(0, 0, 0, 255))
    d.line([(0, bar_h), (WIDTH, bar_h)], fill=(56, 189, 248, 60), width=1)
    d.line([(0, HEIGHT - bar_h), (WIDTH, HEIGHT - bar_h)], fill=(56, 189, 248, 60), width=1)

    # Narrative card (centered bottom)
    cw, ch = 960, 110
    cx = (WIDTH - cw) // 2
    cy = HEIGHT - 180

    # Glass plate
    d.rounded_rectangle([cx, cy, cx + cw, cy + ch], radius=12, fill=(6, 10, 24, 225), outline=(56, 189, 248, 220), width=2)
    # Accent indicator
    d.rectangle([cx + 6, cy + 12, cx + 12, cy + ch - 12], fill=(251, 191, 36, 255))
    # Text
    d.text((cx + 28, cy + 12), f"[ {sc['tag']} ]", font=f_card_tag, fill=(251, 191, 36, 255))
    d.text((cx + 28, cy + 36), sc["title"], font=f_card_title, fill=(255, 255, 255, 255))
    d.text((cx + 28, cy + 74), sc["subtitle"], font=f_card_sub, fill=(56, 189, 248, 240))

    ov_img.save(ov_png)

    # Step C: Composite with FFmpeg
    cmd_ov = [
        FFMPEG_EXE, "-y",
        "-i", raw_cut,
        "-i", ov_png,
        "-filter_complex", "[0:v][1:v]overlay=0:0",
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
        "-c:a", "copy",
        out_clip
    ]
    subprocess.run(cmd_ov, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    processed_clips.append(out_clip)
    print(f"Scene {i+1}/{len(SCENES)}: {sc['title']} ready.")

# === 2. Create clean outro title card (NO technical bullets!) ===
print("=== 2. Creating Clean Outro Card ===")
outro_img = Image.new("RGB", (WIDTH, HEIGHT), (4, 8, 20))
d = ImageDraw.Draw(outro_img)

# Letterbox
d.rectangle([0, 0, WIDTH, bar_h], fill=(0, 0, 0, 255))
d.rectangle([0, HEIGHT - bar_h, WIDTH, HEIGHT], fill=(0, 0, 0, 255))

# Ambient glow
d.ellipse([WIDTH // 2 - 380, 430 - 380, WIDTH // 2 + 380, 430 + 380], fill=(12, 40, 80))

# Metallic styling lines
d.line([(380, 290), (WIDTH - 380, 290)], fill=(56, 189, 248, 230), width=3)
d.line([(380, 460), (WIDTH - 380, 460)], fill=(251, 191, 36, 230), width=3)

# Title: SPACE WARS
d.text((WIDTH // 2 - 150, 375), "SPACE", font=f_huge, fill=(56, 189, 248), anchor="mm")
d.text((WIDTH // 2 + 160, 375), "WARS", font=f_huge, fill=(251, 191, 36), anchor="mm")

# Subtitle
d.text((WIDTH // 2, 515), "PROTOCOLO DE DEFENSA GALÁCTICA", font=f_sub, fill=(248, 250, 252), anchor="mm")

# Clean CTA Button
btn_w, btn_h = 560, 76
btn_x = (WIDTH - btn_w) // 2
btn_y = 660
d.rounded_rectangle([btn_x, btn_y, btn_x + btn_w, btn_y + btn_h], radius=14, fill=(14, 165, 233), outline=(255, 255, 255), width=2)
d.text((WIDTH // 2, btn_y + btn_h // 2), "¡PREPÁRATE PARA LA BATALLA!", font=f_btn, fill=(255, 255, 255), anchor="mm")

outro_png = f"{WORKDIR}/outro.png"
outro_img.save(outro_png)

outro_mp4 = f"{WORKDIR}/outro.mp4"
subprocess.run([
    FFMPEG_EXE, "-y",
    "-loop", "1", "-i", outro_png,
    "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
    "-c:v", "libx264", "-t", "4.0", "-pix_fmt", "yuv420p",
    "-c:a", "aac", "-b:a", "192k", "-shortest",
    outro_mp4
], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
processed_clips.append(outro_mp4)

# === 3. Concatenate all scenes into final trailer ===
print("=== 3. Concatenating Master Trailer ===")
concat_txt = f"{WORKDIR}/concat.txt"
with open(concat_txt, "w") as f:
    for c in processed_clips:
        norm_c = c.replace(".mp4", "_norm.mp4")
        subprocess.run([
            FFMPEG_EXE, "-y", "-i", c,
            "-vf", "scale=1920:1080,fps=30",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
            "-c:a", "aac", "-ar", "48000", "-ac", "2",
            norm_c
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        f.write(f"file '{os.path.abspath(norm_c)}'\n")

cmd_concat = [
    FFMPEG_EXE, "-y",
    "-f", "concat", "-safe", "0",
    "-i", concat_txt,
    "-c", "copy",
    OUTPUT_PROJECT
]
subprocess.run(cmd_concat, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

if os.path.exists(OUTPUT_PROJECT):
    shutil.copy2(OUTPUT_PROJECT, OUTPUT_DESKTOP)
    print(f"SUCCESS! Master trailer updated at {OUTPUT_DESKTOP}")
    print(f"File size: {os.path.getsize(OUTPUT_PROJECT)} bytes")

# Clean temp directory
try:
    shutil.rmtree(WORKDIR)
except:
    pass
print("=== Trailer Rendering Complete! ===")

