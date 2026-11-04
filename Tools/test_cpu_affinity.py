import struct
import unittest
from cpu_affinity import parse_cpu_sets, performance_core_mask


def record(index, efficiency, group=0, kind=0):
    # SYSTEM_CPU_SET_INFORMATION: Size, Type, Id, Group, LogicalProcessorIndex, CoreIndex,
    # LastLevelCacheIndex, NumaNodeIndex, EfficiencyClass, AllFlags, Reserved, AllocationTag.
    return struct.pack("<IIIHBBBBBBIQ", 32, kind, 256 + index, group, index, index // 2, 0, 0,
                       efficiency, 0, 0, 0)


class CpuAffinityTests(unittest.TestCase):
    def test_hybrid_cpu_keeps_only_performance_cores(self):
        # i7-12700H layout: 12 logical P-core threads (class 1), then 8 E-cores (class 0).
        cpus = [(0, i, 1) for i in range(12)] + [(0, i, 0) for i in range(12, 20)]
        self.assertEqual(performance_core_mask(cpus), 0xFFF)

    def test_uniform_cpu_is_left_alone(self):
        self.assertEqual(performance_core_mask([(0, i, 0) for i in range(8)]), 0)

    def test_no_data_or_several_groups_is_left_alone(self):
        self.assertEqual(performance_core_mask([]), 0)
        self.assertEqual(performance_core_mask([(0, 0, 1), (1, 0, 0)]), 0)

    def test_parse_windows_records(self):
        raw = b"".join(record(i, 1 if i < 2 else 0) for i in range(4))
        self.assertEqual(parse_cpu_sets(raw), [(0, 0, 1), (0, 1, 1), (0, 2, 0), (0, 3, 0)])
        self.assertEqual(performance_core_mask(parse_cpu_sets(raw)), 0b11)

    def test_parse_skips_other_record_types_and_stops_on_bad_size(self):
        raw = record(0, 1, kind=7) + record(1, 0) + struct.pack("<I", 0) + b"\0" * 28
        self.assertEqual(parse_cpu_sets(raw), [(0, 1, 0)])


if __name__ == "__main__":
    unittest.main()
