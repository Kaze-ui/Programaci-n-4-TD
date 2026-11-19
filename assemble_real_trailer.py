import os
import sys
import subprocess
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

# ==============================================================================
# CONFIGURATION
# ==============================================================================
WIDTH = 1920
HEIGHT = 1080
FPS = 30 # Standard cinematic framerate

FONT_TITLE_PATH = r"C:\Windows\Fonts\impact.ttf"
FONT_BODY_PATH = r"C:\Windows\Fonts\segoeuib.ttf"
FONT_BACKUP_PATH = r"C:\Windows\Fonts\arialbd.ttf"

def get_font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except:
        try:
            return ImageFont.truetype(FONT_BODY_PATH, size)
        except:
            return ImageFont.load_default()

font_title_huge = get_font(FONT_TITLE_PATH, 110)
font_title_large = get_font(FONT_TITLE_PATH, 74)
font_subtitle = get_font(FONT_BODY_PATH, 34)
font_body = get_font(FONT_BODY_PATH, 28)
font_small = get_font(FONT_BODY_PATH, 21)
font_micro = get_font(FONT_BODY_PATH, 16)

FFMPEG_EXE = imageio_ffmpeg.get_ffmpeg_exe()
OUTPUT_PROJECT_PATH = os.path.abspath("Trailer_Space_Wars.mp4")
OUTPUT_DESKTOP_PATH = os.path.abspath(r"C:\Users\gamer\OneDrive\Escritorio\Trailer_Space_Wars.mp4")

# ==============================================================================
# CLIP DEFINITIONS & OVERLAYS
# ==============================================================================
# (file_path, duration_seconds, badge_tag, badge_title, badge_subtitle)
SCENES = [
    {
        "clip": "temp_clips/clip1_menu.mp4",
        "dur": 3.0,
        "tag": "MISIÓN CLASIFICADA",
        "title": "SECTOR 7 // PROTOCOLO DE DEFENSA",
        "subtitle": "CUANDO LA GALAXIA CAE EN SILENCIO..."
    },
    {
        "clip": "temp_clips/clip2_dogfight.mp4",
        "dur": 6.0,
        "tag": "MODO 1 JUGADOR & CO-OP 2P LOCAL",
        "title": "COMBATE ESPACIAL DE ALTA VELOCIDAD",
        "subtitle": "ARSENAL TÁCTICO // BLÁSTERS & PLASMA CARGADO"
    },
    {
        "clip": "temp_clips/clip3_slowdown.mp4",
        "dur": 7.5,
        "tag": "MECÁNICA EXCLUSIVA",
        "title": "CAMPO DE FUERZA TÁCTICO // TIEMPO BALA",
        "subtitle": "RALENTIZA EL FUEGO ENEMIGO EN TIEMPO REAL"
    },
    {
        "clip": "temp_clips/clip4_boss_intro.mp4",
        "dur": 7.0,
        "tag": "AMENAZA TITÁNICA DETECTADA",
        "title": "NAVE NODRIZA LEVIATÁN // FASE 1",
        "subtitle": "PATRONES DE BALAS QUÍNTUPLES Y MISILES DE RACIMO"
    },
    {
        "clip": "temp_clips/clip5_alert_p2.mp4",
        "dur": 5.5,
        "tag": "ALERTA ROJA EN EL SECTOR",
        "title": "NUEVOS ENEMIGOS DETECTADOS // FASE 2",
        "subtitle": "ESCUDO ENERGÉTICO AZUL (-30% DAÑO) & ESCOLTAS TIER 4"
    },
    {
        "clip": "temp_clips/clip6_hyperbeam.mp4",
        "dur": 8.5,
        "tag": "PELIGRO MÁXIMO",
        "title": "SOBREVIVE AL HIPER RAYO DESTRUCTOR",
        "subtitle": "SUPER LÁSER CONTINUO & BARRIDO MORTAL DE 180°"
    },
    {
        "clip": "temp_clips/clip7_boss_death.mp4",
        "dur": 6.5,
        "tag": "CLÍMAX DE LA BATALLA",
        "title": "COLAPSO DEL NÚCLEO ENEMIGO",
        "subtitle": "DETONACIÓN EN CADENA & SUPERNOVA"
    }
]

OUTRO_DURATION = 4.5

# ==============================================================================
# DRAWING OVERLAY HELPERS
# ==============================================================================
def draw_cinematic_card(frame, tag, title, subtitle, alpha=1.0):
    if alpha <= 0.01:
        return
    overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    
    # Bottom sleek cyber card
    card_w, card_h = 860, 105
    card_x = (WIDTH - card_w) // 2
    card_y = HEIGHT - 180

    a_bg = int(220 * alpha)
    d.rounded_rectangle([card_x, card_y, card_x + card_w, card_y + card_h], radius=10, fill=(6, 10, 24, a_bg), outline=(56, 189, 248, int(210 * alpha)), width=2)
    # Tag bar
    d.rectangle([card_x + 6, card_y + 12, card_x + 12, card_y + card_h - 12], fill=(251, 191, 36, int(255 * alpha)))
    
    d.text((card_x + 28, card_y + 12), f"[ {tag} ]", font=font_micro, fill=(251, 191, 36, int(255 * alpha)))
    d.text((card_x + 28, card_y + 36), title, font=font_body, fill=(255, 255, 255, int(255 * alpha)))
    d.text((card_x + 28, card_y + 70), subtitle, font=font_small, fill=(56, 189, 248, int(240 * alpha)))

    frame.alpha_composite(overlay)

