"""Scale-independent pinch state shared by preview and the Unity wire protocol."""
import math


class PinchState:
    ENTER = 0.18
    EXIT = 0.25
    # Same rule as Unity's PinchTransitionFilter: one ambiguous open sample only pauses
    # ink (pending) and the next open sample confirms the release; a clearly open hand
    # releases at once. A single noisy frame must not split a stroke.
    CLEAR_OPEN = EXIT * 1.5

    def __init__(self):
        self.pinching = False
        self.pending = False
        self.releases = 0
        self.ratio = 0.0

    def update(self, landmarks, aspect):
        def distance(a, b):
            return math.hypot((landmarks[a].x - landmarks[b].x) * aspect,
                              landmarks[a].y - landmarks[b].y)
        palm = max(distance(0, 9), distance(5, 17))
        gap = distance(4, 8)
        self.ratio = gap / palm if palm > 1e-5 else float('inf')
        if not self.pinching:
            self.pending = False
            self.pinching = self.ratio < self.ENTER
        elif self.ratio <= self.EXIT:
            self.pending = False
        elif self.pending or self.ratio >= self.CLEAR_OPEN:
            self.pinching = self.pending = False
            self.releases += 1
        else:
            self.pending = True
        return self.pinching
