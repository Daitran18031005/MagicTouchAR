"""Keep tracker inference on the fastest cores of a hybrid CPU (Windows only).

MediaPipe's CPU inference splits each frame across a thread pool and waits for the
slowest worker. On Intel hybrid CPUs (for example i7-12700H: 6 performance + 8
efficiency cores) Windows readily schedules a hidden background process on the
efficiency cores. Measured there with the original hand_landmarker.task on 640x480
frames: 54-69 ms median and 93-261 ms p90 per frame with default scheduling, versus
19-20 ms median and 22-23 ms p90 on performance cores only
(Related/Docs/TRACKING_PERFORMANCE.md). Uniform CPUs are left untouched.
"""
import ctypes
import sys

CPU_SET_INFORMATION = 0
_RECORD_MIN_SIZE = 19  # Size, Type, Id, Group, LogicalProcessorIndex ... EfficiencyClass


def performance_core_mask(cpus):
    """(group, logical_index, efficiency_class) tuples -> affinity mask, or 0 to keep defaults.

    0 when there is nothing to choose: uniform cores, several processor groups or no data.
    """
    cpus = list(cpus)
    if not cpus or any(group != 0 or index >= 64 for group, index, _ in cpus):
        return 0
    fastest = max(efficiency for _, _, efficiency in cpus)
    if all(efficiency == fastest for _, _, efficiency in cpus):
        return 0
    mask = 0
    for _, index, efficiency in cpus:
        if efficiency == fastest:
            mask |= 1 << index
    return mask


def parse_cpu_sets(raw):
    """Decode SYSTEM_CPU_SET_INFORMATION records returned by GetSystemCpuSetInformation."""
    cpus, offset = [], 0
    while offset + _RECORD_MIN_SIZE <= len(raw):
        size = int.from_bytes(raw[offset:offset + 4], "little")
        if size < _RECORD_MIN_SIZE:
            break
        if int.from_bytes(raw[offset + 4:offset + 8], "little") == CPU_SET_INFORMATION:
            group = int.from_bytes(raw[offset + 12:offset + 14], "little")
            cpus.append((group, raw[offset + 14], raw[offset + 18]))
        offset += size
    return cpus


def _read_cpu_sets():
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    get_info = kernel32.GetSystemCpuSetInformation
    get_info.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(ctypes.c_ulong),
                         ctypes.c_void_p, ctypes.c_ulong]
    get_info.restype = ctypes.c_int
    length = ctypes.c_ulong(0)
    get_info(None, 0, ctypes.byref(length), None, 0)
    if not length.value:
        return []
    buffer = ctypes.create_string_buffer(length.value)
    if not get_info(buffer, length, ctypes.byref(length), None, 0):
        return []
    return parse_cpu_sets(buffer.raw[:length.value])


def apply_cpu_policy(policy="auto"):
    """'auto' pins this process to performance cores when the CPU is hybrid. Returns a log line."""
    if policy != "auto":
        return "default scheduling (--cpu-policy default)"
    if not sys.platform.startswith("win"):
        return "default scheduling (not Windows)"
    try:
        mask = performance_core_mask(_read_cpu_sets())
        if not mask:
            return "default scheduling (uniform cores)"
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.GetCurrentProcess.restype = ctypes.c_void_p
        kernel32.SetProcessAffinityMask.argtypes = [ctypes.c_void_p, ctypes.c_size_t]
        kernel32.SetProcessAffinityMask.restype = ctypes.c_int
        if not kernel32.SetProcessAffinityMask(kernel32.GetCurrentProcess(), mask):
            return f"default scheduling (affinity error {ctypes.get_last_error()})"
        return f"performance cores only (affinity mask 0x{mask:X})"
    except (OSError, AttributeError, ValueError) as error:
        return f"default scheduling ({error})"
