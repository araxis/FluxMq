from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


OUT = Path(__file__).resolve().parent
W, H = 1920, 1080

BG = "#0b0f14"
TOP = "#111821"
PANEL = "#141b24"
PANEL_2 = "#101720"
BORDER = "#263341"
TEXT = "#e8edf2"
MUTED = "#8fa0b3"
DIM = "#516173"
GREEN = "#42d392"
CYAN = "#3ec7d8"
BLUE = "#5b8def"
YELLOW = "#f4bf75"
RED = "#ef6f6c"
ORANGE = "#e59f5b"
PURPLE = "#9b87f5"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    names = ["segoeuib.ttf", "segoeui.ttf"] if bold else ["segoeui.ttf", "consola.ttf"]
    for name in names:
        path = Path("C:/Windows/Fonts") / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


F10 = font(10)
F12 = font(12)
F13 = font(13)
F14 = font(14)
F16 = font(16)
F18 = font(18, True)
F20 = font(20, True)
F24 = font(24, True)
F32 = font(32, True)
MONO12 = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 12)
MONO14 = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 14)
MONO16 = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)


def rect(draw: ImageDraw.ImageDraw, box, fill, outline=BORDER, radius=7, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def pill(draw: ImageDraw.ImageDraw, xy, text, fill, fg=TEXT, pad_x=10, pad_y=4, f=F12):
    x, y = xy
    bbox = draw.textbbox((0, 0), text, font=f)
    w = bbox[2] - bbox[0] + pad_x * 2
    h = bbox[3] - bbox[1] + pad_y * 2 + 2
    draw.rounded_rectangle((x, y, x + w, y + h), radius=5, fill=fill)
    draw.text((x + pad_x, y + pad_y - 1), text, font=f, fill=fg)
    return x + w


def label(draw, xy, text, color=MUTED, f=F12):
    draw.text(xy, text, font=f, fill=color)


def title(draw, xy, text):
    draw.text(xy, text, font=F18, fill=TEXT)


def window_base(name: str, subtitle: str) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 64), fill=TOP)
    d.line((0, 64, W, 64), fill=BORDER)
    d.text((24, 18), "FluxMQ", font=F24, fill=TEXT)
    d.text((126, 22), name, font=F14, fill=MUTED)
    d.text((24, 78), subtitle, font=F32, fill=TEXT)
    d.text((24, 118), "MQTT debugging, payload inspection, replay, and observability in one local-first desktop workspace.", font=F14, fill=MUTED)
    return img, d


def draw_top_controls(d):
    pill(d, (1420, 18), "dev / local broker", "#172332", CYAN, f=F13)
    pill(d, (1572, 18), "Connected", "#123326", GREEN, f=F13)
    pill(d, (1670, 18), "Subscribe", "#1d2a3a", TEXT, f=F13)
    pill(d, (1770, 18), "Publish", "#174a53", TEXT, f=F13)


def draw_topic_tree(d, x, y, w, h):
    rect(d, (x, y, x + w, y + h), PANEL)
    title(d, (x + 18, y + 16), "Topic Explorer")
    pill(d, (x + w - 122, y + 16), "Live", "#123326", GREEN)
    d.rounded_rectangle((x + 18, y + 54, x + w - 18, y + 88), radius=5, fill="#0e141c", outline=BORDER)
    d.text((x + 32, y + 62), "Search topics, regex, tags...", font=F13, fill=DIM)

    rows = [
        ("factory", 0, "12.4k/s", GREEN),
        ("line-01", 1, "8.1k/s", GREEN),
        ("robot-arm-07", 2, "2.2k/s", CYAN),
        ("telemetry", 3, "1.9k/s", CYAN),
        ("status", 3, "286/s", YELLOW),
        ("alerts", 3, "4/s", RED),
        ("line-02", 1, "3.8k/s", GREEN),
        ("temperature", 2, "800/s", CYAN),
        ("energy", 2, "522/s", CYAN),
        ("warehouse", 0, "604/s", CYAN),
        ("dock-3", 1, "88/s", YELLOW),
        ("gps", 2, "88/s", CYAN),
        ("staging", 0, "muted", DIM),
    ]
    yy = y + 106
    for text, level, rate, color in rows:
        if text == "robot-arm-07":
            d.rounded_rectangle((x + 10, yy - 4, x + w - 10, yy + 28), radius=5, fill="#182331")
        indent = x + 20 + level * 20
        d.text((indent, yy), "▸" if level < 3 else "•", font=F13, fill=DIM)
        d.text((indent + 18, yy), text, font=F14, fill=TEXT if color != DIM else MUTED)
        d.ellipse((x + w - 96, yy + 5, x + w - 88, yy + 13), fill=color)
        d.text((x + w - 78, yy), rate, font=F12, fill=MUTED)
        yy += 34


