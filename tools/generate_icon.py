"""Generate AutoCenter app icon as a multi-resolution .ico AND the full Microsoft Store PNG asset set.

Run: python tools/generate_icon.py
Outputs:
  AutoCenter/Assets/AutoCenter.ico
  AutoCenter/Assets/Square44x44Logo*.png  (+ targetsize variants)
  AutoCenter/Assets/Square71x71Logo*.png
  AutoCenter/Assets/Square150x150Logo*.png
  AutoCenter/Assets/Square310x310Logo*.png
  AutoCenter/Assets/Wide310x150Logo*.png
  AutoCenter/Assets/StoreLogo*.png
  AutoCenter/Assets/SplashScreen*.png

Design: transparent canvas (no squircle plate). Four blue corner brackets frame a
centered Windows 11-style window with caption buttons. The bracket+window comp
fills the canvas — no padding plate eats the bounds.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter


SUPERSAMPLE = 1024
ASSETS = Path(__file__).resolve().parent.parent / "AutoCenter" / "Assets"
ICO_OUT = ASSETS / "AutoCenter.ico"
ICO_SIZES = [256, 128, 64, 48, 32, 24, 16]
SCALES = [100, 125, 150, 200, 400]
SQUARE44_TARGETSIZES = [16, 24, 32, 48, 256]

# Brand: Fluent blue. Used for the corner brackets and the window title-bar accent.
BRACKET_COLOR = (30, 105, 230, 255)        # vivid blue, matches Fluent accent
WINDOW_BODY = (255, 255, 255, 255)
WINDOW_BORDER = (190, 198, 212, 255)       # thin neutral border around the window
TITLEBAR_FILL = (243, 245, 249, 255)       # subtle Windows 11 caption tint
GLYPH_COLOR = (40, 44, 52, 255)            # near-black for min/max glyphs
CLOSE_RED = (196, 43, 28, 255)             # Windows 11 close-hover red


def draw_corner_bracket(draw, x, y, length, thickness, color, dx, dy):
    """Draw an L-shaped corner bracket. (dx,dy) = direction the L opens (e.g., (1,1) = top-left)."""
    half = thickness // 2
    if dx > 0:
        draw.rounded_rectangle(
            (x - half, y - half, x + length, y + half),
            radius=half, fill=color,
        )
    else:
        draw.rounded_rectangle(
            (x - length, y - half, x + half, y + half),
            radius=half, fill=color,
        )
    if dy > 0:
        draw.rounded_rectangle(
            (x - half, y - half, x + half, y + length),
            radius=half, fill=color,
        )
    else:
        draw.rounded_rectangle(
            (x - half, y - length, x + half, y + half),
            radius=half, fill=color,
        )


def render_master():
    """Render the high-res master. Transparent background, brackets + Win11 window."""
    s = SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # --- Corner brackets, pushed to the canvas edges (no squircle plate to inset against) ---
    inset = int(s * 0.06)
    fx0, fy0 = inset, inset
    fx1, fy1 = s - inset, s - inset
    cx, cy = s // 2, s // 2

    bracket_len = int(s * 0.22)
    bracket_thick = int(s * 0.055)

    draw_corner_bracket(d, fx0, fy0, bracket_len, bracket_thick, BRACKET_COLOR, +1, +1)
    draw_corner_bracket(d, fx1, fy0, bracket_len, bracket_thick, BRACKET_COLOR, -1, +1)
    draw_corner_bracket(d, fx0, fy1, bracket_len, bracket_thick, BRACKET_COLOR, +1, -1)
    draw_corner_bracket(d, fx1, fy1, bracket_len, bracket_thick, BRACKET_COLOR, -1, -1)

    # --- Centered Windows 11-style window ---
    win_w = int(s * 0.62)
    win_h = int(s * 0.46)
    win_box = (cx - win_w // 2, cy - win_h // 2, cx + win_w // 2, cy + win_h // 2)
    sx0, sy0, sx1, sy1 = win_box
    win_radius = int(s * 0.035)             # ~ Win11 8px on a 240px window

    # Soft drop shadow under the window so it floats on a transparent canvas
    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle(
        (sx0, sy0 + int(s * 0.010), sx1, sy1 + int(s * 0.022)),
        radius=win_radius, fill=(0, 0, 0, 60),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=s * 0.014))
    img.alpha_composite(shadow)

    # Window body (white) + thin neutral border
    d.rounded_rectangle(win_box, radius=win_radius, fill=WINDOW_BODY)
    border_thick = max(2, int(s * 0.0035))
    d.rounded_rectangle(win_box, radius=win_radius, outline=WINDOW_BORDER, width=border_thick)

    # Mask of the window's rounded shape — used to clip the title bar and caption-button fills
    win_mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(win_mask).rounded_rectangle(win_box, radius=win_radius, fill=255)

    # Title bar strip (subtle Win11 caption tint), clipped to the window's curves
    bar_h = int(win_h * 0.32)
    bar_y1 = sy0 + bar_h
    bar_layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(bar_layer).rectangle((sx0, sy0, sx1, bar_y1), fill=TITLEBAR_FILL)
    bar_clipped = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    bar_clipped.paste(bar_layer, (0, 0), win_mask)
    img.alpha_composite(bar_clipped)

    # Subtle 1px separator under the title bar (Win11 has a soft line between caption and content)
    sep_thick = max(1, int(s * 0.0018))
    sep_layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(sep_layer).rectangle(
        (sx0, bar_y1 - sep_thick, sx1, bar_y1), fill=(0, 0, 0, 22)
    )
    sep_clipped = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sep_clipped.paste(sep_layer, (0, 0), win_mask)
    img.alpha_composite(sep_clipped)

    # --- Windows 11 caption buttons (right-aligned: min, max, close) ---
    # Each button is wider than tall, like the real Win11 caption: ~46x32. We render the
    # Close button in its hover state (red bg, white X) since that's the most recognizable.
    bar_mid_y = sy0 + bar_h // 2
    btn_w = int(win_w * 0.115)              # caption-slot width
    glyph_size = int(s * 0.020)             # half-extent of each glyph
    glyph_thick = max(2, int(s * 0.0042))   # thin Segoe-style stroke

    # Close button — red slot, clipped to the window's top-right rounded corner
    close_x1 = sx1
    close_x0 = close_x1 - btn_w
    close_layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(close_layer).rectangle(
        (close_x0, sy0, close_x1, bar_y1), fill=CLOSE_RED
    )
    close_clipped = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    close_clipped.paste(close_layer, (0, 0), win_mask)
    img.alpha_composite(close_clipped)

    # White X glyph on the close button
    cx_close = (close_x0 + close_x1) // 2
    d.line(
        [(cx_close - glyph_size, bar_mid_y - glyph_size),
         (cx_close + glyph_size, bar_mid_y + glyph_size)],
        fill=(255, 255, 255, 255), width=glyph_thick,
    )
    d.line(
        [(cx_close + glyph_size, bar_mid_y - glyph_size),
         (cx_close - glyph_size, bar_mid_y + glyph_size)],
        fill=(255, 255, 255, 255), width=glyph_thick,
    )

    # Maximize button — hollow square outline glyph
    cx_max = close_x0 - btn_w // 2
    d.rectangle(
        (cx_max - glyph_size, bar_mid_y - glyph_size,
         cx_max + glyph_size, bar_mid_y + glyph_size),
        outline=GLYPH_COLOR, width=glyph_thick,
    )

    # Minimize button — single horizontal line glyph
    cx_min = cx_max - btn_w
    line_half = max(1, glyph_thick // 2)
    d.rectangle(
        (cx_min - glyph_size, bar_mid_y - line_half,
         cx_min + glyph_size, bar_mid_y + line_half),
        fill=GLYPH_COLOR,
    )

    return img


def render_simple(size):
    """Simplified version for very small sizes (<=32). Drops shadow/border/glyphs;
    keeps the four blue brackets + a clean white window with a red close slot so the
    icon stays legible at 16-32 px."""
    s = SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    inset = int(s * 0.05)
    fx0, fy0 = inset, inset
    fx1, fy1 = s - inset, s - inset
    cx, cy = s // 2, s // 2

    bracket_len = int(s * 0.24)
    bracket_thick = int(s * 0.085)

    draw_corner_bracket(d, fx0, fy0, bracket_len, bracket_thick, BRACKET_COLOR, +1, +1)
    draw_corner_bracket(d, fx1, fy0, bracket_len, bracket_thick, BRACKET_COLOR, -1, +1)
    draw_corner_bracket(d, fx0, fy1, bracket_len, bracket_thick, BRACKET_COLOR, +1, -1)
    draw_corner_bracket(d, fx1, fy1, bracket_len, bracket_thick, BRACKET_COLOR, -1, -1)

    win_w = int(s * 0.56)
    win_h = int(s * 0.42)
    win_box = (cx - win_w // 2, cy - win_h // 2, cx + win_w // 2, cy + win_h // 2)
    sx0, sy0, sx1, sy1 = win_box
    win_radius = int(s * 0.05)

    d.rounded_rectangle(win_box, radius=win_radius, fill=WINDOW_BODY)

    # Tiny red close slot in the top-right so even the 24/32 px icon reads as a "window"
    if size >= 24:
        bar_h = int(win_h * 0.34)
        slot_w = int(win_w * 0.22)
        win_mask = Image.new("L", (s, s), 0)
        ImageDraw.Draw(win_mask).rounded_rectangle(win_box, radius=win_radius, fill=255)
        close_layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        ImageDraw.Draw(close_layer).rectangle(
            (sx1 - slot_w, sy0, sx1, sy0 + bar_h), fill=CLOSE_RED
        )
        close_clipped = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        close_clipped.paste(close_layer, (0, 0), win_mask)
        img.alpha_composite(close_clipped)

    return img.resize((size, size), Image.LANCZOS)


def square_at(master, size):
    """Downscale the master to a square of the given size."""
    if size <= 32:
        return render_simple(size)
    return master.resize((size, size), Image.LANCZOS)


def rect_with_centered_icon(master, w, h):
    """Create a transparent rectangle with the icon centered.

    Used for Wide tiles and the splash screen, both of which need a non-square canvas
    while the icon itself remains square. Margin keeps the icon from touching edges.
    """
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    icon_size = int(min(w, h) * 0.85)
    icon = square_at(master, icon_size)
    x = (w - icon_size) // 2
    y = (h - icon_size) // 2
    canvas.alpha_composite(icon, (x, y))
    return canvas


def write_scaled_set(name_stem, base_w, base_h, master, square=True):
    """Emit `<stem>.scale-100/125/150/200/400.png` plus a base `<stem>.png` (== scale-100)."""
    for scale in SCALES:
        w = round(base_w * scale / 100)
        h = round(base_h * scale / 100)
        img = square_at(master, w) if square and base_w == base_h else rect_with_centered_icon(master, w, h)
        out = ASSETS / f"{name_stem}.scale-{scale}.png"
        img.save(out, format="PNG", optimize=True)
    base = square_at(master, base_w) if square and base_w == base_h else rect_with_centered_icon(master, base_w, base_h)
    base.save(ASSETS / f"{name_stem}.png", format="PNG", optimize=True)


def write_targetsize_set(name_stem, master):
    """Emit Square44 targetsize variants (plated and unplated) used for taskbar/start glyphs."""
    for ts in SQUARE44_TARGETSIZES:
        img = square_at(master, ts)
        img.save(ASSETS / f"{name_stem}.targetsize-{ts}.png", format="PNG", optimize=True)
        img.save(ASSETS / f"{name_stem}.targetsize-{ts}_altform-unplated.png", format="PNG", optimize=True)


def write_ico(master):
    frames = [render_simple(s) if s <= 32 else master.resize((s, s), Image.LANCZOS) for s in ICO_SIZES]
    frames[0].save(
        ICO_OUT,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[1:],
    )
    print(f"wrote {ICO_OUT} ({', '.join(f'{s}x{s}' for s in ICO_SIZES)})")


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    master = render_master()

    write_ico(master)

    # Tile / Store assets — base sizes per Microsoft Store requirements.
    tiles = [
        ("Square44x44Logo",   44,  44,  True),
        ("Square71x71Logo",   71,  71,  True),
        ("Square150x150Logo", 150, 150, True),
        ("Square310x310Logo", 310, 310, True),
        ("Wide310x150Logo",   310, 150, False),
        ("StoreLogo",          50,  50, True),
        ("SplashScreen",      620, 300, False),
    ]
    for stem, w, h, square in tiles:
        write_scaled_set(stem, w, h, master, square=square)
        print(f"wrote {stem} (base {w}x{h} + scales {SCALES})")

    write_targetsize_set("Square44x44Logo", master)
    print(f"wrote Square44x44Logo targetsize variants {SQUARE44_TARGETSIZES}")


if __name__ == "__main__":
    main()
