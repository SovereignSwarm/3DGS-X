import os
import sys
import argparse
import subprocess
from pathlib import Path
import shutil

def main():
    parser = argparse.ArgumentParser(description="PerseusXR 1-Click Offline Auto-Processor")
    parser.add_argument(
        "input_dir", 
        type=str, 
        help="The Quest RealityLog recording folder (e.g., Recordings/20260222_123456)"
    )
    args = parser.parse_args()

    input_path = Path(args.input_dir).resolve()
    if not input_path.exists() or not input_path.is_dir():
        print(f"[Error] The path {input_path} does not exist or is not a directory.")
        sys.exit(1)

    # Automatically derive the colmap output name inside the same folder
    colmap_out = input_path.parent / f"{input_path.name}_colmap_export"
    
    # Locate the embedded e2e_quest_to_colmap script
    project_root = Path(__file__).parent.resolve()
    q3r_dir = project_root / "perseusxr-reconstruction"
    e2e_script = q3r_dir / "scripts" / "e2e_quest_to_colmap.py"

    if not e2e_script.exists():
        print(f"[Error] Cannot find the engine script at {e2e_script}")
        print("Did you clone the submodule? Run: git submodule update --init --recursive")
        sys.exit(1)

    # PerseusXR Optimal YAML Overrides
    # Our engine runs best with CLAHE tone mapping on to protect specular highlights in windows/lighting.
    print(f"=== PerseusXR Post-Capture processing initiated for: {input_path.name} ===")
    print("-> Forcing 3D Gaussian Splatting optimum features (CLAHE Tone Mapping enabled).")
    
    # We will temporarily edit the pipeline_config.yml in the perseusxr-reconstruction to ensure tone mapping is enabled
    config_path = q3r_dir / "config" / "pipeline_config.yml"
    
    try:
        if config_path.exists():
            with open(config_path, "r") as f:
                config_lines = f.readlines()
                
            modified_lines = []
            in_yuv = False
            for line in config_lines:
                if "yuv_to_rgb:" in line:
                    in_yuv = True
                    modified_lines.append(line)
                elif in_yuv and "tone_mapping:" in line:
                    line = line.split(":")[0] + ": true\n"
                    modified_lines.append(line)
                elif in_yuv and "tone_mapping_method:" in line:
                    line = line.split(":")[0] + ": \"clahe\"\n"
                    modified_lines.append(line)
                elif in_yuv and "clahe_clip_limit:" in line:
                    line = line.split(":")[0] + ": 2.0\n"
                    modified_lines.append(line)
                elif line.strip() and not line.startswith(" "):
                    if in_yuv and "yuv_to_rgb" not in line:
                        in_yuv = False
                    modified_lines.append(line)
                else:
                    modified_lines.append(line)
                    
            with open(config_path, "w") as f:
                f.writelines(modified_lines)
    except Exception as e:
        print(f"[Warning] Failed to enforce CLAHE Tone mapping configs: {e}")

    # Build the massive command string
    # We use python -m syntax and update PYTHONPATH to ensure it resolves local models/ etc.
    cmd = [
        sys.executable, str(e2e_script),
        "--project_dir", str(input_path),
        "--output_dir", str(colmap_out),
        "--config", str(config_path),
        "--use_colored_pointcloud",
        "--use_optimized_color_dataset"
    ]
    
    env = os.environ.copy()
    env["PYTHONPATH"] = str(q3r_dir / "scripts")
    
    print("\nStarting TSDF Reconstruction & COLMAP Sparse Generation...")
    print("------------------------------------------------------------------")
    
    try:
        subprocess.run(cmd, env=env, check=True)
        
        print("------------------------------------------------------------------")
        print("\n[SUCCESS] PerseusXR Pre-Processing Complete!")
        print(f"-> Your pre-triangulated COLMAP sparse project is ready at:")
        print(f"   {colmap_out}")
        print("\nYou can now feed this directory into your favorite 3DGS trainer (Postshot, Polycam, etc.)")
        
    except subprocess.CalledProcessError as e:
        print(f"\n[ERROR] Pipeline failed with exit code {e.returncode}.")
        sys.exit(1)

if __name__ == "__main__":
    main()