def draw_message_table(d, x, y, w, h):
    rect(d, (x, y, x + w, y + h), PANEL)
    title(d, (x + 18, y + 16), "Message Stream")
    pill(d, (x + w - 244, y + 16), "QoS 1", "#1c2635", TEXT)
    pill(d, (x + w - 178, y + 16), "Retained off", "#1c2635", MUTED)
    pill(d, (x + w - 78, y + 16), "Pause", "#263242", TEXT)
    columns = ["Time", "Topic", "Payload", "Size", "QoS"]
    positions = [x + 20, x + 128, x + 410, x + w - 128, x + w - 64]
    d.rectangle((x + 1, y + 58, x + w - 1, y + 90), fill="#101720")
    for c, px in zip(columns, positions):
        d.text((px, y + 67), c, font=F12, fill=MUTED)
    messages = [
        ("20:55:18.222", "factory/line-01/robot-arm-07/telemetry", '{"rpm": 1420, "load": 0.72, "temp": 61.8}', "184 B", "1"),
        ("20:55:18.190", "factory/line-01/robot-arm-07/status", '{"state": "welding", "cycle": 44219}', "96 B", "1"),
        ("20:55:18.140", "factory/line-01/robot-arm-07/alerts", '{"severity": "warn", "code": "joint-drift"}', "132 B", "1"),
        ("20:55:18.108", "factory/line-02/temperature", '{"zone": "press", "value": 48.1}', "74 B", "0"),
        ("20:55:18.060", "warehouse/dock-3/gps", '{"lat": 59.3293, "lon": 18.0686}', "80 B", "0"),
        ("20:55:17.994", "factory/line-01/robot-arm-07/telemetry", '{"rpm": 1418, "load": 0.70, "temp": 61.6}', "184 B", "1"),
        ("20:55:17.950", "factory/line-02/energy", '{"kw": 82.4, "phase": "B"}', "68 B", "0"),
    ]
    yy = y + 102
    for i, row in enumerate(messages):
        if i == 0:
            d.rounded_rectangle((x + 10, yy - 6, x + w - 10, yy + 32), radius=5, fill="#182331")
        elif i % 2:
            d.rectangle((x + 1, yy - 6, x + w - 1, yy + 32), fill="#111922")
        d.text((positions[0], yy), row[0], font=MONO12, fill=MUTED)
        d.text((positions[1], yy), row[1], font=F13, fill=CYAN if "alerts" not in row[1] else RED)
        d.text((positions[2], yy), row[2], font=MONO12, fill=TEXT)
        d.text((positions[3], yy), row[3], font=F12, fill=MUTED)
        d.text((positions[4], yy), row[4], font=F12, fill=YELLOW)
        yy += 42


def draw_payload_panel(d, x, y, w, h, compact=False):
    rect(d, (x, y, x + w, y + h), PANEL)
    title(d, (x + 18, y + 16), "Payload Inspector")
    pill(d, (x + w - 236, y + 16), "JSON", "#17314a", CYAN)
    pill(d, (x + w - 170, y + 16), "Schema OK", "#123326", GREEN)
    pill(d, (x + w - 70, y + 16), "Diff", "#1c2635", TEXT)
    d.text((x + 20, y + 62), "factory/line-01/robot-arm-07/telemetry", font=F13, fill=CYAN)
    code = [
        "{",
        '  "deviceId": "robot-arm-07",',
        '  "mode": "welding",',
        '  "cycle": 44219,',
        '  "metrics": {',
        '    "rpm": 1420,',
        '    "load": 0.72,',
        '    "temperature": 61.8,',
        '    "vibration": 0.031',
        "  },",
        '  "timestamp": "2026-05-06T20:55:18.222Z"',
        "}",
    ]
    yy = y + 96
    for n, line in enumerate(code, 1):
        d.text((x + 22, yy), f"{n:>2}", font=MONO14, fill=DIM)
        color = TEXT
        if '"' in line and ":" in line:
            color = "#b9d7ff"
        if any(k in line for k in ["1420", "0.72", "61.8", "0.031"]):
            color = YELLOW
        d.text((x + 56, yy), line, font=MONO14, fill=color)
        yy += 24 if not compact else 22
    d.line((x + 18, y + h - 98, x + w - 18, y + h - 98), fill=BORDER)
    d.text((x + 20, y + h - 74), "Decoded as UTF-8 JSON · 184 bytes · QoS 1 · retained false", font=F13, fill=MUTED)
    d.text((x + 20, y + h - 44), "Schema: robot.telemetry.v2 · validation latency 1.8 ms", font=F13, fill=GREEN)


