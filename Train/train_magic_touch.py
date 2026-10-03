"""
Magic Touch Gesture Classifier - Focused 11-Class Pipeline with Garbage/Unknown Rejection
==========================================================================================
Target Environment: Google Colab (GPU/CPU) or local Python 3.8+
Outputs: magic_touch_model.onnx (optimized for Unity.Sentis)

Scope:
  - 7 Basic EMNIST Letters: V, W, S, E, X, Z, O
  - 3 Simple Synthetic Lines: Caret (^), HorizontalLine (-), VerticalLine (|)
  - 1 Garbage/Unknown Class: Noise, squiggles, dots, scribbles, random shapes

Total Target Classes = 11:
  0: V
  1: Caret (^)
  2: HorizontalLine (-)
  3: VerticalLine (|)
  4: O
  5: Z
  6: W
  7: E
  8: X
  9: S
 10: Unknown (Garbage / Noise / Rejection)
"""

import os
import math
import random
import numpy as np
import cv2
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader, random_split
import torchvision

# -----------------------------------------------------------------------------
# 1. Configuration & Constants
# -----------------------------------------------------------------------------
RANDOM_SEED = 42
torch.manual_seed(RANDOM_SEED)
np.random.seed(RANDOM_SEED)
random.seed(RANDOM_SEED)

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
print(f"[*] Training on device: {DEVICE}")

IMG_SIZE = 28
NUM_CLASSES = 11
SAMPLES_PER_CLASS = 1800  # 1800 per class -> 19,800 balanced images
BATCH_SIZE = 64
EPOCHS = 15
LEARNING_RATE = 0.001

CLASS_NAMES = [
    "V",              # 0
    "Caret",          # 1
    "HorizontalLine", # 2
    "VerticalLine",   # 3
    "O",              # 4
    "Z",              # 5
    "W",              # 6
    "E",              # 7
    "X",              # 8
    "S",              # 9
    "Unknown"         # 10 (Garbage / Noise / Rejection)
]

# -----------------------------------------------------------------------------
# 2. Augmentations & Generators
# -----------------------------------------------------------------------------
def apply_augmentations(img: np.ndarray) -> np.ndarray:
    """Applies random rotation, translation, scaling, blur, and additive noise."""
    h, w = img.shape[:2]

    # Rotation (-12 to +12 deg) and scale (0.85 to 1.15)
    angle = random.uniform(-12.0, 12.0)
    scale = random.uniform(0.85, 1.15)
    center = (w / 2.0, h / 2.0)
    M = cv2.getRotationMatrix2D(center, angle, scale)

    # Translation (-2 to +2 pixels)
    M[0, 2] += random.uniform(-2.0, 2.0)
    M[1, 2] += random.uniform(-2.0, 2.0)

    augmented = cv2.warpAffine(img, M, (w, h), flags=cv2.INTER_LINEAR, borderValue=0)

    # Blur
    if random.random() > 0.4:
        ksize = random.choice([3, 5])
        augmented = cv2.GaussianBlur(augmented, (ksize, ksize), random.uniform(0.3, 0.8))

    # Additive noise
    if random.random() > 0.5:
        noise = np.random.normal(0, random.uniform(2, 6), (h, w)).astype(np.float32)
        augmented = np.clip(augmented.astype(np.float32) + noise, 0, 255).astype(np.uint8)

    return augmented


def draw_polyline(canvas: np.ndarray, pts: list, thickness: int, closed: bool = False):
    pts_array = np.array(pts, dtype=np.int32).reshape((-1, 1, 2))
    cv2.polylines(canvas, [pts_array], isClosed=closed, color=255, thickness=thickness, lineType=cv2.LINE_AA)


def generate_caret() -> np.ndarray:
    """Inverted V / Caret (^)"""
    canvas = np.zeros((IMG_SIZE, IMG_SIZE), dtype=np.uint8)
    t = random.randint(1, 2)
    left_x = random.randint(3, 6)
    right_x = random.randint(21, 24)
    apex_x = random.randint(12, 16)
    base_y = random.randint(22, 25)
    apex_y = random.randint(3, 6)

    pts = [(left_x, base_y), (apex_x, apex_y), (right_x, base_y)]
    draw_polyline(canvas, pts, thickness=t)
    return apply_augmentations(canvas)


