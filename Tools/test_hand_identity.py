import unittest
from hand_identity import HandIdentityTracker


def hand(x, label='Right', y=0.5):
    return {'wrist': (x, y), 'label': label}


class IdentityTests(unittest.TestCase):
    def test_detection_order_and_two_right_hands(self):
        tracker = HandIdentityTracker()
        first = tracker.assign([hand(0.2), hand(0.8)], 0)
        original = {h['wrist']: (slot, identity) for slot, identity, h in first}
        reverse = tracker.assign([hand(0.8), hand(0.2)], 0.03)
        self.assertEqual(original, {h['wrist']: (slot, identity) for slot, identity, h in reverse})

    def test_survivor_is_not_renumbered(self):
        tracker = HandIdentityTracker()
        first = tracker.assign([hand(0.2, 'Left'), hand(0.8)], 0)
        expected = next((s, t) for s, t, h in first if h['wrist'][0] == 0.2)
        for i in range(1, 20):
            slot, identity, _ = tracker.assign([hand(0.2, 'Left')], i * 0.03)[0]
            self.assertEqual(expected, (slot, identity))

    def test_brief_occlusion_preserves_identity(self):
        tracker = HandIdentityTracker()
        before = tracker.assign([hand(0.2, 'Left')], 0)[0][:2]
        tracker.assign([], 0.10)
        after = tracker.assign([hand(0.22, 'Left')], 0.20)[0][:2]
        self.assertEqual(before, after)

    def test_long_loss_starts_new_history(self):
        tracker = HandIdentityTracker()
        old = tracker.assign([hand(0.2)], 0)[0][1]
        tracker.assign([], 1)
        self.assertNotEqual(old, tracker.assign([hand(0.2)], 1.1)[0][1])

    def test_midline_does_not_switch_player(self):
        tracker = HandIdentityTracker()
        before = tracker.assign([hand(0.49, 'Left')], 0)[0][:2]
        for i, x in enumerate([0.51, 0.48, 0.52, 0.49]):
            self.assertEqual(before, tracker.assign([hand(x, 'Left')], (i+1)*0.03)[0][:2])

    def test_crossing_keeps_different_handedness_when_close(self):
        tracker = HandIdentityTracker()
        initial = {h['label']: slot for slot, _, h in tracker.assign([hand(0.45,'Left'), hand(0.55)], 0)}
        crossed = {h['label']: slot for slot, _, h in tracker.assign([hand(0.52,'Left'), hand(0.48)], 0.03)}
        self.assertEqual(initial, crossed)


if __name__ == '__main__':
    unittest.main()