def main_workspace():
    img, d = window_base("Live Workspace", "MQTT Operations Console")
    draw_top_controls(d)
    draw_topic_tree(d, 24, 160, 360, 820)
    draw_message_table(d, 404, 160, 930, 522)
    draw_payload_panel(d, 1354, 160, 542, 522, compact=True)
    rect(d, (404, 704, 930, 980), PANEL)
    title(d, (424, 722), "Topic Timeline")
    points = [38, 64, 48, 96, 128, 72, 84, 156, 118, 92, 104, 142, 126, 168]
    base_x, base_y = 432, 936
    for i, p in enumerate(points):
        x = base_x + i * 34
        d.rectangle((x, base_y - p, x + 18, base_y), fill=CYAN if p < 140 else YELLOW)
    d.text((424, 758), "robot-arm-07 activity over the last 60 seconds", font=F13, fill=MUTED)
    rect(d, (950, 704, 1334, 980), PANEL)
    title(d, (970, 722), "Connection Health")
    for i, (name, value, color) in enumerate([("Latency", "18 ms", GREEN), ("Messages/sec", "12.4k", CYAN), ("Reconnects", "0", GREEN), ("Drops", "0", GREEN)]):
        yy = 768 + i * 44
        d.text((970, yy), name, font=F13, fill=MUTED)
        d.text((1210, yy), value, font=F18, fill=color)
    rect(d, (1354, 704, 1896, 980), PANEL)
    title(d, (1374, 722), "Publish Scratchpad")
    d.rounded_rectangle((1374, 760, 1876, 880), radius=5, fill="#0e141c", outline=BORDER)
    d.text((1392, 778), 'Topic: factory/line-01/robot-arm-07/command\nQoS: 1\nPayload: {"action":"pause-after-cycle"}', font=MONO14, fill=TEXT)
    pill(d, (1742, 914), "Send", "#174a53", TEXT, f=F14)
    img.save(OUT / "01-main-workspace.png")


def payload_debugger():
    img, d = window_base("Payload Workbench", "Payload Inspection & Message Diff")
    draw_top_controls(d)
    rect(d, (24, 160, 500, 980), PANEL)
    title(d, (44, 178), "Selected Messages")
    for i in range(9):
        yy = 230 + i * 70
        fill = "#182331" if i in (1, 2) else ("#111922" if i % 2 else PANEL)
        d.rounded_rectangle((44, yy, 480, yy + 54), radius=5, fill=fill, outline=BORDER if i in (1, 2) else fill)
        d.text((60, yy + 10), f"20:55:{18 - i:02}.{'222' if i == 1 else '190'}", font=MONO12, fill=MUTED)
        d.text((190, yy + 10), "robot-arm-07/telemetry", font=F13, fill=CYAN)
        d.text((60, yy + 32), '{"rpm": 1420, "load": 0.72, ...}', font=MONO12, fill=TEXT)
    rect(d, (524, 160, 1188, 980), PANEL)
    title(d, (544, 178), "Formatted Payload")
    pill(d, (1000, 178), "Current", "#17314a", CYAN)
    payload = [
        "{",
        '  "deviceId": "robot-arm-07",',
        '  "mode": "welding",',
        '  "cycle": 44219,',
        '  "metrics": {',
        '    "rpm": 1420,',
        '    "load": 0.72,',
        '    "temperature": 61.8,',
        '    "vibration": 0.031',
        "  },",
        '  "status": {',
        '    "jointDrift": "warn",',
        '    "safetyGate": "closed"',
        "  }",
        "}",
    ]
    yy = 236
    for n, line in enumerate(payload, 1):
        d.text((552, yy), f"{n:>2}", font=MONO16, fill=DIM)
        d.text((594, yy), line, font=MONO16, fill=YELLOW if any(k in line for k in ["1420", "0.72", "61.8", "warn"]) else TEXT)
        yy += 31
    rect(d, (1212, 160, 1896, 980), PANEL)
    title(d, (1232, 178), "Diff Against Previous")
    pill(d, (1698, 178), "3 changes", "#3a2818", YELLOW)
    diffs = [
        ('-    "rpm": 1418,', RED),
        ('+    "rpm": 1420,', GREEN),
        ('-    "load": 0.70,', RED),
        ('+    "load": 0.72,', GREEN),
        ('     "temperature": 61.8,', TEXT),
        ('+    "vibration": 0.031', GREEN),
        ('+  "status": {', GREEN),
        ('+    "jointDrift": "warn"', GREEN),
    ]
    yy = 236
    for line, color in diffs:
        d.rounded_rectangle((1232, yy - 5, 1876, yy + 27), radius=4, fill="#211719" if color == RED else "#13251d" if color == GREEN else PANEL_2)
        d.text((1248, yy), line, font=MONO16, fill=color)
        yy += 42
    d.line((1232, 720, 1876, 720), fill=BORDER)
    d.text((1232, 752), "Auto-detected JSON · schema robot.telemetry.v2 · compare window 2 messages", font=F14, fill=MUTED)
    d.text((1232, 792), "Actionable insight: load increased while vibration appeared; candidate alert threshold marker.", font=F14, fill=YELLOW)
    img.save(OUT / "02-payload-debugger.png")


