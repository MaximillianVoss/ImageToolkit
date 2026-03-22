from __future__ import annotations

import subprocess
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
APP_EXE = ROOT / "ImageToolkit" / "bin" / "Debug" / "net10.0-windows" / "ImageToolkit.exe"
SCREENSHOT_DIR = ROOT / "tmp" / "docs" / "ui_screens"
SAMPLE_IMAGE_PATH = ROOT / "tmp" / "docs" / "sample-input.png"
INITIAL_SCREENSHOT = SCREENSHOT_DIR / "initial-ui.png"
PROCESSED_SCREENSHOT = SCREENSHOT_DIR / "processed-ui.png"


def ensure_directories() -> None:
    SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)
    SAMPLE_IMAGE_PATH.parent.mkdir(parents=True, exist_ok=True)


def create_sample_image() -> None:
    image = Image.new("RGB", (900, 540), "white")
    draw = ImageDraw.Draw(image)

    for x in range(image.width):
        color = int(255 * x / image.width)
        draw.line((x, 0, x, image.height), fill=(color, 120, 255 - color))

    draw.rectangle((60, 60, 300, 220), outline="black", width=6)
    draw.ellipse((360, 80, 620, 340), outline="navy", width=8)
    draw.rectangle((680, 100, 840, 420), fill="gold", outline="black", width=4)

    image.save(SAMPLE_IMAGE_PATH)


def capture_screenshots() -> tuple[Path, Path]:
    ensure_directories()
    create_sample_image()

    subprocess.run(
        [
            str(APP_EXE),
            "--capture-screenshots",
            str(SAMPLE_IMAGE_PATH),
            str(INITIAL_SCREENSHOT),
            str(PROCESSED_SCREENSHOT),
        ],
        check=True,
    )

    return INITIAL_SCREENSHOT, PROCESSED_SCREENSHOT


if __name__ == "__main__":
    first, second = capture_screenshots()
    print(first)
    print(second)
