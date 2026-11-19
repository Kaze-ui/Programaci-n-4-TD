import os
import sys
import math
import random
import subprocess
import wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import imageio_ffmpeg

# ==============================================================================
# CONFIGURATION & CONSTANTS
# ==============================================================================
WIDTH = 1920
HEIGHT = 1080
FPS = 30
DURATION = 37.0
TOTAL_FRAMES = int(FPS * DURATION)

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
font_small = get_font(FONT_BODY_PATH, 20)
font_micro = get_font(FONT_BODY_PATH, 16)

FFMPEG_EXE = imageio_ffmpeg.get_ffmpeg_exe()
OUTPUT_PROJECT_PATH = os.path.abspath("Trailer_Space_Wars.mp4")
DESKTOP_DIR = os.path.abspath(r"C:\Users\gamer\OneDrive\Escritorio")
OUTPUT_DESKTOP_PATH = os.path.join(DESKTOP_DIR, "Trailer_Space_Wars.mp4")

# ==============================================================================
# ASSET LOADING
# ==============================================================================
def load_img(path, size=None):
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert("RGBA")
    if size is not None:
        im = im.resize(size, Image.Resampling.LANCZOS)
    return im

print("Loading game assets...")
bg_space = load_img("Assets/Resources/Sprites/SpaceBackground.png", (WIDTH, HEIGHT))
bg_boss = load_img("Assets/Resources/Sprites/BossBackground.png", (WIDTH, HEIGHT))
if bg_space is None:
    bg_space = Image.new("RGBA", (WIDTH, HEIGHT), (5, 8, 20, 255))
if bg_boss is None:
    bg_boss = bg_space

art_bg1 = load_img(r"C:\Users\gamer\.gemini\antigravity\brain\626862a6-b0f5-4a63-a155-7677a0438a62\finalbossarenacataclysm_1794458030890.jpg", (WIDTH, HEIGHT))
art_bg2 = load_img(r"C:\Users\gamer\.gemini\antigravity\brain\626862a6-b0f5-4a63-a155-7677a0438a62\deep_space_shmup_bg_v2_1794451686123.jpg", (WIDTH, HEIGHT))

ship_p1 = load_img("Assets/Resources/Sprites/PlayerShip.png", (130, 130))
ship_p2 = load_img("Assets/Sprites/PlayerShipOutlined.png", (130, 130))
if ship_p2 is None:
    ship_p2 = ship_p1

alien_t1 = load_img("Assets/Resources/Sprites/AlienTier1.png", (95, 95))
alien_t2 = load_img("Assets/Resources/Sprites/AlienTier2.png", (125, 125))
alien_t3 = load_img("Assets/Resources/Sprites/AlienTier3.png", (150, 150))
alien_t4 = load_img("Assets/Resources/Sprites/AlienTier4.png", (130, 130))
boss_ship = load_img("Assets/Resources/Sprites/BossMothership.png", (560, 560))

bullet_player = load_img("Assets/Resources/Sprites/BulletPlayer.png", (24, 48))
bullet_alien = load_img("Assets/Resources/Sprites/BulletAlien.png", (28, 28))
exp_boss = load_img("Assets/Resources/Sprites/ExplosionBoss.png", (250, 250))
exp_t1 = load_img("Assets/Resources/Sprites/ExplosionTier1.png", (130, 130))
exp_t3 = load_img("Assets/Resources/Sprites/ExplosionTier3.png", (180, 180))

