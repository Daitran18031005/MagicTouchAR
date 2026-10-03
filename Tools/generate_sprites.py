import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)

def create_balloon_sprite(base_color=(255, 255, 255), shadow_color=(180, 180, 190), outline_color=(25, 25, 35)):
    """
    Creates a 256x256 glossy arcade-style balloon sprite with specular highlight and tie knot.
    """
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Balloon parameters
    cx, cy = 128, 110
    rx, ry = 72, 82

    # 1. String hanging down from knot
    draw.line([(cx, cy + ry + 12), (cx, 245)], fill=(60, 60, 70, 220), width=3)

    # 2. Balloon knot at bottom
    knot_points = [
        (cx - 10, cy + ry - 2),
        (cx + 10, cy + ry - 2),
        (cx + 16, cy + ry + 14),
        (cx - 16, cy + ry + 14)
    ]
    draw.polygon(knot_points, fill=shadow_color, outline=outline_color)
    draw.line([(cx - 16, cy + ry + 14), (cx + 16, cy + ry + 14)], fill=outline_color, width=3)

    # 3. Outer dark outline for balloon body
    draw.ellipse([cx - rx - 4, cy - ry - 4, cx + rx + 4, cy + ry + 4], fill=outline_color)

    # 4. Shadow base
    draw.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=shadow_color)

    # 5. Balloon body (spherical gradient fill)
    for i in range(16):
        ratio = i / 16.0
        cur_rx = int(rx * (1.0 - ratio * 0.4))
        cur_ry = int(ry * (1.0 - ratio * 0.4))
        offset_x = int(-18 * ratio)
        offset_y = int(-20 * ratio)

        r = int(shadow_color[0] + (base_color[0] - shadow_color[0]) * ratio)
        g = int(shadow_color[1] + (base_color[1] - shadow_color[1]) * ratio)
        b = int(shadow_color[2] + (base_color[2] - shadow_color[2]) * ratio)

        box = [
            (cx + offset_x) - cur_rx,
            (cy + offset_y) - cur_ry,
            (cx + offset_x) + cur_rx,
            (cy + offset_y) + cur_ry
        ]
        draw.ellipse(box, fill=(r, g, b, 255))

    # 6. Specular curved gloss highlight (top-left)
    highlight_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    h_draw = ImageDraw.Draw(highlight_img)
    # Primary curved sheen
    h_draw.ellipse([cx - 52, cy - 65, cx - 18, cy - 25], fill=(255, 255, 255, 235))
    # Small secondary dot sheen
    h_draw.ellipse([cx - 48, cy - 15, cx - 36, cy - 3], fill=(255, 255, 255, 180))
    # Soften highlight edges slightly
    highlight_img = highlight_img.filter(ImageFilter.GaussianBlur(1.0))
    img.alpha_composite(highlight_img)

    return img

