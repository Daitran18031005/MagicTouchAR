"""Scale-independent pinch state shared by preview and the Unity wire protocol."""
import math


class PinchState:
    ENTER = 0.18
    EXIT = 0.25

    def __init__(self):
        self.pinching = False
        self.releases = 0
        self.ratio = 0.0

    def update(self, landmarks, aspect):
        def distance(a, b):
            return math.hypot((landmarks[a].x - landmarks[b].x) * aspect,
                              landmarks[a].y - landmarks[b].y)
        palm = max(distance(0, 9), distance(5, 17))
        gap = distance(4, 8)
        self.ratio = gap / palm if palm > 1e-5 else float('inf')
        previous = self.pinching
        self.pinching = (self.ratio <= self.EXIT if previous
                         else self.ratio < self.ENTER)
        if previous and not self.pinching:
            self.releases += 1
        return self.pinching