def generate_horizontal_line() -> np.ndarray:
    """Horizontal line (-) with natural hand waviness"""
    canvas = np.zeros((IMG_SIZE, IMG_SIZE), dtype=np.uint8)
    t = random.randint(1, 3)
    y = random.randint(11, 17)
    x_start = random.randint(3, 6)
    x_end = random.randint(22, 25)
    pts = [(int(x_start + (x_end - x_start) * (i / 9.0)), int(y + random.uniform(-1.5, 1.5))) for i in range(10)]
    draw_polyline(canvas, pts, thickness=t)
    return apply_augmentations(canvas)


def generate_vertical_line() -> np.ndarray:
    """Vertical line (|) with natural hand waviness"""
    canvas = np.zeros((IMG_SIZE, IMG_SIZE), dtype=np.uint8)
    t = random.randint(1, 3)
    x = random.randint(11, 17)
    y_start = random.randint(3, 6)
    y_end = random.randint(22, 25)
    pts = [(int(x + random.uniform(-1.5, 1.5)), int(y_start + (y_end - y_start) * (i / 9.0))) for i in range(10)]
    draw_polyline(canvas, pts, thickness=t)
    return apply_augmentations(canvas)


def generate_unknown_garbage() -> np.ndarray:
    """
    Synthesizes diverse negative/garbage samples to train the rejection class:
      - Random walk squiggles / scribbles
      - Accidental dots and micro-taps
      - Random geometric shapes (triangles, boxes, stars, wavy loops)
      - Pure white noise and salt-and-pepper grids
    """
    canvas = np.zeros((IMG_SIZE, IMG_SIZE), dtype=np.uint8)
    mode = random.randint(0, 4)

    if mode == 0:
        # Random walk polyline (scribbles / random squiggles)
        num_pts = random.randint(4, 9)
        pts = [(random.randint(3, 24), random.randint(3, 24)) for _ in range(num_pts)]
        draw_polyline(canvas, pts, thickness=random.randint(1, 2))

    elif mode == 1:
        # Accidental dot, cluster of dots, or micro-tap
        num_dots = random.randint(1, 3)
        for _ in range(num_dots):
            cx = random.randint(6, 22)
            cy = random.randint(6, 22)
            r = random.randint(1, 3)
            cv2.circle(canvas, (cx, cy), r, 255, -1)

    elif mode == 2:
        # Random arbitrary polygon / triangle / star
        num_pts = random.randint(3, 6)
        pts = [(random.randint(4, 24), random.randint(4, 24)) for _ in range(num_pts)]
        draw_polyline(canvas, pts, thickness=random.randint(1, 2), closed=random.choice([True, False]))

    elif mode == 3:
        # Double random arcs or disconnected cross strokes
        for _ in range(2):
            p1 = (random.randint(3, 24), random.randint(3, 24))
            p2 = (random.randint(3, 24), random.randint(3, 24))
            cv2.line(canvas, p1, p2, 255, thickness=random.randint(1, 2), lineType=cv2.LINE_AA)

    elif mode == 4:
        # Noise pattern with random blur
        noise = np.random.uniform(0, 180, (IMG_SIZE, IMG_SIZE)).astype(np.uint8)
        mask = np.random.choice([0, 255], size=(IMG_SIZE, IMG_SIZE), p=[0.85, 0.15]).astype(np.uint8)
        canvas = cv2.bitwise_and(noise, mask)

    return apply_augmentations(canvas)


