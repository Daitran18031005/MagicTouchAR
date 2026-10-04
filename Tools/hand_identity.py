"""Stable two-player slots, independent of detection order or left/right anatomy.

Uses temporal wrist proximity; handedness is only a weak tie-breaker, so two
right hands work too. A missing hand reserves its slot briefly instead of
renumbering the surviving hand. Coordinates are camera coordinates (unmirrored).
"""
from itertools import permutations
from math import hypot


class HandIdentityTracker:
    def __init__(self, retention=0.45, mirror=True):
        self.retention = retention
        self.mirror = mirror
        self.slots = [None, None]
        self.next_id = 1

    def assign(self, hands, now):
        hands = hands[:2]
        for i, slot in enumerate(self.slots):
            if slot is not None and now - slot['seen'] > self.retention:
                self.slots[i] = None
        if not hands:
            return []

        def cost(hand, index):
            slot = self.slots[index]
            x, y = hand['wrist']
            if slot is None:
                screen_x = 1 - x if self.mirror else x
                return 0.40 + abs(screen_x - (0.25 if index == 0 else 0.75)) * 0.15
            distance = hypot(x - slot['wrist'][0], y - slot['wrist'][1])
            label = hand.get('label', '')
            mismatch = bool(label and slot['label'] and label != slot['label'])
            return distance + (0.06 if mismatch else 0.0)

        mapping = min(permutations(range(2), len(hands)),
                      key=lambda slots: sum(cost(h, s) for h, s in zip(hands, slots)))
        assigned = []
        for hand, index in zip(hands, mapping):
            old = self.slots[index]
            # A large discontinuity is a new track, never inherited filter history.
            if old is None or hypot(hand['wrist'][0] - old['wrist'][0],
                                    hand['wrist'][1] - old['wrist'][1]) > 0.45:
                track_id = self.next_id
                self.next_id += 1
            else:
                track_id = old['id']
            self.slots[index] = dict(wrist=hand['wrist'], label=hand.get('label', ''),
                                     seen=now, id=track_id)
            assigned.append((index, track_id, hand))
        return assigned
