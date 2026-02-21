<div align="center">
  <img src="Assets/RealityLog/Textures/PerseusXR_Logo.svg" width="300" alt="PerseusXR Logo"/>
  <h1>PerseusXR Research</h1>
  <p><strong>Next-Generation Spatial Data Capture Engine for Meta Quest 3</strong></p>
  <a href="http://www.perseusxr.com">www.perseusxr.com</a>
</div>

<br/>

Welcome to the **PerseusXR Research** repository. This project provides a robust, professional-grade Unity application and Python processing pipeline for capturing perfectly synchronized real-world spatial data directly on the Meta Quest 3. It is engineered specifically for ultra-high-quality **3D Gaussian Splatting (3DGS)** and advanced photogrammetry.

---

## 🌟 Vision & Features

The spatial capture experience has been rebuilt from the ground up to guarantee flawless data logging through a sleek, high-tech ecosystem. 

- **Point-Cloud Spatial Heatmaps:** Never miss a blind spot. As you scan, the physical environment is painted with a sleek point-cloud sphere overlay in real-time, utilizing Meta's MRUK to track volumetric coverage. 
- **The 3-Pass Guided Capture Machine:** Achieve professional scans instantly. The system dynamically orchestrates UX guidance across three distinct phases: *Geometry (Macro), Texture Details (Micro), and Reflections (View-Dependent).*
- **Hardware-Level Camera Locks:** PerseusXR interfaces directly with the Android Camera2 API to hardcode Auto-Exposure (AE) and Auto-Focus (AF) locks, eliminating the "cloudy floater" motion blur artifacts that frequently degrade standard 3DGS datasets.
- **Sensory Gamification:** Perfect your spatial scanning cadence. Integrated haptic rumbles and spatial audio cues actively warn you if your movements are too erratic.
- **The 1-Click Offline Engine:** Skip hours of traditional COLMAP feature matching. Our unified python wrapper (`perseusxr_process.py`) directly ingests Quest 3 SLAM poses and Depth maps to pre-triangulate scenes automatically, armed with forced CLAHE Tone Mapping to protect vital specular highlights.

## 🚀 Getting Started

### 1. Headset Installation (Unity)
1. Open this repository in **Unity 2022.3** (or later).
2. Build the project into an Android APK. 
3. Sideload the application onto your Meta Quest 3:
   ```bash
   adb install -r Build/PerseusXR.apk
   ```
4. **Important:** On your very first launch in the headset, you must actively grant all Camera and Storage permissions when prompted by Horizon OS.

### 2. PC Processing Setup (Python)
1. Ensure you have **Python 3.10+** installed on your workstation.
2. Initialize the required submodules for the local reconstruction engine:
   ```bash
   git submodule update --init --recursive
   ```
3. Install the Python dependencies located within the `quest-3d-reconstruction` submodule.

## 🎥 Capture Workflow

Scanning the physical world with PerseusXR is designed to be highly intuitive. 

1. **Launch & Look:** Open the app and allow the MRUK spatial anchors to map your immediate surroundings.
2. **Commence Scan:** Press the **Menu Button** on your left controller to begin the capture sequence.
3. **Paint the World:** 
   - Move smoothly. The ghostly white sphere overlay will track your spatial coverage.
   - For close-up material details, physically lean in until the UI locks into its **Cyan** success state.
   - For recording specular surfaces (Pass 3), orbit the target to trigger the glowing Directional Anchors.
4. **Finalize:** Press the Menu Button again to end the session. The Post-Capture Dashboard will appear, rating your scan quality and confirming the secure save path.

## 💾 Extracting & Processing Data

Once your capture session is finished, connect your Quest 3 to your PC via USB.

### Step 1: Pull the Data
Navigate to your Quest's internal Android storage:
```text
/sdcard/Android/data/com.perseusxr.research/files/
```
Copy the timestamped session folder (e.g., `20260222_143000`) to your local PC.

### Step 2: 1-Click Reconstruction
Run the master processing script against your saved extraction folder:

```bash
python perseusxr_process.py /path/to/your/transferred/session/folder
```

This single command seamlessly parses the raw YUV streams, synchronizes the depth maps, applies advanced tone-mapping, and automatically generates a fully pre-triangulated **COLMAP** sparse model. You can directly drag this output structure into cutting-edge 3DGS trainers like Postshot, Luma, or Polycam!

---

## 🙏 Acknowledgements & Heritage

This project was forged on the shoulders of giants. The core sensor interception pipeline within `PerseusXR Research` acts as an advanced, highly specialized architectural fork of the phenomenal open-source tools:

- **[OpenQuestCapture](https://github.com/samuelm2/OpenQuestCapture)** engineered by `samuelm2`.
- **[QuestRealityCapture](https://github.com/t-34400/QuestRealityCapture)** engineered by `t-34400`.

Without their trailblazing research into unlocking the Meta Quest 3's raw Passthrough and Depth APIs, this application would not exist. Please investigate and support the original authors!

## 📜 License
(See LICENSE file for detailed information regarding modification and distribution rights.)
