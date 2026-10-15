#!/usr/bin/env bash
# macOS fallback for Tools/setup_python_env.bat - CHUA KIEM THU TREN macOS (not tested on macOS).
# Creates .venv in the project root with Python 3.11 and installs Tools/requirements.txt.
# Usage: Tools/setup_python_env.sh [/path/to/python3.11]
set -euo pipefail
cd "$(dirname "$0")/.."

is_311() { "$@" -c 'import sys; sys.exit(0 if sys.version_info[:2] == (3, 11) else 1)' >/dev/null 2>&1; }

PY=""
if [ $# -ge 1 ]; then
  if is_311 "$1"; then PY="$1"; else echo "[ERROR] $1 is not Python 3.11."; exit 1; fi
else
  for c in python3.11 python3 python; do
    if command -v "$c" >/dev/null 2>&1 && is_311 "$c"; then PY="$c"; break; fi
  done
fi
if [ -z "$PY" ]; then
  echo "[ERROR] Python 3.11 was not found. Install it (e.g. brew install python@3.11) or pass its path."
  exit 1
fi
echo "Using Python 3.11: $PY"

if [ -x .venv/bin/python ]; then
  if ! is_311 .venv/bin/python; then
    echo "[ERROR] .venv exists but is not Python 3.11. Delete the .venv folder and run again."
    exit 1
  fi
  echo "Reusing existing .venv (Python 3.11)."
else
  "$PY" -m venv .venv
fi

.venv/bin/python -m pip install --upgrade pip
.venv/bin/python -m pip install -r Tools/requirements.txt
.venv/bin/python -c "import cv2, mediapipe, numpy; print('OK: cv2', cv2.__version__, '| mediapipe', mediapipe.__version__, '| numpy', numpy.__version__)"
echo "Done: .venv is ready (Python 3.11)."