# Pre-render slow-down bullet with thick glowing red outline
def make_slow_bullet():
    base = bullet_alien.copy()
    w, h = base.size
    glow = Image.new("RGBA", (w + 40, h + 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(glow)
    center = ((w + 40) // 2, (h + 40) // 2)
    # Halo glow
    for r in range(24, 14, -2):
        alpha = int(90 * (1.0 - (r - 14) / 10.0))
        d.ellipse([center[0] - r, center[1] - r, center[0] + r, center[1] + r], outline=(255, 0, 8, alpha), width=3)
    # Intense sharp red outline ring (#FF0008)
    d.ellipse([center[0] - 15, center[1] - 15, center[0] + 15, center[1] + 15], outline=(255, 10, 20, 255), width=4)
    # Paste alien bullet at center
    glow.paste(base, (20, 20), base)
    return glow

slow_bullet_img = make_slow_bullet()

# Pre-render plasma charge ball
def make_plasma_orb():
    sz = 70
    orb = Image.new("RGBA", (sz, sz), (0, 0, 0, 0))
    d = ImageDraw.Draw(orb)
    c = sz // 2
    for r in range(c, 4, -2):
        pct = r / c
        a = int(220 * (1.0 - pct))
        d.ellipse([c - r, c - r, c + r, c + r], fill=(56, 189, 248, a))
    d.ellipse([c - 12, c - 12, c + 12, c + 12], fill=(255, 255, 255, 255))
    return orb

plasma_orb_img = make_plasma_orb()

# ==============================================================================
# DRAWING HELPERS
# ==============================================================================
def draw_thruster(frame, x, y, angle_deg, color=(56, 189, 248), size=24):
    rad = math.radians(angle_deg)
    back_x = x - math.sin(rad) * (size * 1.5)
    back_y = y + math.cos(rad) * (size * 1.5)
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    # Flame tip
    d.polygon([
        (x - 10, y + 5),
        (x + 10, y + 5),
        (back_x + random.uniform(-4, 4), back_y + random.uniform(8, 20))
    ], fill=(*color, 210))
    d.ellipse([x - 8, y - 2, x + 8, y + 14], fill=(255, 255, 255, 230))
    frame.alpha_composite(overlay)

def draw_laser_beam(frame, x1, y1, x2, y2, width, color_core=(255, 255, 255), color_glow=(56, 189, 248), alpha_mult=1.0):
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    # Outer glow
    glow_w = int(width * 2.8)
    a_glow = int(140 * alpha_mult)
    d.line([(x1, y1), (x2, y2)], fill=(*color_glow, a_glow), width=glow_w)
    # Mid beam
    mid_w = int(width * 1.5)
    a_mid = int(210 * alpha_mult)
    d.line([(x1, y1), (x2, y2)], fill=(*color_glow, a_mid), width=mid_w)
    # Core
    core_w = max(4, int(width * 0.6))
    a_core = int(255 * alpha_mult)
    d.line([(x1, y1), (x2, y2)], fill=(*color_core, a_core), width=core_w)
    frame.alpha_composite(overlay)

def draw_force_field(frame, x, y, radius, is_slowdown=False, is_shielded=True):
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    col = (56, 189, 248) if is_shielded else (160, 170, 190)
    # Outer pulsating aura
    d.ellipse([x - radius, y - radius, x + radius, y + radius], outline=(*col, 180), width=4)
    d.ellipse([x - radius + 4, y - radius + 4, x + radius - 4, y + radius - 4], outline=(*col, 90), width=3)
    d.ellipse([x - radius, y - radius, x + radius, y + radius], fill=(*col, 28))
    # Inner energy ripple
    r_in = radius * 0.65
    d.ellipse([x - r_in, y - r_in, x + r_in, y + r_in], outline=(*col, 120), width=2)
    frame.alpha_composite(overlay)

def draw_toast(frame, text, subtext, x, y, alpha=1.0):
    if alpha <= 0.01:
        return
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    w, h = 380, 74
    a_bg = int(220 * alpha)
    d.rounded_rectangle([x, y, x + w, y + h], radius=8, fill=(10, 16, 30, a_bg), outline=(56, 189, 248, int(230 * alpha)), width=2)
    # Accent indicator bar
    d.rectangle([x + 4, y + 8, x + 10, y + h - 8], fill=(251, 191, 36, int(255 * alpha)))
    # Texts
    d.text((x + 24, y + 12), text, font=font_body, fill=(255, 255, 255, int(255 * alpha)))
    d.text((x + 24, y + 42), subtext, font=font_small, fill=(56, 189, 248, int(230 * alpha)))
    frame.alpha_composite(overlay)

def draw_tactical_alert_modal(frame, t_phase, alpha=1.0):
    if alpha <= 0.01:
        return
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    # Translucent black backdrop
    d.rectangle([0, 0, WIDTH, HEIGHT], fill=(0, 0, 0, int(185 * alpha)))
    # Central cyber warning panel
    pw, ph = 1200, 240
    px = (WIDTH - pw) // 2
    py = (HEIGHT - ph) // 2
    d.rounded_rectangle([px, py, px + pw, py + ph], radius=16, fill=(12, 18, 32, int(245 * alpha)), outline=(239, 68, 68, int(255 * alpha)), width=4)
    # Flashing Yellow and Red header
    flash = int(t_phase * 6) % 2 == 0
    hdr_color = (251, 191, 36) if flash else (239, 68, 68)
    d.text((WIDTH // 2, py + 45), "[ ⚠ ALERTA DE COMBATE // SECTOR CLASIFICADO ⚠ ]", font=font_small, fill=hdr_color, anchor="mm")
    d.text((WIDTH // 2, py + 115), "NUEVOS ENEMIGOS DETECTADOS", font=font_title_large, fill=hdr_color, anchor="mm")
    d.text((WIDTH // 2, py + 185), "AMENAZA TITÁNICA: NAVE NODRIZA LEVIATÁN // FASE 2", font=font_subtitle, fill=(248, 250, 252), anchor="mm")
    # Scanning bracket accents
    d.rectangle([px + 20, py + 20, px + 80, py + 24], fill=hdr_color)
    d.rectangle([px + pw - 80, py + 20, px + pw - 20, py + 24], fill=hdr_color)
    d.rectangle([px + 20, py + ph - 24, px + 80, py + ph - 20], fill=hdr_color)
    d.rectangle([px + pw - 80, py + ph - 24, px + pw - 20, py + ph - 20], fill=hdr_color)
    frame.alpha_composite(overlay)

def draw_boss_hp_bar(frame, hp_pct, is_phase_2=False, is_invulnerable=False):
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    bw, bh = 760, 28
    bx = (WIDTH - bw) // 2
    by = 48
    # Background card
    d.rounded_rectangle([bx - 20, by - 32, bx + bw + 20, by + bh + 14], radius=10, fill=(8, 12, 24, 235), outline=(56, 189, 248, 160), width=2)
    # Label
    d.text((bx, by - 26), "LEVIATÁN // NAVE NODRIZA ENEMIGA", font=font_small, fill=(248, 250, 252))
    state_txt = "FASE 2 // ESCUDO ACTIVO (-30% DAÑO)" if is_phase_2 else "FASE 1 // TITÁN ACTIVO"
    if is_invulnerable:
        state_txt = "INVULNERABLE // MANIOBRA SUPERIOR"
    d.text((bx + bw, by - 26), state_txt, font=font_micro, fill=(251, 191, 36) if is_phase_2 else (148, 163, 184), anchor="ra")
    # Health bar background
    d.rounded_rectangle([bx, by, bx + bw, by + bh], radius=6, fill=(25, 30, 45, 255))
    # Fill
    fill_w = max(0, int(bw * max(0.0, min(1.0, hp_pct))))
    if is_invulnerable:
        fill_col = (140, 145, 155)
    elif is_phase_2:
        fill_col = (56, 189, 248) # Tactical Blue as specified!
    else:
        fill_col = (239, 68, 68) # Red
    if fill_w > 0:
        d.rounded_rectangle([bx, by, bx + fill_w, by + bh], radius=6, fill=fill_col)
        # Gloss highlight
        d.rectangle([bx, by, bx + fill_w, by + 8], fill=(255, 255, 255, 80))
    # Outer shield bracket if phase 2
    if is_phase_2:
        d.rounded_rectangle([bx - 6, by - 6, bx + bw + 6, by + bh + 6], radius=8, outline=(56, 189, 248, 220), width=2)
    frame.alpha_composite(overlay)

def draw_hud_corner(frame, p1_hp=3, p2_hp=3, score=14850):
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    # Bottom Left: Player 1 stats
    d.rounded_rectangle([30, HEIGHT - 110, 320, HEIGHT - 30], radius=8, fill=(10, 15, 28, 210), outline=(56, 189, 248, 150), width=2)
    d.text((45, HEIGHT - 100), "JUGADOR 1 // INTEGRIDAD", font=font_micro, fill=(56, 189, 248))
    for i in range(4):
        col = (56, 189, 248) if i < p1_hp else (40, 50, 70)
        d.rounded_rectangle([45 + i * 62, HEIGHT - 76, 45 + i * 62 + 52, HEIGHT - 46], radius=4, fill=col)
    # Bottom Right: Player 2 stats
    d.rounded_rectangle([WIDTH - 350, HEIGHT - 110, WIDTH - 60, HEIGHT - 30], radius=8, fill=(10, 15, 28, 210), outline=(251, 191, 36, 150), width=2)
    d.text((WIDTH - 335, HEIGHT - 100), "JUGADOR 2 // CO-OP LOCAL", font=font_micro, fill=(251, 191, 36))
    for i in range(4):
        col = (251, 191, 36) if i < p2_hp else (40, 50, 70)
        d.rounded_rectangle([WIDTH - 335 + i * 62, HEIGHT - 76, WIDTH - 335 + i * 62 + 52, HEIGHT - 46], radius=4, fill=col)
    frame.alpha_composite(overlay)

def draw_cinematic_card(frame, title, subtitle, alpha=1.0, is_climax=False):
    if alpha <= 0.01:
        return
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    cy = 540 if is_climax else 860
    # Backdrop stripe
    sh_h = 170 if is_climax else 120
    d.rectangle([0, cy - sh_h // 2, WIDTH, cy + sh_h // 2], fill=(6, 10, 22, int(220 * alpha)))
    d.line([(0, cy - sh_h // 2), (WIDTH, cy - sh_h // 2)], fill=(56, 189, 248, int(230 * alpha)), width=2)
    d.line([(0, cy + sh_h // 2), (WIDTH, cy + sh_h // 2)], fill=(56, 189, 248, int(230 * alpha)), width=2)
    # Text
    f_t = font_title_huge if is_climax else font_title_large
    d.text((WIDTH // 2 + 2, cy - 24 + 2), title, font=f_t, fill=(0, 0, 0, int(255 * alpha)), anchor="mm")
    d.text((WIDTH // 2, cy - 24), title, font=f_t, fill=(255, 255, 255, int(255 * alpha)), anchor="mm")
    if subtitle:
        d.text((WIDTH // 2, cy + 36), subtitle, font=font_subtitle, fill=(56, 189, 248, int(255 * alpha)), anchor="mm")
    frame.alpha_composite(overlay)

# ==============================================================================
# AUDIO SYNTHESIS & SOUND DESIGN
# ==============================================================================
def build_audio_track():
    print("Mixing high-impact cinematic soundtrack and sound effects...")
    sr = 44100
    total_samples = int(sr * DURATION)
    audio_master = np.zeros((total_samples, 2), dtype=np.float32)

    # 1. Convert and load music_game.ogg
    temp_wav = "temp_music_track.wav"
    subprocess.run([
        FFMPEG_EXE, "-y", "-i", "Assets/Resources/Audio/music_game.ogg",
        "-ar", str(sr), "-ac", "2", temp_wav
    ], stdout=subprocess.PIPE, stderr=subprocess.PIPE)

    if os.path.exists(temp_wav):
        with wave.open(temp_wav, "rb") as w:
            n = min(total_samples, w.getnframes())
            data = np.frombuffer(w.readframes(n), dtype=np.int16).reshape(-1, 2)
            music_norm = data.astype(np.float32) / 32768.0
            audio_master[:n] += music_norm * 0.72
        try:
            os.remove(temp_wav)
        except:
            pass

    # Music volume automation:
    # Fade in 0 - 2.5s
    fade_in_len = int(sr * 2.5)
    fade_curve = np.linspace(0.1, 1.0, fade_in_len)
    audio_master[:fade_in_len, 0] *= fade_curve
    audio_master[:fade_in_len, 1] *= fade_curve

    # Duck music during bullet time (12.0s - 15.5s)
    bt_start = int(sr * 12.0)
    bt_end = int(sr * 15.5)
    audio_master[bt_start:bt_end] *= 0.45

    # Crescendo at Boss Death (31.5s - 34.0s)
    # Fade out at end (35.5s - 37.0s)
    fo_start = int(sr * 35.2)
    fo_len = total_samples - fo_start
    audio_master[fo_start:, 0] *= np.linspace(1.0, 0.0, fo_len)
    audio_master[fo_start:, 1] *= np.linspace(1.0, 0.0, fo_len)

    # 2. Helper to load and overlay SFX
    def overlay_sfx(path, start_time_sec, volume=1.0, pan=0.0):
        if not os.path.exists(path):
            return
        try:
            with wave.open(path, "rb") as w:
                n_ch = w.getnchannels()
                frames = w.readframes(w.getnframes())
                data = np.frombuffer(frames, dtype=np.int16)
                if n_ch == 1:
                    data = np.column_stack([data, data])
                else:
                    data = data.reshape(-1, n_ch)
                sfx_norm = (data.astype(np.float32) / 32768.0) * volume
                start_samp = int(start_time_sec * sr)
                end_samp = min(total_samples, start_samp + len(sfx_norm))
                copy_len = end_samp - start_samp
                if copy_len > 0:
                    left_gain = max(0.0, 1.0 - pan)
                    right_gain = max(0.0, 1.0 + pan)
                    audio_master[start_samp:end_samp, 0] += sfx_norm[:copy_len, 0] * left_gain
                    audio_master[start_samp:end_samp, 1] += sfx_norm[:copy_len, 1] * right_gain
        except Exception as e:
            pass

    # Scene 1: Prologue
    overlay_sfx("Assets/Resources/Audio/charge_energy.wav", 0.6, volume=0.85)

    # Scene 2: Dogfight
    for t in [4.8, 5.2, 5.6, 6.0, 6.4, 7.0, 7.4, 8.0, 8.4, 9.6, 10.0]:
        overlay_sfx("Assets/Resources/Audio/shoot_normal.wav", t, volume=0.45, pan=random.uniform(-0.5, 0.5))
    overlay_sfx("Assets/Resources/Audio/shoot_big.wav", 8.8, volume=0.8, pan=-0.2)
    overlay_sfx("Assets/Resources/Audio/shoot_big.wav", 10.4, volume=0.8, pan=0.2)
    for t in [5.8, 7.2, 8.5, 9.2, 10.8]:
        overlay_sfx("Assets/Resources/Audio/enemy_explosion.wav", t, volume=0.75, pan=random.uniform(-0.4, 0.4))

    # Scene 3: Force Field & Slow-Motion Bullet Time
    overlay_sfx("Assets/Resources/Audio/charge_energy.wav", 11.8, volume=0.9)
    overlay_sfx("Assets/Resources/Audio/shoot_big.wav", 15.2, volume=0.85)
    overlay_sfx("Assets/Resources/Audio/enemy_explosion.wav", 15.6, volume=0.9)

    # Scene 4: Boss Warning & Emergence
    overlay_sfx("Assets/Resources/Audio/charge_energy.wav", 18.0, volume=1.0)
    overlay_sfx("Assets/Resources/Audio/tier4_laser.wav", 20.8, volume=0.75, pan=-0.6)
    overlay_sfx("Assets/Resources/Audio/tier4_laser.wav", 21.4, volume=0.75, pan=0.6)

    # Scene 5: Boss Hyper Beam & 180 Sweep
    overlay_sfx("Assets/Resources/Audio/boss_laser.wav", 24.2, volume=0.9)
    overlay_sfx("Assets/Resources/Audio/boss_laser.wav", 27.0, volume=1.1)
    overlay_sfx("Assets/Resources/Audio/boss_laser.wav", 29.5, volume=1.0)
    for t in [25.5, 26.2, 28.0, 28.8, 30.2]:
        overlay_sfx("Assets/Resources/Audio/shoot_big.wav", t, volume=0.6)
    overlay_sfx("Assets/Resources/Audio/boss_death.wav", 31.6, volume=1.3)

    # Soft Limiter & Normalization
    max_val = np.max(np.abs(audio_master))
    if max_val > 0.98:
        audio_master = audio_master * (0.98 / max_val)

    audio_master_i16 = (audio_master * 32767.0).astype(np.int16)
    out_audio_path = "trailer_final_audio.wav"
    with wave.open(out_audio_path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(audio_master_i16.tobytes())
    return out_audio_path

# ==============================================================================
# MAIN CINEMATIC FRAME GENERATOR
# ==============================================================================
def render_trailer_video(audio_path):
    print("Starting video frame generation & direct FFmpeg encoding...")
    pipe_cmd = [
        FFMPEG_EXE, "-y",
        "-f", "rawvideo",
        "-vcodec", "rawvideo",
        "-s", f"{WIDTH}x{HEIGHT}",
        "-pix_fmt", "rgb24",
        "-r", str(FPS),
        "-i", "-", # Stdin from Python
        "-i", audio_path,
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-preset", "fast",
        "-crf", "18",
        "-c:a", "aac",
        "-b:a", "192k",
        "-shortest",
        OUTPUT_PROJECT_PATH
    ]

    proc = subprocess.Popen(pipe_cmd, stdin=subprocess.PIPE)

    stars = [(random.randint(0, WIDTH), random.randint(0, HEIGHT), random.randint(1, 3), random.uniform(0.3, 1.0)) for _ in range(160)]

    for f_idx in range(TOTAL_FRAMES):
        t = f_idx / float(FPS)
        
        # Base background selection and vertical drift
        scroll_y = int((t * 90) % HEIGHT)
        frame = Image.new("RGBA", (WIDTH, HEIGHT), (4, 7, 18, 255))
        
        if t < 11.5:
            base_bg = art_bg1 if art_bg1 else bg_space
        elif t < 22.0:
            base_bg = art_bg2 if art_bg2 else bg_space
        else:
            base_bg = bg_boss

        # Tile scroll background
        frame.paste(base_bg, (0, scroll_y - HEIGHT))
        frame.paste(base_bg, (0, scroll_y))

        # Starfield layer with motion blur
        d_star = ImageDraw.Draw(frame)
        for sx, sy, s_rad, s_spd in stars:
            cur_sy = int((sy + t * s_spd * 140) % HEIGHT)
            d_star.ellipse([sx, cur_sy, sx + s_rad, cur_sy + s_rad + 2], fill=(220, 235, 255, int(180 * s_spd)))

        # ----------------------------------------------------------------------
        # ACT 1: PROLOGUE (0.0s - 4.5s)
        # ----------------------------------------------------------------------
        if t < 4.5:
            # Cinematic slow space pan
            draw_cinematic_card(
                frame,
                "CUANDO LA GALAXIA CAE EN SILENCIO...",
                "SECTOR 7 // PROTOCOLO DE DEFENSA ACTIVO",
                alpha=min(1.0, max(0.0, math.sin((t / 4.5) * math.pi)))
            )
            if t > 3.2:
                # Ships hyperspace warp in
                warp_prog = (t - 3.2) / 1.3
                p1_x = 760
                p1_y = int(HEIGHT + 150 - warp_prog * 380)
                p2_x = 1160
                p2_y = int(HEIGHT + 150 - warp_prog * 380)
                draw_thruster(frame, p1_x + 65, p1_y + 110, 0, (56, 189, 248), 35)
                draw_thruster(frame, p2_x + 65, p2_y + 110, 0, (251, 191, 36), 35)
                frame.paste(ship_p1, (p1_x, p1_y), ship_p1)
                frame.paste(ship_p2, (p2_x, p2_y), ship_p2)

        # ----------------------------------------------------------------------
        # ACT 2: DOGFIGHT & CO-OP (4.5s - 11.5s)
        # ----------------------------------------------------------------------
        elif t < 11.5:
            sub_t = t - 4.5
            p1_x = int(680 + math.sin(sub_t * 2.8) * 140)
            p1_y = 780
            p2_x = int(1120 - math.sin(sub_t * 2.5) * 160)
            p2_y = 800

            # Engines
            draw_thruster(frame, p1_x + 65, p1_y + 110, 0, (56, 189, 248), 24)
            draw_thruster(frame, p2_x + 65, p2_y + 110, 0, (251, 191, 36), 24)

            # Player bullets firing
            for b_idx in range(6):
                by = int((p1_y - 20 - ((sub_t * 1200 + b_idx * 160) % 700)))
                frame.paste(bullet_player, (p1_x + 40, by), bullet_player)
                frame.paste(bullet_player, (p1_x + 70, by), bullet_player)
                frame.paste(bullet_player, (p2_x + 40, by), bullet_player)
                frame.paste(bullet_player, (p2_x + 70, by), bullet_player)

            # Enemies attacking in formation
            for i in range(4):
                ex = int(380 + i * 320 + math.cos(sub_t * 3.0 + i) * 80)
                ey = int(120 + ((sub_t * 220 + i * 90) % 450))
                frame.paste(alien_t1, (ex, ey), alien_t1)
                # Alien bullets
                if f_idx % 8 == 0:
                    frame.paste(bullet_alien, (ex + 35, ey + 80), bullet_alien)

            # Heavy Tier 3 Cruiser in middle
            t3_x, t3_y = 880, int(160 + math.sin(sub_t * 1.5) * 40)
            frame.paste(alien_t3, (t3_x, t3_y), alien_t3)

            # Explosions on hit
            if 1.2 < sub_t < 2.2:
                frame.paste(exp_t1, (520, 240), exp_t1)
            if 3.8 < sub_t < 4.8:
                frame.paste(exp_t1, (1260, 310), exp_t1)
            if 5.2 < sub_t < 6.5:
                frame.paste(exp_t3, (t3_x - 15, t3_y - 15), exp_t3)

            # Draw Ships
            frame.paste(ship_p1, (p1_x, p1_y), ship_p1)
            frame.paste(ship_p2, (p2_x, p2_y), ship_p2)

            # Weapon Switch Toast Popup showcase
            toast_alpha = 1.0 if 2.0 <= sub_t <= 5.5 else 0.0
            draw_toast(frame, "BLÁSTERS // MODO ASALTO", "CADENCIA MÁXIMA ACTIVADA", 40, 680, toast_alpha)

            draw_cinematic_card(
                frame,
                "MODO 1 JUGADOR & CO-OP 2P LOCAL",
                "ARSENAL TÁCTICO // BLÁSTERS & PLASMA CARGADO",
                alpha=min(1.0, max(0.0, math.sin((sub_t / 7.0) * math.pi)))
            )
            draw_hud_corner(frame, 4, 3, score=int(sub_t * 4000))

        # ----------------------------------------------------------------------
        # ACT 3: BULLET SLOWDOWN & FORCE FIELD (11.5s - 18.0s)
        # ----------------------------------------------------------------------
        elif t < 18.0:
            sub_t = t - 11.5
            p1_x, p1_y = 960 - 65, 680

            # Matrix Slowdown: bullets move slowly, thick glowing RED OUTLINE active!
            field_radius = 210
            draw_force_field(frame, p1_x + 65, p1_y + 65, field_radius, is_slowdown=True, is_shielded=(sub_t < 4.0))

            # Swarm of slowed bullets surrounding the player with intense RED OUTLINE
            num_bullets = 22
            for b_i in range(num_bullets):
                ang = (b_i / float(num_bullets)) * 2 * math.pi + sub_t * 0.4
                dist = 110 + math.sin(sub_t * 2.0 + b_i) * 35
                bx = int(p1_x + 65 + math.cos(ang) * dist - 25)
                by = int(p1_y + 65 + math.sin(ang) * dist - 25)
                # Slow bullet with red glowing ring
                frame.paste(slow_bullet_img, (bx, by), slow_bullet_img)

            # Player weaves through and fires charged plasma
            frame.paste(ship_p1, (p1_x, p1_y), ship_p1)
            draw_thruster(frame, p1_x + 65, p1_y + 110, 0, (56, 189, 248), 18)

            if sub_t > 3.5:
                # Plasma blast firing upward
                py_orb = int(p1_y - (sub_t - 3.5) * 750)
                frame.paste(plasma_orb_img, (p1_x + 35, py_orb), plasma_orb_img)
                if py_orb < 350:
                    frame.paste(exp_t3, (p1_x - 20, 200), exp_t3)

            draw_cinematic_card(
                frame,
                "CAMPO DE FUERZA TÁCTICO",
                "RALENTIZA EL FUEGO ENEMIGO EN TIEMPO REAL",
                alpha=min(1.0, max(0.0, math.sin((sub_t / 6.5) * math.pi)))
            )

        # ----------------------------------------------------------------------
        # ACT 4: BOSS EMERGENCE & PHASE 2 ALERT (18.0s - 25.5s)
        # ----------------------------------------------------------------------
        elif t < 25.5:
            sub_t = t - 18.0
            
            # Boss descending
            boss_y = int(-180 + min(360, sub_t * 90))
            boss_x = (WIDTH - 560) // 2

            # Shake camera if entering
            shake_x = random.randint(-4, 4) if sub_t < 3.0 else 0
            shake_y = random.randint(-4, 4) if sub_t < 3.0 else 0
            
            # Tier 4 escort snipers charging lasers
            frame.paste(alien_t4, (280 + shake_x, 260 + shake_y), alien_t4)
            frame.paste(alien_t4, (WIDTH - 410 + shake_x, 260 + shake_y), alien_t4)

            if sub_t > 2.5:
                draw_laser_beam(frame, 345, 340, 520, HEIGHT, 8, (255, 255, 255), (56, 189, 248), 0.9)
                draw_laser_beam(frame, WIDTH - 345, 340, WIDTH - 520, HEIGHT, 8, (255, 255, 255), (56, 189, 248), 0.9)

            frame.paste(boss_ship, (boss_x + shake_x, boss_y + shake_y), boss_ship)

            # Boss HP Bar: Transitioning from Phase 1 to Phase 2
            is_p2 = sub_t > 3.0
            hp_val = 0.55 if not is_p2 else 0.65 # Healed 20%!
            draw_boss_hp_bar(frame, hp_val, is_phase_2=is_p2, is_invulnerable=(sub_t < 1.0))

            # Tactical Alert Pop-up Modal (Intercalating yellow and red, 3 seconds)
            if sub_t < 3.2:
                modal_alpha = min(1.0, max(0.0, math.sin((sub_t / 3.2) * math.pi)))
                draw_tactical_alert_modal(frame, sub_t, modal_alpha)

            # Player ships evading below
            frame.paste(ship_p1, (640, 840), ship_p1)
            frame.paste(ship_p2, (1200, 840), ship_p2)
            draw_hud_corner(frame, 3, 3, score=28400)

        # ----------------------------------------------------------------------
        # ACT 5: HYPER BEAM & 180° SWEEPING DEATH RAY (25.5s - 32.5s)
        # ----------------------------------------------------------------------
        elif t < 32.5:
            sub_t = t - 25.5
            boss_x = (WIDTH - 560) // 2
            boss_y = 60

            # Boss Quintuple Cannon pattern & Diagonals
            if sub_t < 2.0:
                # 5 Spread Bullets
                angles = [-36, -18, 0, 18, 36]
                for a in angles:
                    rad = math.radians(a + 90)
                    bx = int(WIDTH // 2 + math.cos(rad) * (sub_t * 600) - 14)
                    by = int(boss_y + 340 + math.sin(rad) * (sub_t * 600) - 14)
                    frame.paste(bullet_alien, (bx, by), bullet_alien)

            # Telegraph -> HYPER BEAM (2.0s - 4.5s)
            if 1.0 <= sub_t < 2.5:
                # 3s Red Telegraph line down center
                flash_a = 0.6 + 0.4 * math.sin(sub_t * 16.0)
                draw_laser_beam(frame, WIDTH // 2, boss_y + 340, WIDTH // 2, HEIGHT, 12, (255, 180, 180), (239, 68, 68), flash_a)
            elif 2.5 <= sub_t < 4.2:
                # THE HYPER BEAM: Colossal 280px wide beam obliterating center screen!
                beam_w = 260
                draw_laser_beam(frame, WIDTH // 2, boss_y + 340, WIDTH // 2, HEIGHT, beam_w, (255, 255, 255), (239, 68, 68), 1.0)
                # Outer plasma rings
                d_bm = ImageDraw.Draw(frame)
                d_bm.rectangle([WIDTH // 2 - 180, 0, WIDTH // 2 + 180, HEIGHT], fill=(255, 50, 50, 45))

            # 180° SWEEPING DEATH RAY (4.2s - 6.0s)
            if 4.2 <= sub_t < 6.2:
                sweep_prog = (sub_t - 4.2) / 2.0 # 0.0 to 1.0
                # Sweep from right (0 deg) down to left (180 deg)
                sweep_angle = math.radians(0 + sweep_prog * 180)
                target_x = WIDTH // 2 + math.cos(sweep_angle) * 1200
                target_y = boss_y + 340 + math.sin(sweep_angle) * 900
                draw_laser_beam(frame, WIDTH // 2, boss_y + 340, target_x, target_y, 40, (255, 255, 255), (56, 189, 248), 1.0)

            frame.paste(boss_ship, (boss_x, boss_y), boss_ship)

            # Boss Final Destruction Detonation (6.0s - 7.0s)
            if sub_t >= 6.0:
                exp_t = sub_t - 6.0
                draw_boss_hp_bar(frame, 0.0, is_phase_2=True)
                # Cascading hull explosions
                frame.paste(exp_boss, (boss_x + 60, boss_y + 120), exp_boss)
                frame.paste(exp_boss, (boss_x + 280, boss_y + 80), exp_boss)
                frame.paste(exp_boss, (boss_x + 180, boss_y + 240), exp_boss)
                # Supernova shockwave
                sw_r = int(exp_t * 1100)
                if sw_r > 5:
                    d_sw = ImageDraw.Draw(frame)
                    d_sw.ellipse([WIDTH // 2 - sw_r, boss_y + 260 - sw_r, WIDTH // 2 + sw_r, boss_y + 260 + sw_r], outline=(255, 255, 255, 255), width=max(1, min(18, sw_r)))
                    if sw_r > 30:
                        d_sw.ellipse([WIDTH // 2 - sw_r + 18, boss_y + 260 - sw_r + 18, WIDTH // 2 + sw_r - 18, boss_y + 260 + sw_r - 18], outline=(56, 189, 248, 180), width=max(1, min(10, sw_r - 20)))
            else:
                draw_boss_hp_bar(frame, max(0.05, 0.65 - (sub_t / 6.0) * 0.65), is_phase_2=True)

            # Players dodging at edges
            frame.paste(ship_p1, (240, 840), ship_p1)
            frame.paste(ship_p2, (WIDTH - 360, 840), ship_p2)

        # ----------------------------------------------------------------------
        # ACT 6: TITLE REVEAL & CALL TO ACTION (32.5s - 37.0s)
        # ----------------------------------------------------------------------
        else:
            sub_t = t - 32.5
            fade_in = min(1.0, sub_t / 1.0)
            
            overlay = Image.new("RGBA", frame.size, (0, 0, 0, 0))
            d = ImageDraw.Draw(overlay)

            # Dark sleek cinematic backing
            d.rectangle([0, 0, WIDTH, HEIGHT], fill=(4, 8, 18, int(235 * fade_in)))
            
            # Logo Accent Lines
            d.line([(320, 260), (WIDTH - 320, 260)], fill=(56, 189, 248, int(200 * fade_in)), width=3)
            d.line([(320, 410), (WIDTH - 320, 410)], fill=(251, 191, 36, int(200 * fade_in)), width=3)

            # Title
            d.text((WIDTH // 2 - 140, 335), "SPACE", font=font_title_huge, fill=(56, 189, 248, int(255 * fade_in)), anchor="mm")
            d.text((WIDTH // 2 + 150, 335), "WARS", font=font_title_huge, fill=(251, 191, 36, int(255 * fade_in)), anchor="mm")
            
            # Subtitle
            d.text((WIDTH // 2, 455), "PROTOCOLO DE DEFENSA GALÁCTICA", font=font_subtitle, fill=(248, 250, 252, int(255 * fade_in)), anchor="mm")

            # Feature Badges
            features = [
                "⚔ MODO 1 JUGADOR & CO-OP LOCAL 2P",
                "🛡 CAMPO DE FUERZA & RALENTIZACIÓN TÁCTICA",
                "⚡ SISTEMA DE MEJORAS & ARMAS DINÁMICAS",
                "🏆 TABLAS DE CLASIFICACIÓN SEPARADAS (SOLO / CO-OP)",
                "👾 JEFE FINAL: 2 FASES, HIPER RAYO Y BARRIDO 180°"
            ]
            for idx, feat in enumerate(features):
                fy = 540 + idx * 46
                d.text((WIDTH // 2, fy), feat, font=font_small, fill=(203, 213, 225, int(240 * fade_in)), anchor="mm")

            # Call to Action Box
            cta_y = 820
            d.rounded_rectangle([WIDTH // 2 - 240, cta_y, WIDTH // 2 + 240, cta_y + 64], radius=12, fill=(14, 165, 233, int(255 * fade_in)), outline=(255, 255, 255, int(220 * fade_in)), width=2)
            d.text((WIDTH // 2, cta_y + 32), "¡PREPÁRATE PARA LA BATALLA!", font=font_body, fill=(255, 255, 255, int(255 * fade_in)), anchor="mm")

            frame.alpha_composite(overlay)

        # Subtle Cinema Letterbox (Cinematic Aspect Ratio)
        d_cin = ImageDraw.Draw(frame)
        bar_h = 54
        d_cin.rectangle([0, 0, WIDTH, bar_h], fill=(0, 0, 0, 255))
        d_cin.rectangle([0, HEIGHT - bar_h, WIDTH, HEIGHT], fill=(0, 0, 0, 255))
        d_cin.line([(0, bar_h), (WIDTH, bar_h)], fill=(56, 189, 248, 70), width=1)
        d_cin.line([(0, HEIGHT - bar_h), (WIDTH, HEIGHT - bar_h)], fill=(56, 189, 248, 70), width=1)

        # Convert to RGB and pipe into ffmpeg
        rgb_bytes = frame.convert("RGB").tobytes()
        proc.stdin.write(rgb_bytes)

        if f_idx % 150 == 0 or f_idx == TOTAL_FRAMES - 1:
            print(f"Rendered {f_idx}/{TOTAL_FRAMES} frames ({(f_idx / TOTAL_FRAMES) * 100:.1f}%)...")

    proc.stdin.close()
    proc.wait()
    print("FFmpeg encoding complete!")

# ==============================================================================
# MAIN ENTRY POINT
# ==============================================================================
if __name__ == "__main__":
    audio_file = build_audio_track()
    render_trailer_video(audio_file)

    # Clean up audio intermediate
    try:
        if os.path.exists(audio_file):
            os.remove(audio_file)
    except:
        pass

    # Copy to Desktop if available
    try:
        if os.path.exists(OUTPUT_PROJECT_PATH) and os.path.exists(DESKTOP_DIR):
            import shutil
            shutil.copy2(OUTPUT_PROJECT_PATH, OUTPUT_DESKTOP_PATH)
            print(f"Copied trailer to Desktop: {OUTPUT_DESKTOP_PATH}")
    except Exception as e:
        print(f"Desktop copy notice: {e}")

    print("ALL DONE! TRAILER GENERATED SUCCESSFULLY!")

