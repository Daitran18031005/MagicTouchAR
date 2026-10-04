import unittest
from stream_protocol import video_prefix


class StreamProtocolTests(unittest.TestCase):
    def test_unity_session_header(self):
        session = '0123456789abcdef0123456789abcdef'
        self.assertEqual(video_prefix(session), b'MTAR' + session.encode('ascii'))
        self.assertEqual(len(video_prefix(session)), 36)

    def test_manual_legacy(self):
        self.assertEqual(video_prefix(''), b'')

    def test_invalid_session(self):
        for value in ('old', 'z' * 32, 'a' * 31, 'a' * 33):
            with self.subTest(value=value), self.assertRaises(ValueError):
                video_prefix(value)


if __name__ == '__main__':
    unittest.main()