def create_symbol_sprite(symbol_char, style="bold"):
    """
    Creates a 256x256 transparent sprite with a bold white symbol, crisp dark border, and subtle drop shadow.
    """
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    cx, cy = 128, 110 # Aligned with balloon center
    stroke_w = 18
    color_fg = (255, 255, 255, 255)
    color_bg = (30, 30, 40, 255)

    def draw_path(pts, width, col):
        if len(pts) >= 2:
            draw.line(pts, fill=col, width=width, joint="curve")
            for p in pts:
                r = width // 2
                draw.ellipse([p[0]-r, p[1]-r, p[0]+r, p[1]+r], fill=col)

    def render_glyph(ox, oy, col, width):
        if symbol_char == 'V':
            pts = [(cx - 36 + ox, cy - 40 + oy), (cx + ox, cy + 38 + oy), (cx + 36 + ox, cy - 40 + oy)]
            draw_path(pts, width, col)
        elif symbol_char == '^':
            pts = [(cx - 36 + ox, cy + 36 + oy), (cx + ox, cy - 40 + oy), (cx + 36 + ox, cy + 36 + oy)]
            draw_path(pts, width, col)
        elif symbol_char == '—':
            pts = [(cx - 44 + ox, cy + oy), (cx + 44 + ox, cy + oy)]
            draw_path(pts, width, col)
        elif symbol_char == '|':
            pts = [(cx + ox, cy - 44 + oy), (cx + ox, cy + 44 + oy)]
            draw_path(pts, width, col)
        elif symbol_char == 'O':
            box = [cx - 38 + ox, cy - 42 + oy, cx + 38 + ox, cy + 42 + oy]
            draw.ellipse(box, outline=col, width=width)
        elif symbol_char == 'X':
            draw_path([(cx - 36 + ox, cy - 38 + oy), (cx + 36 + ox, cy + 38 + oy)], width, col)
            draw_path([(cx + 36 + ox, cy - 38 + oy), (cx - 36 + ox, cy + 38 + oy)], width, col)
        elif symbol_char == 'Z':
            pts = [
                (cx - 34 + ox, cy - 38 + oy),
                (cx + 34 + ox, cy - 38 + oy),
                (cx - 34 + ox, cy + 38 + oy),
                (cx + 34 + ox, cy + 38 + oy)
            ]
            draw_path(pts, width, col)
        elif symbol_char == 'W':
            pts = [
                (cx - 42 + ox, cy - 38 + oy),
                (cx - 22 + ox, cy + 38 + oy),
                (cx + ox, cy - 10 + oy),
                (cx + 22 + ox, cy + 38 + oy),
                (cx + 42 + ox, cy - 38 + oy)
            ]
            draw_path(pts, width, col)
        elif symbol_char == 'S':
            # S path using curved segments
            r = 20
            # top loop
            draw.arc([cx - r + ox, cy - 40 + oy, cx + r + ox, cy + oy], start=100, end=350, fill=col, width=width)
            # bottom loop
            draw.arc([cx - r + ox, cy - 6 + oy, cx + r + ox, cy + 34 + oy], start=280, end=170, fill=col, width=width)
        elif symbol_char == 'E':
            pts_stem = [(cx - 28 + ox, cy - 40 + oy), (cx - 28 + ox, cy + 40 + oy)]
            draw_path(pts_stem, width, col)
            draw_path([(cx - 28 + ox, cy - 40 + oy), (cx + 28 + ox, cy - 40 + oy)], width, col)
            draw_path([(cx - 28 + ox, cy + oy), (cx + 18 + ox, cy + oy)], width, col)
            draw_path([(cx - 28 + ox, cy + 40 + oy), (cx + 28 + ox, cy + 40 + oy)], width, col)

    # 1. Shadow layer (offset down-right)
    render_glyph(2, 4, (0, 0, 0, 160), stroke_w + 10)
    # 2. Dark outline
    render_glyph(0, 0, color_bg, stroke_w + 8)
    # 3. Crisp white foreground
    render_glyph(0, 0, color_fg, stroke_w)

    return img

def create_frog_enemy():
    """
    Creates a 256x256 pixel-art arcade frog enemy hanging from hands.
    """
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    cx, cy = 128, 140
    outline = (20, 40, 20, 255)
    green_body = (80, 200, 70, 255)
    green_dark = (50, 150, 45, 255)
    yellow_belly = (240, 240, 140, 255)

    # Reaching hands (holding balloon string above)
    # Left hand reaching up
    draw.line([(cx - 30, cy - 45), (cx - 15, cy - 70)], fill=outline, width=12)
    draw.line([(cx - 30, cy - 45), (cx - 15, cy - 70)], fill=green_body, width=8)
    # Right hand reaching up
    draw.line([(cx + 30, cy - 45), (cx + 15, cy - 70)], fill=outline, width=12)
    draw.line([(cx + 30, cy - 45), (cx + 15, cy - 70)], fill=green_body, width=8)

    # Big feet dangling at bottom
    draw.ellipse([cx - 58, cy + 42, cx - 22, cy + 68], fill=outline)
    draw.ellipse([cx - 56, cy + 44, cx - 24, cy + 66], fill=green_dark)
    draw.ellipse([cx + 22, cy + 42, cx + 58, cy + 68], fill=outline)
    draw.ellipse([cx + 24, cy + 44, cx + 56, cy + 66], fill=green_dark)

    # Main Body outline
    draw.ellipse([cx - 52, cy - 35, cx + 52, cy + 50], fill=outline)
    draw.ellipse([cx - 48, cy - 31, cx + 48, cy + 46], fill=green_body)

    # Light yellow belly
    draw.ellipse([cx - 32, cy - 5, cx + 32, cy + 42], fill=yellow_belly)

    # Big Bulging Eyes (Nitrome style)
    # Left eye outline
    draw.ellipse([cx - 45, cy - 55, cx - 10, cy - 18], fill=outline)
    draw.ellipse([cx - 42, cy - 52, cx - 13, cy - 21], fill=(255, 255, 255, 255))
    # Left pupil looking up at player
    draw.ellipse([cx - 32, cy - 48, cx - 18, cy - 32], fill=(25, 25, 35, 255))
    draw.ellipse([cx - 24, cy - 46, cx - 20, cy - 41], fill=(255, 255, 255, 255)) # highlight

    # Right eye outline
    draw.ellipse([cx + 10, cy - 55, cx + 45, cy - 18], fill=outline)
    draw.ellipse([cx + 13, cy - 52, cx + 42, cy - 21], fill=(255, 255, 255, 255))
    # Right pupil looking up
    draw.ellipse([cx + 18, cy - 48, cx + 32, cy - 32], fill=(25, 25, 35, 255))
    draw.ellipse([cx + 26, cy - 46, cx + 30, cy - 41], fill=(255, 255, 255, 255))

    # Cute wide open mouth with tongue
    draw.arc([cx - 28, cy - 10, cx + 28, cy + 22], start=20, end=160, fill=outline, width=6)
    draw.ellipse([cx - 10, cy + 8, cx + 10, cy + 24], fill=(240, 80, 100, 255)) # tongue

    # Pink cheeks
    draw.ellipse([cx - 40, cy + 2, cx - 26, cy + 14], fill=(255, 150, 160, 180))
    draw.ellipse([cx + 26, cy + 2, cx + 40, cy + 14], fill=(255, 150, 160, 180))

    return img