def observability_replay():
    img, d = window_base("Observability & Replay", "Production Debug Timeline")
    draw_top_controls(d)
    rect(d, (24, 160, 448, 980), PANEL)
    title(d, (44, 178), "Session Recorder")
    pill(d, (304, 178), "Recording", "#3a1718", RED)
    stats = [("Session", "Factory Line 01"), ("Duration", "00:42:18"), ("Messages", "2,184,021"), ("Storage", "LiteDB · 684 MB"), ("Replayable", "100%")]
    yy = 236
    for k, v in stats:
        d.text((44, yy), k, font=F13, fill=MUTED)
        d.text((180, yy), v, font=F16, fill=TEXT if k != "Replayable" else GREEN)
        yy += 48
    d.rounded_rectangle((44, 520, 428, 880), radius=7, fill="#0e141c", outline=BORDER)
    d.text((64, 542), "Replay Controls", font=F18, fill=TEXT)
    for i, t in enumerate(["Jump to spike", "Replay 0.5x", "Replay 1x", "Replay 5x", "Inject to staging"]):
        pill(d, (64, 590 + i * 52), t, "#1c2635" if i != 4 else "#174a53", TEXT, f=F14)
    rect(d, (472, 160, 1298, 510), PANEL)
    title(d, (492, 178), "Throughput")
    bars = [60, 84, 82, 120, 144, 122, 188, 170, 204, 236, 168, 132, 112, 180, 260, 218, 176, 142, 118, 154]
    for i, b in enumerate(bars):
        x = 510 + i * 36
        d.rectangle((x, 466 - b, x + 22, 466), fill=CYAN if b < 230 else RED)
    d.text((492, 222), "Messages/sec by 10 second bucket. Red marks spike windows available for replay.", font=F13, fill=MUTED)
    rect(d, (1322, 160, 1896, 510), PANEL)
    title(d, (1342, 178), "Alert Rules")
    rules = [("Spike", "> 20k msg/s", RED), ("Silence", "no status for 30s", YELLOW), ("Payload drift", "schema mismatch", PURPLE), ("Reconnect", "broker drop", ORANGE)]
    yy = 236
    for name, desc, color in rules:
        d.ellipse((1344, yy + 6, 1356, yy + 18), fill=color)
        d.text((1372, yy), name, font=F16, fill=TEXT)
        d.text((1500, yy + 2), desc, font=F13, fill=MUTED)
        yy += 52
    rect(d, (472, 534, 1896, 980), PANEL)
    title(d, (492, 552), "Replay Timeline")
    d.line((520, 842, 1848, 842), fill=BORDER, width=3)
    events = [(620, "connect", GREEN), (760, "subscribe #", CYAN), (1020, "payload drift", PURPLE), (1190, "spike", RED), (1380, "operator publish", YELLOW), (1620, "recovered", GREEN)]
    for x, txt, color in events:
        d.line((x, 786, x, 874), fill=color, width=3)
        d.ellipse((x - 9, 833, x + 9, 851), fill=color)
        d.text((x - 34, 756), txt, font=F13, fill=TEXT)
    d.rounded_rectangle((1148, 710, 1250, 916), radius=5, outline=RED, width=3)
    d.text((1166, 924), "selected spike", font=F13, fill=RED)
    d.text((492, 600), "Select a time range, inspect exact messages, then replay into a staging broker with timing control.", font=F14, fill=MUTED)
    img.save(OUT / "03-observability-replay.png")


if __name__ == "__main__":
    main_workspace()
    payload_debugger()
    observability_replay()
    print(f"Generated mockups in {OUT}")