def draw_letterbox(frame):
    d = ImageDraw.Draw(frame)
    bar_h = 48
    d.rectangle([0, 0, WIDTH, bar_h], fill=(0, 0, 0, 255))
    d.rectangle([0, HEIGHT - bar_h, WIDTH, HEIGHT], fill=(0, 0, 0, 255))
    d.line([(0, bar_h), (WIDTH, bar_h)], fill=(56, 189, 248, 60), width=1)
    d.line([(0, HEIGHT - bar_h), (WIDTH, HEIGHT - bar_h)], fill=(56, 189, 248, 60), width=1)

def draw_outro_card(t, dur):
    frame = Image.new("RGBA", (WIDTH, HEIGHT), (4, 8, 20, 255))
    fade_in = min(1.0, t / 0.8)
    fade_out = 1.0 if t < dur - 0.8 else max(0.0, (dur - t) / 0.8)
    alpha = fade_in * fade_out

    overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)

    # Title Lines
    d.line([(360, 250), (WIDTH - 360, 250)], fill=(56, 189, 248, int(200 * alpha)), width=3)
    d.line([(360, 400), (WIDTH - 360, 400)], fill=(251, 191, 36, int(200 * alpha)), width=3)

    # Title
    d.text((WIDTH // 2 - 140, 325), "SPACE", font=font_title_huge, fill=(56, 189, 248, int(255 * alpha)), anchor="mm")
    d.text((WIDTH // 2 + 150, 325), "WARS", font=font_title_huge, fill=(251, 191, 36, int(255 * alpha)), anchor="mm")

    # Subtitle
    d.text((WIDTH // 2, 445), "PROTOCOLO DE DEFENSA GALÁCTICA", font=font_subtitle, fill=(248, 250, 252, int(255 * alpha)), anchor="mm")

    # Feature List
    features = [
        "⚔ MODO 1 JUGADOR Y COOPERATIVO LOCAL 2P",
        "🛡 CAMPO DE FUERZA Y RALENTIZACIÓN DE BALAS TÁCTICA",
        "⚡ ARMAS DINÁMICAS (BLÁSTERS Y PLASMA CARGADO)",
        "👾 JEFE FINAL: 2 FASES, HIPER RAYO Y BARRIDO 180°",
        "🏆 TABLAS DE CLASIFICACIÓN SEPARADAS (SOLO / CO-OP)"
    ]
    for idx, feat in enumerate(features):
        fy = 530 + idx * 46
        d.text((WIDTH // 2, fy), feat, font=font_small, fill=(203, 213, 225, int(240 * alpha)), anchor="mm")

    # CTA Button
    cta_y = 810
    d.rounded_rectangle([WIDTH // 2 - 240, cta_y, WIDTH // 2 + 240, cta_y + 64], radius=12, fill=(14, 165, 233, int(255 * alpha)), outline=(255, 255, 255, int(220 * alpha)), width=2)
    d.text((WIDTH // 2, cta_y + 32), "¡PREPÁRATE PARA LA BATALLA!", font=font_body, fill=(255, 255, 255, int(255 * alpha)), anchor="mm")

    frame.alpha_composite(overlay)
    draw_letterbox(frame)
    return frame.convert("RGB")

# ==============================================================================
# MASTER RENDER PIPELINE
# ==============================================================================
def process_and_assemble_trailer():
    print("=== BUILDING OFFICIAL SPACE WARS GAMEPLAY TRAILER ===")
    
    # 1. Process each gameplay clip: extract frames, apply letterbox + overlay
    processed_clip_files = []
    
    for idx, s in enumerate(SCENES):
        clip_path = s["clip"]
        dur = s["dur"]
        out_processed = f"temp_clips/proc_{idx}.mp4"
        print(f"Processing scene {idx+1}/{len(SCENES)}: {s['title']} ({dur}s)...")

        # Open input clip stream
        pipe_in_cmd = [
            FFMPEG_EXE, "-y",
            "-i", clip_path,
            "-f", "image2pipe",
            "-pix_fmt", "rgb24",
            "-vcodec", "rawvideo",
            "-r", str(FPS),
            "-"
        ]
        proc_in = subprocess.Popen(pipe_in_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)

        # Output processed clip stream
        pipe_out_cmd = [
            FFMPEG_EXE, "-y",
            "-f", "rawvideo",
            "-vcodec", "rawvideo",
            "-s", f"{WIDTH}x{HEIGHT}",
            "-pix_fmt", "rgb24",
            "-r", str(FPS),
            "-i", "-", # Stdin from Python
            "-i", clip_path, # Audio from original clip
            "-map", "0:v:0",
            "-map", "1:a:0?",
            "-c:v", "libx264",
            "-preset", "fast",
            "-crf", "18",
            "-c:a", "aac",
            "-b:a", "192k",
            "-shortest",
            out_processed
        ]
        proc_out = subprocess.Popen(pipe_out_cmd, stdin=subprocess.PIPE)

        frame_bytes = WIDTH * HEIGHT * 3
        total_frames = int(dur * FPS)

        for f_i in range(total_frames):
            raw = proc_in.stdout.read(frame_bytes)
            if not raw or len(raw) < frame_bytes:
                break
            
            frame_img = Image.frombytes("RGB", (WIDTH, HEIGHT), raw).convert("RGBA")
            t_curr = f_i / float(FPS)

            # Overlay alpha curve: fade in over 0.6s, hold, fade out last 0.6s
            fade_in = min(1.0, t_curr / 0.6)
            fade_out = 1.0 if t_curr < dur - 0.6 else max(0.0, (dur - t_curr) / 0.6)
            alpha = fade_in * fade_out

            draw_cinematic_card(frame_img, s["tag"], s["title"], s["subtitle"], alpha)
            draw_letterbox(frame_img)

            # Dip to white flash on transition (first 3 frames)
            if f_i < 3 and idx > 0:
                flash_a = int(180 * (1.0 - f_i / 3.0))
                flash_lay = Image.new("RGBA", (WIDTH, HEIGHT), (255, 255, 255, flash_a))
                frame_img.alpha_composite(flash_lay)

            proc_out.stdin.write(frame_img.convert("RGB").tobytes())

        proc_in.stdout.close()
        proc_in.wait()
        proc_out.stdin.close()
        proc_out.wait()

        processed_clip_files.append(out_processed)
        print(f"  -> Scene {idx+1} processed successfully.")

    # 2. Render Outro Title Card Clip
    print("Rendering Outro Title Card Clip...")
    outro_file = "temp_clips/proc_outro.mp4"
    # Create silent or subtle hum audio for outro
    pipe_outro_cmd = [
        FFMPEG_EXE, "-y",
        "-f", "rawvideo",
        "-vcodec", "rawvideo",
        "-s", f"{WIDTH}x{HEIGHT}",
        "-pix_fmt", "rgb24",
        "-r", str(FPS),
        "-i", "-",
        "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
        "-c:v", "libx264",
        "-preset", "fast",
        "-crf", "18",
        "-c:a", "aac",
        "-b:a", "192k",
        "-t", str(OUTRO_DURATION),
        outro_file
    ]
    proc_outro = subprocess.Popen(pipe_outro_cmd, stdin=subprocess.PIPE)
    total_outro_frames = int(OUTRO_DURATION * FPS)
    for f_i in range(total_outro_frames):
        t = f_i / float(FPS)
        frame_rgb = draw_outro_card(t, OUTRO_DURATION)
        proc_outro.stdin.write(frame_rgb.tobytes())
    proc_outro.stdin.close()
    proc_outro.wait()
    processed_clip_files.append(outro_file)
    print("Outro clip ready.")

    # 3. Concatenate all processed clips using FFmpeg concat filter
    print("Concatenating all scenes into final master trailer...")
    filter_inputs = []
    filter_graph = ""
    for idx, p in enumerate(processed_clip_files):
        filter_inputs.extend(["-i", p])
        filter_graph += f"[{idx}:v:0][{idx}:a:0]"

    filter_graph += f"concat=n={len(processed_clip_files)}:v=1:a=1[outv][outa]"

    final_cmd = [
        FFMPEG_EXE, "-y",
        *filter_inputs,
        "-filter_complex", filter_graph,
        "-map", "[outv]",
        "-map", "[outa]",
        "-c:v", "libx264",
        "-preset", "medium",
        "-crf", "18",
        "-pix_fmt", "yuv420p",
        "-c:a", "aac",
        "-b:a", "192k",
        OUTPUT_PROJECT_PATH
    ]
    res = subprocess.run(final_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if res.returncode != 0:
        print("Concat error:", res.stderr.decode(errors="ignore"))
    else:
        print(f"Master trailer created at: {OUTPUT_PROJECT_PATH}")

    # Copy to Desktop
    try:
        import shutil
        shutil.copy2(OUTPUT_PROJECT_PATH, OUTPUT_DESKTOP_PATH)
        print(f"Copied to Desktop: {OUTPUT_DESKTOP_PATH}")
    except Exception as e:
        print("Desktop copy notice:", e)

    print("ALL DONE! TRAILER BASED 100% ON REAL GAMEPLAY IS COMPLETE!")

if __name__ == "__main__":
    process_and_assemble_trailer()