def create_demon_knight_enemy():
    """
    Creates a purple horned mini-knight enemy inspired by image 2.
    """
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    cx, cy = 128, 140
    outline = (25, 15, 35, 255)
    purple_dark = (75, 40, 95, 255)
    purple_light = (130, 80, 165, 255)
    red_cape = (195, 45, 55, 255)
    gold_belt = (245, 190, 50, 255)
    yellow_eyes = (255, 230, 70, 255)

    # Red spiky cape in back
    cape_pts = [
        (cx - 65, cy - 30), (cx - 75, cy + 20), (cx - 50, cy + 45),
        (cx - 30, cy + 30), (cx, cy + 50), (cx + 30, cy + 30),
        (cx + 50, cy + 45), (cx + 75, cy + 20), (cx + 65, cy - 30)
    ]
    draw.polygon(cape_pts, fill=red_cape, outline=outline)

    # Hands reaching up holding string
    draw.line([(cx - 25, cy - 40), (cx - 10, cy - 70)], fill=outline, width=12)
    draw.line([(cx - 25, cy - 40), (cx - 10, cy - 70)], fill=purple_light, width=8)
    draw.line([(cx + 25, cy - 40), (cx + 10, cy - 70)], fill=outline, width=12)
    draw.line([(cx + 25, cy - 40), (cx + 10, cy - 70)], fill=purple_light, width=8)

    # Armor body
    draw.rectangle([cx - 40, cy - 10, cx + 40, cy + 45], fill=purple_dark, outline=outline, width=4)
    # Gold belt
    draw.rectangle([cx - 40, cy + 28, cx + 40, cy + 40], fill=gold_belt, outline=outline, width=3)
    draw.rectangle([cx - 12, cy + 25, cx + 12, cy + 43], fill=(255, 255, 255, 255), outline=outline, width=3)

    # Big Horned Helmet
    # Left Horn
    horn_l = [(cx - 45, cy - 35), (cx - 70, cy - 65), (cx - 40, cy - 50)]
    draw.polygon(horn_l, fill=purple_dark, outline=outline)
    # Right Horn
    horn_r = [(cx + 45, cy - 35), (cx + 70, cy - 65), (cx + 40, cy - 50)]
    draw.polygon(horn_r, fill=purple_dark, outline=outline)

    # Helmet face block
    draw.rectangle([cx - 45, cy - 55, cx + 45, cy - 5], fill=purple_dark, outline=outline, width=4)
    # Visor slit
    draw.rectangle([cx - 36, cy - 38, cx + 36, cy - 22], fill=(15, 10, 20, 255))
    # Glowing yellow eyes
    draw.rectangle([cx - 28, cy - 34, cx - 12, cy - 26], fill=yellow_eyes)
    draw.rectangle([cx + 12, cy - 34, cx + 28, cy - 26], fill=yellow_eyes)

    # Pauldrons
    draw.ellipse([cx - 62, cy - 25, cx - 35, cy + 8], fill=purple_light, outline=outline, width=3)
    draw.ellipse([cx + 35, cy - 25, cx + 62, cy + 8], fill=purple_light, outline=outline, width=3)

    # Dangling armored boots
    draw.rectangle([cx - 38, cy + 45, cx - 14, cy + 62], fill=purple_light, outline=outline, width=3)
    draw.rectangle([cx + 14, cy + 45, cx + 38, cy + 62], fill=purple_light, outline=outline, width=3)

    return img

