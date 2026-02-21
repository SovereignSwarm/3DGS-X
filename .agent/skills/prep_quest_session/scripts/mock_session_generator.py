import os
import json
import argparse
import random
from datetime import datetime

def generate_mock_session(output_dir: str, frame_count: int = 10):
    """
    Scaffolds a mock Quest 3 capture folder.
    Creates dummy directories for images, depth, and poses.
    """
    if os.path.exists(output_dir) and os.path.exists(os.path.join(output_dir, "session.json")):
        print(f"Error: {output_dir} appears to be a real session or already exists. Aborting to prevent overwrite.")
        return

    os.makedirs(output_dir, exist_ok=True)
    
    # Subdirectories expected by perseusxr_process.py
    cam_dir = os.path.join(output_dir, "camera_data")
    depth_dir = os.path.join(output_dir, "depth_data")
    pose_dir = os.path.join(output_dir, "pose_data")
    
    os.makedirs(cam_dir, exist_ok=True)
    os.makedirs(depth_dir, exist_ok=True)
    os.makedirs(pose_dir, exist_ok=True)

    # 1. Generate session.json
    session_data = {
        "session_id": f"mock_{datetime.now().strftime('%Y%m%d_%H%M%S')}",
        "device": "Meta Quest 3 (Mocked)",
        "frame_count": frame_count
    }
    with open(os.path.join(output_dir, "session.json"), 'w') as f:
        json.dump(session_data, f, indent=4)
        
    # 2. Generate intrinsics
    intrinsics = {
        "fx": 500.0, "fy": 500.0,
        "cx": 512.0, "cy": 512.0,
        "width": 1024, "height": 1024,
        "distortion_coeffs": [0.0, 0.0, 0.0, 0.0, 0.0]
    }
    with open(os.path.join(output_dir, "intrinsics.json"), 'w') as f:
        json.dump(intrinsics, f, indent=4)

    # 3. Generate dummy frames
    for i in range(frame_count):
        # image yuv placeholder
        with open(os.path.join(cam_dir, f"frame_{i:04d}.yuv"), 'wb') as f:
            f.write(os.urandom(1024)) # 1kb junk
            
        # depth placeholder
        with open(os.path.join(depth_dir, f"depth_{i:04d}.bin"), 'wb') as f:
            f.write(os.urandom(512)) # 512b junk
            
        # pose placeholder (4x4 matrix)
        pose_data = {
            "timestamp": float(i) * 0.033,
            "matrix": [1.0, 0.0, 0.0, random.uniform(-1,1),
                       0.0, 1.0, 0.0, random.uniform(-1,1),
                       0.0, 0.0, 1.0, random.uniform(-1,1),
                       0.0, 0.0, 0.0, 1.0]
        }
        with open(os.path.join(pose_dir, f"pose_{i:04d}.json"), 'w') as f:
            json.dump(pose_data, f, indent=4)
            
    print(f"✅ Successfully generated mock Quest 3 session at: {output_dir}")
    print(f"Generated {frame_count} synchronized frame pairs (Image/Depth/Pose).")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Generate a mock Quest 3 capture session.")
    parser.add_argument("--output", type=str, default="./mock_session", help="Output directory path")
    parser.add_argument("--frames", type=int, default=10, help="Number of frames to mock")
    args = parser.parse_args()
    
    generate_mock_session(args.output, args.frames)
