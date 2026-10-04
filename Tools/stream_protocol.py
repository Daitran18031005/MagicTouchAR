"""Session tag shared by landmark JSON and camera JPEG packets."""


def video_prefix(session_id):
    if not session_id:
        return b''  # Manual/legacy streamer compatibility.
    if len(session_id) != 32 or any(c not in '0123456789abcdef' for c in session_id):
        raise ValueError('session-id must contain 32 lowercase hexadecimal characters')
    return b'MTAR' + session_id.encode('ascii')
