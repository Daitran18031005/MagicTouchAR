import unittest
from types import SimpleNamespace
from pinch_state import PinchState


def pose(gap, scale=1.0, aspect=1.0):
    points = [SimpleNamespace(x=0.5, y=0.5) for _ in range(21)]
    points[9].y += 0.2 * scale
    points[8].x += gap * 0.2 * scale / aspect
    return points


class PinchTests(unittest.TestCase):
    def test_light_open_releases_immediately(self):
        state = PinchState()
        self.assertTrue(state.update(pose(0.10), 1))
        self.assertFalse(state.update(pose(0.26), 1))
        self.assertEqual(state.releases, 1)

    def test_release_survives_repinch_and_missing_packets(self):
        state = PinchState()
        for gap in [0.1, 0.3, 0.1]:
            state.update(pose(gap), 1)
        self.assertTrue(state.pinching)
        self.assertEqual(state.releases, 1)

    def test_scale_and_camera_aspect(self):
        for scale in [0.2, 0.5, 1, 2]:
            for aspect in [4/3, 16/9, 3/4]:
                state = PinchState()
                self.assertTrue(state.update(pose(0.1, scale, aspect), aspect))
                self.assertFalse(state.update(pose(0.3, scale, aspect), aspect))
                self.assertAlmostEqual(state.ratio, 0.3)

    def test_hysteresis_does_not_reenter_on_borderline_noise(self):
        state = PinchState()
        for gap in [0.24, 0.26, 0.20]:
            self.assertFalse(state.update(pose(gap), 1))
        self.assertTrue(state.update(pose(0.17), 1))
        self.assertTrue(state.update(pose(0.22), 1))

    def test_degenerate_palm_is_open(self):
        state = PinchState()
        state.update(pose(0.1), 1)
        self.assertFalse(state.update(pose(0, 0), 1))


if __name__ == '__main__':
    unittest.main()
