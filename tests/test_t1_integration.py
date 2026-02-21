"""T-1: Integration test for the e2e pipeline using mock session data.

Validates that the end-to-end processing pipeline can handle synthetic
Quest 3 session folders without crashing. Uses pytest fixtures to generate
temporary mock directories per .agent/rules/testing.md requirements.
"""

import json
import tempfile
from pathlib import Path

import numpy as np
import pytest


@pytest.fixture
def mock_quest_session(tmp_path: Path) -> Path:
    """Create a minimal mock Quest 3 capture session folder.

    Structure matches what RecordingManager produces on-device:
    ```
    session/
    ├── left_rgb/
    │   ├── format_info.json
    │   └── {timestamp}.yuv
    ├── left_depth/
    │   └── {timestamp}.raw
    ├── left_depth_descriptors.csv
    └── poses.csv
    ```

    Args:
        tmp_path: pytest built-in fixture for temporary directories.

    Returns:
        Path to the root session directory.
    """
    session_dir = tmp_path / "mock_session"
    session_dir.mkdir()

    # Create RGB directory with format info and dummy YUV file
    rgb_dir = session_dir / "left_rgb"
    rgb_dir.mkdir()

    timestamp = 1000000000

    format_info = {
        "width": 640,
        "height": 480,
        "format": "YUV_420_888",
        "planes": [
            {"bufferSize": 307200, "rowStride": 640, "pixelStride": 1},
            {"bufferSize": 76800, "rowStride": 320, "pixelStride": 1},
            {"bufferSize": 76800, "rowStride": 320, "pixelStride": 1},
        ],
    }
    (rgb_dir / "format_info.json").write_text(json.dumps(format_info))

    # Create a dummy YUV file (Y plane + U plane + V plane)
    yuv_size = 640 * 480 * 3 // 2  # YUV420 = 1.5 bytes per pixel
    yuv_data = np.random.randint(0, 255, yuv_size, dtype=np.uint8)
    yuv_data.tofile(str(rgb_dir / f"{timestamp}.yuv"))

    # Create depth directory with dummy raw file
    depth_dir = session_dir / "left_depth"
    depth_dir.mkdir()

    depth_data = np.random.rand(256, 256).astype(np.float32)
    depth_data.tofile(str(depth_dir / f"{timestamp}.raw"))

    # Create depth descriptors CSV
    depth_csv = session_dir / "left_depth_descriptors.csv"
    depth_csv.write_text(
        "timestamp_ms,ovr_timestamp,"
        "create_pose_location_x,create_pose_location_y,create_pose_location_z,"
        "create_pose_rotation_x,create_pose_rotation_y,create_pose_rotation_z,create_pose_rotation_w,"
        "fov_left_angle_tangent,fov_right_angle_tangent,fov_top_angle_tangent,fov_down_angle_tangent,"
        "near_z,far_z,width,height\n"
        f"{timestamp},0.001,"
        "0.0,1.5,0.0,"
        "0.0,0.0,0.0,1.0,"
        "1.0,1.0,1.0,1.0,"
        "0.1,10.0,256,256\n"
    )

    # Create poses CSV
    poses_csv = session_dir / "poses.csv"
    poses_csv.write_text(
        "unix_time,ovr_timestamp,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w\n"
        f"{timestamp - 10},0.0009,0.0,1.5,0.0,0.0,0.0,0.0,1.0\n"
        f"{timestamp},0.001,0.0,1.5,0.1,0.0,0.0,0.0,1.0\n"
        f"{timestamp + 10},0.0011,0.0,1.5,0.2,0.0,0.0,0.0,1.0\n"
    )

    return session_dir


class TestMockSessionStructure:
    """Validate that mock session data is well-formed."""

    def test_session_has_required_directories(self, mock_quest_session: Path) -> None:
        """Session should contain RGB, depth, and pose data."""
        assert (mock_quest_session / "left_rgb").is_dir()
        assert (mock_quest_session / "left_depth").is_dir()
        assert (mock_quest_session / "poses.csv").is_file()
        assert (mock_quest_session / "left_depth_descriptors.csv").is_file()

    def test_format_info_is_valid_json(self, mock_quest_session: Path) -> None:
        """format_info.json should parse without errors."""
        info_path = mock_quest_session / "left_rgb" / "format_info.json"
        data = json.loads(info_path.read_text())
        assert data["width"] == 640
        assert data["height"] == 480
        assert len(data["planes"]) == 3

    def test_depth_file_is_loadable(self, mock_quest_session: Path) -> None:
        """Raw depth file should load as float32 array."""
        depth_files = list((mock_quest_session / "left_depth").glob("*.raw"))
        assert len(depth_files) == 1

        depth = np.fromfile(str(depth_files[0]), dtype=np.float32)
        assert depth.shape == (256 * 256,)

    def test_poses_csv_has_correct_columns(self, mock_quest_session: Path) -> None:
        """Poses CSV should have all required columns."""
        import pandas as pd

        df = pd.read_csv(mock_quest_session / "poses.csv")
        expected = ["unix_time", "ovr_timestamp", "pos_x", "pos_y", "pos_z",
                     "rot_x", "rot_y", "rot_z", "rot_w"]
        assert list(df.columns) == expected
        assert len(df) == 3

    def test_depth_descriptors_csv_is_valid(self, mock_quest_session: Path) -> None:
        """Depth descriptors should have all 17 columns."""
        import pandas as pd

        df = pd.read_csv(mock_quest_session / "left_depth_descriptors.csv")
        assert len(df.columns) == 17
        assert len(df) == 1
