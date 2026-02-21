"""
PHASE 3 — Proof of Concept: Finding C-2
===========================================
Proves that camera timestamp drift causes the PoseInterpolator to
return None for ALL camera frames, silently dropping them from
the 3DGS reconstruction dataset.

Self-contained test — replicates the PoseInterpolator's find_nearest_frames
logic directly (no scipy/pandas dependency). This tests the EXACT same
algorithm with the same 30ms window.
"""
import csv
import os
import pytest


def find_nearest_frames(pose_timestamps: list[int], target_ts: int, window_ms: int = 30):
    """
    Replicates PoseInterpolator.find_nearest_frames() from pose_interpolator.py line 26.
    Returns (prev_ts, next_ts) or (None, None) if outside window.
    """
    before = [t for t in pose_timestamps if t <= target_ts]
    after = [t for t in pose_timestamps if t >= target_ts]

    prev = before[-1] if before and (target_ts - before[-1]) <= window_ms else None
    nxt = after[0] if after and (after[0] - target_ts) <= window_ms else None

    return prev, nxt


def interpolate_pose(pose_timestamps: list[int], target_ts: int, window_ms: int = 30):
    """
    Replicates PoseInterpolator.interpolate_pose() from pose_interpolator.py line 36.
    Returns True if a valid interpolation would succeed, False (None) otherwise.
    """
    prev, nxt = find_nearest_frames(pose_timestamps, target_ts, window_ms)
    if prev is None or nxt is None:
        return None
    return True  # Would interpolate — we only care about match/no-match


class TestC2_TimestampDrift:
    """Proves C-2: Camera base time drift causes frame drops in reconstruction."""

    def _make_pose_timestamps(self, base_time_ms: int, duration_sec: float, rate_hz: float) -> list[int]:
        """Generate pose timestamps as PoseLogger would produce at given rate."""
        n = int(duration_sec * rate_hz)
        return [base_time_ms + int(i * (1000.0 / rate_hz)) for i in range(n)]

    def _make_camera_timestamps(self, base_time_ms: int, duration_sec: float, rate_fps: float) -> list[int]:
        """Generate camera timestamps at given FPS."""
        n = int(duration_sec * rate_fps)
        return [base_time_ms + int(i * (1000.0 / rate_fps)) for i in range(n)]

    def test_01_no_drift_all_frames_matched(self):
        """
        BASELINE: When camera and pose share the same base time,
        all camera timestamps find pose matches within the 30ms window.
        """
        base_time = 1740000000000  # Unix epoch ms

        # Pose at 50Hz (like FixedUpdate), camera at 3 FPS — same base
        pose_ts = self._make_pose_timestamps(base_time, 10.0, 50.0)
        cam_ts = self._make_camera_timestamps(base_time, 10.0, 3.0)

        matched = sum(1 for t in cam_ts if interpolate_pose(pose_ts, t) is not None)
        match_rate = matched / len(cam_ts)

        assert match_rate > 0.9, (
            f"BASELINE: With no drift, {match_rate:.0%} frames matched (expected >90%)"
        )

    def test_02_60s_drift_zero_frames_matched(self):
        """
        PROOF: When camera base time is set 60 seconds before session start
        (app was open 1 minute before recording), ALL camera timestamps are
        offset by ~60,000ms. PoseInterpolator's 30ms window rejects every frame.
        """
        session_start = 1740000000000
        drift_ms = 60_000  # 60 seconds

        pose_ts = self._make_pose_timestamps(session_start, 10.0, 50.0)
        # Camera base was set 60s earlier → all timestamps shifted back
        cam_ts = self._make_camera_timestamps(session_start - drift_ms, 10.0, 3.0)

        matched = sum(1 for t in cam_ts if interpolate_pose(pose_ts, t) is not None)

        assert matched == 0, (
            f"PROOF: With 60s drift, {matched}/{len(cam_ts)} frames matched. "
            f"Expected 0 — the 30ms window rejects all drifted timestamps."
        )

    def test_03_even_2s_drift_loses_early_frames(self):
        """
        PROOF: Even a modest 2-second app-open-before-record drops
        the first ~6 camera frames (2s worth at 3fps). For short captures
        (<2s), this means 100% data loss. Extremely common scenario.
        
        With 2s drift: camera timestamps start 2000ms BEFORE pose data.
        Camera frames 0-5 (timestamps at session_start-2000 to session_start-333)
        have NO matching pose within the 30ms window.
        """
        session_start = 1740000000000
        drift_ms = 2_000  # just 2 seconds

        pose_ts = self._make_pose_timestamps(session_start, 10.0, 50.0)
        cam_ts = self._make_camera_timestamps(session_start - drift_ms, 10.0, 3.0)

        # Count how many of the first N frames are dropped
        first_n = 6  # 2s at 3fps = 6 frames
        early_matched = sum(1 for t in cam_ts[:first_n] if interpolate_pose(pose_ts, t) is not None)

        assert early_matched == 0, (
            f"PROOF: First {first_n} camera frames (2s worth) have ZERO pose matches. "
            f"Matched {early_matched}/{first_n}. "
            f"For any recording shorter than {drift_ms}ms, this means 100% data loss."
        )

        # Also prove: total match rate is degraded
        total_matched = sum(1 for t in cam_ts if interpolate_pose(pose_ts, t) is not None)
        assert total_matched < len(cam_ts), (
            f"PROOF: Overall match rate is degraded: {total_matched}/{len(cam_ts)}"
        )

    def test_04_31ms_drift_starts_dropping(self):
        """
        PROOF: Drift of just 31ms (barely above the 30ms window)
        causes frames at the start of recording to be dropped.
        """
        session_start = 1740000000000
        drift_ms = 31  # 1ms beyond window

        pose_ts = self._make_pose_timestamps(session_start, 10.0, 50.0)
        cam_ts = self._make_camera_timestamps(session_start - drift_ms, 10.0, 3.0)

        # The first camera timestamp is at (session_start - 31ms)
        # The first pose timestamp is at session_start
        # Difference = 31ms > 30ms window → first frame is dropped
        first_result = interpolate_pose(pose_ts, cam_ts[0])
        
        assert first_result is None, (
            f"PROOF: First camera frame at {cam_ts[0]} has no pose match — "
            f"nearest pose at {pose_ts[0]} is {pose_ts[0] - cam_ts[0]}ms away (>30ms window)"
        )

    def test_05_the_commented_out_fix_would_solve_it(self):
        """
        META-PROOF: If ResetBaseTime() were called (uncomment lines 102-108
        in RecordingManager.cs), the drift would be zeroed and all frames
        would match. This proves the fix is known but inactive.
        """
        session_start = 1740000000000
        drift_ms = 60_000  # 60s drift

        pose_ts = self._make_pose_timestamps(session_start, 10.0, 50.0)

        # WITHOUT reset: drifted
        cam_ts_drifted = self._make_camera_timestamps(session_start - drift_ms, 10.0, 3.0)
        matched_drifted = sum(1 for t in cam_ts_drifted if interpolate_pose(pose_ts, t) is not None)

        # WITH reset: base time would be re-synced to session_start
        cam_ts_reset = self._make_camera_timestamps(session_start, 10.0, 3.0)
        matched_reset = sum(1 for t in cam_ts_reset if interpolate_pose(pose_ts, t) is not None)

        assert matched_drifted == 0, "Without reset: 100% drop"
        assert matched_reset > 0.9 * len(cam_ts_reset), "With reset: >90% matched"
        assert matched_reset > matched_drifted, (
            f"PROOF: ResetBaseTime() would fix this. "
            f"Drifted={matched_drifted}, Reset={matched_reset}"
        )


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
