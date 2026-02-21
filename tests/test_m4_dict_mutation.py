"""
PHASE 3 — Proof of Concept: Finding M-4
===========================================
Proves that image_data_io.py::load_camera_characteristics() mutates
the source JSON dict in-place, causing a double-call to flip the Z-axis
back to its original (wrong) orientation.

This test is self-contained — no scipy dependency needed.
We test only the MUTATION BUG, not the rotation math.
"""
import json
import pytest


def extract_translation_logic(camera_pose: dict) -> list:
    """
    Exact replica of lines 118-121 from image_data_io.py.
    Only the translation mutation portion — the rotation part is irrelevant
    to proving the in-place mutation bug.
    """
    transl = camera_pose["translation"]
    transl[2] *= -1                       # ← LINE 119: mutates source dict IN PLACE
    if len(transl) < 3:                   # ← LINE 120: dead code (IndexError on line 119 first)
        transl = [0, 0, 0]
    return transl


class TestM4_DictMutation:
    """Proves M-4: In-place dict mutation on camera pose data."""

    def test_01_single_call_negates_z(self):
        """BASELINE: First call correctly negates Z."""
        camera_pose = {"translation": [0.03, 0.0, -0.01]}
        original_z = camera_pose["translation"][2]

        transl = extract_translation_logic(camera_pose)

        assert transl[2] == -original_z, (
            f"First call should negate Z: {original_z} → {transl[2]}"
        )

    def test_02_double_call_flips_z_back(self):
        """
        PROOF: Calling the function twice on the same dict flips Z back 
        to its ORIGINAL value, undoing the correction.
        """
        camera_pose = {"translation": [0.03, 0.0, -0.01]}
        original_z = camera_pose["translation"][2]  # -0.01

        # First call
        transl1 = extract_translation_logic(camera_pose)
        z_after_first = transl1[2]  # Should be 0.01

        # Second call — operates on the SAME dict (already mutated)
        transl2 = extract_translation_logic(camera_pose)
        z_after_second = transl2[2]  # Will be -0.01 again!

        assert z_after_first == -original_z
        assert z_after_second == original_z, (
            f"PROOF: Second call flips Z BACK to original! "
            f"{z_after_first} → {z_after_second} == {original_z}"
        )
        assert z_after_second != z_after_first, (
            "Double-call produces inconsistent results — function is NOT idempotent"
        )

    def test_03_original_dict_is_corrupted(self):
        """
        PROOF: The source dict is mutated. If this dict came from 
        json.load(), the loaded JSON data is permanently corrupted.
        """
        json_str = '{"translation": [0.03, 0.0, -0.01]}'
        loaded_data = json.loads(json_str)
        original_translation = loaded_data["translation"].copy()

        extract_translation_logic(loaded_data)

        assert loaded_data["translation"] != original_translation, (
            f"PROOF: Source dict was mutated from {original_translation} "
            f"to {loaded_data['translation']}"
        )

    def test_04_len_check_is_dead_code(self):
        """
        PROOF: Line 120's `if len(transl) < 3` is dead code because
        line 119's `transl[2] *= -1` raises IndexError for short lists.
        """
        camera_pose = {"translation": [0.03, 0.0]}  # only 2 elements

        with pytest.raises(IndexError):
            extract_translation_logic(camera_pose)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