def create_pop_shards():
    """
    Creates balloon pop shard graphics (two torn halves + stars).
    """
    # Left popped half
    img_l = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw_l = ImageDraw.Draw(img_l)
    pts_l = [
        (85, 20), (50, 25), (25, 55), (30, 95), (65, 110),
        (80, 95), (70, 75), (85, 60), (72, 42), (85, 20)
    ]
    draw_l.polygon(pts_l, fill=(240, 60, 80, 255), outline=(30, 20, 30, 255))
    draw_l.ellipse([35, 35, 55, 65], fill=(255, 180, 190, 220)) # highlight

    # Right popped half
    img_r = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw_r = ImageDraw.Draw(img_r)
    pts_r = [
        (43, 20), (78, 25), (103, 55), (98, 95), (63, 110),
        (48, 95), (58, 75), (43, 60), (56, 42), (43, 20)
    ]
    draw_r.polygon(pts_r, fill=(240, 60, 80, 255), outline=(30, 20, 30, 255))
    draw_r.ellipse([73, 35, 93, 65], fill=(255, 180, 190, 220))

    # Sparkle / Star shard
    img_star = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw_star = ImageDraw.Draw(img_star)
    star_pts = [
        (32, 4), (40, 24), (60, 32), (40, 40),
        (32, 60), (24, 40), (4, 32), (24, 24)
    ]
    draw_star.polygon(star_pts, fill=(255, 255, 255, 255), outline=(255, 220, 80, 255))

    return img_l, img_r, img_star

def main():
    base_dir = "Assets/Sprites"
    ensure_dir(f"{base_dir}/Balloons")
    ensure_dir(f"{base_dir}/Symbols")
    ensure_dir(f"{base_dir}/Enemies")
    ensure_dir(f"{base_dir}/VFX")

    # 1. Base White Balloon (Tintable)
    balloon_white = create_balloon_sprite(base_color=(255, 255, 255), shadow_color=(200, 205, 220))
    balloon_white.save(f"{base_dir}/Balloons/balloon_white.png")

    # Vibrant Nitrome Color Variations
    color_presets = {
        "red": ((245, 60, 70), (160, 25, 45)),
        "yellow": ((255, 215, 45), (200, 140, 20)),
        "cyan": ((45, 210, 230), (20, 135, 160)),
        "blue": ((60, 140, 250), (25, 75, 185)),
        "pink": ((245, 90, 175), (170, 35, 110)),
        "purple": ((175, 75, 235), (105, 30, 155)),
        "green": ((65, 215, 80), (30, 145, 45)),
        "orange": ((255, 140, 35), (195, 75, 15)),
        "silver": ((225, 230, 240), (145, 155, 170))
    }

    for name, (base_c, shadow_c) in color_presets.items():
        b_img = create_balloon_sprite(base_color=base_c, shadow_color=shadow_c)
        b_img.save(f"{base_dir}/Balloons/balloon_{name}.png")

    print("[1/4] Balloons generated.")

    # 2. Symbol Glyphs
    symbols_map = {
        "v": "V",
        "caret": "^",
        "horizontal": "—",
        "vertical": "|",
        "o": "O",
        "z": "Z",
        "w": "W",
        "s": "S",
        "e": "E",
        "x": "X"
    }

    for filename, char in symbols_map.items():
        sym_img = create_symbol_sprite(char)
        sym_img.save(f"{base_dir}/Symbols/symbol_{filename}.png")

    print("[2/4] Symbols generated.")

    # 3. Enemy Sprites
    frog = create_frog_enemy()
    frog.save(f"{base_dir}/Enemies/enemy_frog.png")

    knight = create_demon_knight_enemy()
    knight.save(f"{base_dir}/Enemies/enemy_knight.png")

    print("[3/4] Enemies generated.")

    # 4. VFX Shards
    shard_l, shard_r, star = create_pop_shards()
    shard_l.save(f"{base_dir}/VFX/balloon_pop_left.png")
    shard_r.save(f"{base_dir}/VFX/balloon_pop_right.png")
    star.save(f"{base_dir}/VFX/pop_sparkle.png")

    print("[4/4] VFX generated.")
    print("All sprites successfully created in Assets/Sprites!")

if __name__ == "__main__":
    main()