# -----------------------------------------------------------------------------
# 3. Hybrid Dataset (EMNIST Letters + Synthetic + Unknown Garbage)
# -----------------------------------------------------------------------------
class MagicTouchDataset(Dataset):
    def __init__(self, samples_per_class=SAMPLES_PER_CLASS):
        self.data = []
        self.labels = []

        print("[*] Loading EMNIST Letters dataset for: V, W, S, E, X, Z, O...")
        # EMNIST mapping in 'letters' split: 1='A' ... 26='Z'
        emnist_letter_targets = {
            'V': (0, 22),   # Class 0: V
            'O': (4, 15),   # Class 4: O
            'Z': (5, 26),   # Class 5: Z
            'W': (6, 23),   # Class 6: W
            'E': (7, 5),    # Class 7: E
            'X': (8, 24),   # Class 8: X
            'S': (9, 19)    # Class 9: S
        }

        try:
            emnist_raw = torchvision.datasets.EMNIST(root="./data", split="letters", train=True, download=True)
            emnist_pool = {target_label: [] for _, (_, target_label) in emnist_letter_targets.items()}
            for img, lbl in emnist_raw:
                if lbl in emnist_pool and len(emnist_pool[lbl]) < samples_per_class:
                    np_img = np.array(img)
                    fixed_img = np.fliplr(np.rot90(np_img, 3))  # Correct EMNIST orientation
                    emnist_pool[lbl].append(fixed_img)

            for char_name, (cid, elbl) in emnist_letter_targets.items():
                for im in emnist_pool[elbl]:
                    self.data.append(im)
                    self.labels.append(cid)
                print(f"    - Ingested {len(emnist_pool[elbl])} samples for '{char_name}' (class {cid}) from EMNIST.")
        except Exception as e:
            print(f"[!] Warning: EMNIST download failed ({e}). Falling back to synthetic generators.")

        class_generators = {
            0: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(4,4),(14,24),(24,4)], dtype=np.int32)], False, 255, random.randint(1,2)), # V fallback
            1: generate_caret,
            2: generate_horizontal_line,
            3: generate_vertical_line,
            4: lambda: cv2.circle(np.zeros((28,28), dtype=np.uint8), (14,14), random.randint(8,11), 255, random.randint(1,2)), # O fallback
            5: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(5,5),(23,5),(5,23),(23,23)], dtype=np.int32)], False, 255, random.randint(1,2)), # Z fallback
            6: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(3,5),(9,23),(14,10),(19,23),(25,5)], dtype=np.int32)], False, 255, random.randint(1,2)), # W fallback
            7: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(23,5),(7,5),(7,14),(19,14),(7,14),(7,23),(23,23)], dtype=np.int32)], False, 255, random.randint(1,2)), # E fallback
            8: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(4,4),(24,24)], dtype=np.int32), np.array([(24,4),(4,24)], dtype=np.int32)], False, 255, random.randint(1,2)), # X fallback
            9: lambda: cv2.polylines(np.zeros((28,28), dtype=np.uint8), [np.array([(22,6),(8,6),(7,13),(21,15),(20,22),(6,22)], dtype=np.int32)], False, 255, random.randint(1,2)), # S fallback
            10: generate_unknown_garbage  # Class 10: Unknown / Rejection
        }

        print("[*] Generating synthetic samples & unknown garbage samples...")
        for cid in range(NUM_CLASSES):
            current = sum(1 for l in self.labels if l == cid)
            needed = samples_per_class - current
            if needed > 0:
                gen_fn = class_generators[cid]
                for _ in range(needed):
                    img = gen_fn()
                    if len(img.shape) == 2:
                        img = apply_augmentations(img)
                    self.data.append(img)
                    self.labels.append(cid)
            print(f"    - Class {cid:2d} ({CLASS_NAMES[cid]:14s}): total {sum(1 for l in self.labels if l == cid)} samples.")

    def __len__(self):
        return len(self.labels)

    def __getitem__(self, idx):
        tensor = torch.from_numpy(self.data[idx]).unsqueeze(0).float() / 255.0
        return tensor, self.labels[idx]


# -----------------------------------------------------------------------------
# 4. Mobile CNN Architecture (Strictly ONNX & Unity Sentis Compatible)
# -----------------------------------------------------------------------------
class MobileMagicCNN(nn.Module):
    """
    Lightweight 2D CNN with standard MaxPool2d and Dropout.
    Strictly ONNX-compatible (zero AdaptiveAvgPool2d operators).
    Input:  [Batch, 1, 28, 28] (Grayscale)
    Output: [Batch, 11] (Logits for 11 classes)
    Total Parameters: ~105K (< 1ms execution on mobile in Unity.Sentis).
    """
    def __init__(self, num_classes=NUM_CLASSES):
        super(MobileMagicCNN, self).__init__()
        self.features = nn.Sequential(
            # Block 1: 28x28 -> 14x14
            nn.Conv2d(1, 16, kernel_size=3, padding=1, bias=False),
            nn.BatchNorm2d(16),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(kernel_size=2, stride=2),

            # Block 2: 14x14 -> 7x7
            nn.Conv2d(16, 32, kernel_size=3, padding=1, bias=False),
            nn.BatchNorm2d(32),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(kernel_size=2, stride=2),

            # Block 3: 7x7 -> 3x3 (standard MaxPool2d: 7 // 2 = 3)
            nn.Conv2d(32, 64, kernel_size=3, padding=1, bias=False),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(kernel_size=2, stride=2)
        )

        self.classifier = nn.Sequential(
            nn.Flatten(),
            nn.Dropout(0.3),
            nn.Linear(64 * 3 * 3, 128),  # 64 channels * 3 * 3 = 576 features
            nn.ReLU(inplace=True),
            nn.Dropout(0.2),
            nn.Linear(128, num_classes)
        )

    def forward(self, x):
        feat = self.features(x)
        return self.classifier(feat)


