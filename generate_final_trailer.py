import os
import subprocess
import shutil
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

WIDTH = 1920
HEIGHT = 1080
FFMPEG_EXE = imageio_ffmpeg.get_ffmpeg_exe()

OUTPUT_PROJECT = os.path.abspath("Trailer_Space_Wars.mp4")
OUTPUT_DESKTOP = os.path.abspath(r"C:\Users\gamer\OneDrive\Escritorio\Trailer_Space_Wars.mp4")

# Fonts
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
        "clip": "temp_clips/clip1_menu.mp4",
        "tag": "SECTOR 7 // ESPACIO PROFUNDO",
        "title": "CUANDO LA GALAXIA CAE EN SILENCIO...",
        "subtitle": "UNA INVASIÓN HOSTIL HA DESPERTADO."
    },
    {
        "clip": "temp_clips/clip2_dogfight.mp4",
        "tag": "DESPLIEGUE DEFENSIVO",
        "title": "ENFRÉNTATE AL ENJAMBRE ALIENÍGENA",
        "subtitle": "MANIOBRAS AL LÍMITE Y COMBATE DE ALTA VELOCIDAD"
    },
    {
        "clip": "temp_clips/clip3_slowdown.mp4",
        "tag": "TECNOLOGÍA EXPERIMENTAL",
        "title": "CUANDO TODO PARECE PERDIDO...",
        "subtitle": "ACTIVA EL CAMPO DE FUERZA Y CONTROLA EL TIEMPO"
    },
    {
        "clip": "temp_clips/clip4_boss_intro.mp4",
        "tag": "AMENAZA TITÁNICA",
        "title": "UNA FUERZA COLOSAL EMERGE DE LAS SOMBRAS",
        "subtitle": "LA NAVE NODRIZA LEVIATÁN"
    },
    {
        "clip": "temp_clips/clip5_alert_p2.mp4",
        "tag": "ALERTA ROJA // NIVEL CRÍTICO",
        "title": "NUEVOS ENEMIGOS DETECTADOS: FASE 2",
        "subtitle": "ESCUDO IMPENETRABLE Y ESCOLTAS TIER 4"
    },
    {
        "clip": "temp_clips/clip6_hyperbeam.mp4",
        "tag": "PELIGRO MÁXIMO",
        "title": "SOBREVIVE A SU ARMA DEFINITIVA",
        "subtitle": "EL HIPER RAYO QUE DESTRUYE TODO A SU PASO"
    },
    {
        "clip": "temp_clips/clip7_boss_death.mp4",
        "tag": "IMPACTO FINAL",
        "title": "DESTRUYE EL NÚCLEO... Y SALVA LA GALAXIA",
        "subtitle": "VICTORIA DE LA FLOTA DEFENSIVA"
    }
]

os.makedirs("temp_narrative", exist_ok=True)
processed_clips = []

print("1. Creating narrative overlay cards...")
for i, sc in enumerate(SCENES):
    ov_img = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov_img)

    # Letterbox cinematic bars
    bar_h = 44
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

    ov_path = f"temp_narrative/ov_{i}.png"
    ov_img.save(ov_path)

    # Apply overlay using native FFmpeg
    out_clip = f"temp_narrative/clip_{i}.mp4"
    print(f"Applying overlay {i+1}/{len(SCENES)}: {sc['title']}...")
    cmd = [
        FFMPEG_EXE, "-y",
        "-i", sc["clip"],
        "-i", ov_path,
        "-filter_complex", "[0:v][1:v]overlay=0:0",
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
        "-c:a", "copy",
        out_clip
    ]
    subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    processed_clips.append(out_clip)

# 2. Create clean outro title card (NO technical bullets!)
print("2. Creating clean outro title card...")
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

# CTA Button (clean, cinema style, no bullet points!)
btn_w, btn_h = 560, 76
btn_x = (WIDTH - btn_w) // 2
btn_y = 660
d.rounded_rectangle([btn_x, btn_y, btn_x + btn_w, btn_y + btn_h], radius=14, fill=(14, 165, 233), outline=(255, 255, 255), width=2)
d.text((WIDTH // 2, btn_y + btn_h // 2), "¡PREPÁRATE PARA LA BATALLA!", font=f_btn, fill=(255, 255, 255), anchor="mm")

outro_img_path = "temp_narrative/outro.png"
outro_img.save(outro_img_path)

outro_mp4 = "temp_narrative/outro.mp4"
subprocess.run([
    FFMPEG_EXE, "-y",
    "-loop", "1", "-i", outro_img_path,
    "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
    "-c:v", "libx264", "-t", "4.0", "-pix_fmt", "yuv420p",
    "-c:a", "aac", "-b:a", "192k", "-shortest",
    outro_mp4
], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
processed_clips.append(outro_mp4)

# 3. Concatenate all normalized clips
print("3. Concatenating all scenes into master trailer...")
concat_list_file = "temp_narrative/concat.txt"
with open(concat_list_file, "w") as f:
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

cmd = [
    FFMPEG_EXE, "-y",
    "-f", "concat", "-safe", "0",
    "-i", concat_list_file,
    "-c", "copy",
    OUTPUT_PROJECT
]
subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

if os.path.exists(OUTPUT_PROJECT):
    shutil.copy2(OUTPUT_PROJECT, OUTPUT_DESKTOP)
    print(f"SUCCESS! Master trailer updated at {OUTPUT_DESKTOP} and {OUTPUT_PROJECT}")
    print(f"File size: {os.path.getsize(OUTPUT_PROJECT)} bytes")

