import os
import sys
import argparse
import subprocess
import tempfile
import yaml
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
    
    # Enforce CLAHE tone mapping in pipeline config using proper YAML parsing.
    # Previous implementation used string splitting which broke on colon-containing values.
    config_path = q3r_dir / "config" / "pipeline_config.yml"
    
    try:
        if config_path.exists():
            # Create backup before modifying tracked submodule file
            backup_path = config_path.with_suffix('.yml.bak')
            shutil.copy2(config_path, backup_path)

            with open(config_path, "r") as f:
                config = yaml.safe_load(f)

            # Safely update yuv_to_rgb section
            if "yuv_to_rgb" not in config:
                config["yuv_to_rgb"] = {}

            config["yuv_to_rgb"]["tone_mapping"] = True
            config["yuv_to_rgb"]["tone_mapping_method"] = "clahe"
            config["yuv_to_rgb"]["clahe_clip_limit"] = 2.0

            # Atomic write via temp file to prevent TOCTOU corruption
            tmp_fd, tmp_path = tempfile.mkstemp(
                dir=config_path.parent, suffix='.yml.tmp'
            )
            try:
                with os.fdopen(tmp_fd, 'w') as f:
                    yaml.dump(config, f, default_flow_style=False, sort_keys=False)
                os.replace(tmp_path, str(config_path))
            except Exception:
                os.unlink(tmp_path)
                raise
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