# -----------------------------------------------------------------------------
# 5. Training Pipeline
# -----------------------------------------------------------------------------
def train_model():
    dataset = MagicTouchDataset(samples_per_class=SAMPLES_PER_CLASS)
    val_size = int(len(dataset) * 0.15)
    train_size = len(dataset) - val_size
    train_set, val_set = random_split(dataset, [train_size, val_size])

    train_loader = DataLoader(train_set, batch_size=BATCH_SIZE, shuffle=True, num_workers=2, pin_memory=True)
    val_loader = DataLoader(val_set, batch_size=BATCH_SIZE, shuffle=False, num_workers=2, pin_memory=True)

    model = MobileMagicCNN(num_classes=NUM_CLASSES).to(DEVICE)
    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=LEARNING_RATE, weight_decay=1e-4)
    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=EPOCHS)

    print(f"\n[*] Training for {EPOCHS} epochs on {train_size} train / {val_size} val samples...\n")
    best_val_acc = 0.0

    for epoch in range(1, EPOCHS + 1):
        model.train()
        running_loss, correct, total = 0.0, 0, 0
        for inputs, targets in train_loader:
            inputs, targets = inputs.to(DEVICE), targets.to(DEVICE)
            optimizer.zero_grad()
            outputs = model(inputs)
            loss = criterion(outputs, targets)
            loss.backward()
            optimizer.step()

            running_loss += loss.item() * inputs.size(0)
            _, predicted = outputs.max(1)
            total += targets.size(0)
            correct += predicted.eq(targets).sum().item()

        train_acc = 100.0 * correct / total
        train_loss = running_loss / total
        scheduler.step()

        model.eval()
        val_loss, val_correct, val_total = 0.0, 0, 0
        with torch.no_grad():
            for inputs, targets in val_loader:
                inputs, targets = inputs.to(DEVICE), targets.to(DEVICE)
                outputs = model(inputs)
                loss = criterion(outputs, targets)

                val_loss += loss.item() * inputs.size(0)
                _, predicted = outputs.max(1)
                val_total += targets.size(0)
                val_correct += predicted.eq(targets).sum().item()

        val_acc = 100.0 * val_correct / val_total
        val_loss = val_loss / val_total

        print(f"Epoch [{epoch:2d}/{EPOCHS:2d}] "
              f"Train Loss: {train_loss:.4f} | Train Acc: {train_acc:.2f}% | "
              f"Val Loss: {val_loss:.4f} | Val Acc: {val_acc:.2f}%")

        if val_acc > best_val_acc:
            best_val_acc = val_acc
            torch.save(model.state_dict(), "best_magic_touch.pth")

    print(f"\n[+] Training complete! Best Validation Accuracy: {best_val_acc:.2f}%")
    return model


# -----------------------------------------------------------------------------
# 6. ONNX Export for Unity.Sentis
# -----------------------------------------------------------------------------
def export_to_onnx(model, output_path="magic_touch_model.onnx"):
    model.eval()
    model.to("cpu")
    dummy_input = torch.randn(1, 1, IMG_SIZE, IMG_SIZE, dtype=torch.float32)

    print(f"[*] Exporting model to ONNX: '{output_path}'...")
    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        export_params=True,
        opset_version=14,               # Rock-solid compatibility with Unity.Sentis
        do_constant_folding=True,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch_size"}, "output": {0: "batch_size"}}
    )
    print(f"[+] Successfully exported '{output_path}' ({os.path.getsize(output_path) / 1024:.2f} KB)!")


if __name__ == "__main__":
    trained_model = train_model()
    export_to_onnx(trained_model, "magic_touch_model.onnx")
