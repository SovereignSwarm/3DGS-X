# nullable enable

using System;
using UnityEngine;

namespace PerseusXR.UI
{
    /// <summary>
    /// The 3-Pass Guided Capture state machine exclusively architected for PerseusXR Research.
    /// It orchestrates the flow from Geometry -> Details -> Reflections, guaranteeing
    /// "Professional Grade" PerseusXR captures without needing extensive training.
    /// </summary>
    public enum CapturePass
    {
        Geometry,      // Broad room structure
        TextureDetail, // Close-up surfaces (< 1.5m)
        ViewDependent  // Orbital/specular reflections (> 30 deg view delta)
    }

    /// <summary>
    /// Orchestrates the 3-pass guided scanning pattern, feeding state changes
    /// to the spatial heatmaps and Viewfinder HUD.
    /// </summary>
    public class GuidedCaptureManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecordingManager recordingManager = default!;
        [Tooltip("The time (seconds) to wait before allowing manual progression to the next pass")]
        [SerializeField] private float passCooldown = 3.0f;

        public event Action<CapturePass>? OnPassChanged;

        private CapturePass currentPass = CapturePass.Geometry;
        private float lastPassChangeTime = 0f;

        public CapturePass CurrentPass => currentPass;

        private void Start()
        {
            if (recordingManager != null)
            {
                recordingManager.OnRecordingStarted.AddListener(ResetPasses);
            }
        }

        private void Update()
        {
            if (recordingManager == null || !recordingManager.IsRecording) return;

            // Allow the user to manually advance passes using the B button (Right Controller)
            // In a full commercial app, this would be heavily automated by MRUK completion %
            if (OVRInput.GetDown(OVRInput.Button.Two) && Time.time - lastPassChangeTime > passCooldown)
            {
                AdvancePass();
            }
        }

        private void ResetPasses()
        {
            currentPass = CapturePass.Geometry;
            lastPassChangeTime = Time.time;
            OnPassChanged?.Invoke(currentPass);
            Debug.Log($"[GuidedCapture] Started Pass 1: {currentPass} - Mapping broad structural bounds.");
        }

        public void AdvancePass()
        {
            switch (currentPass)
            {
                case CapturePass.Geometry:
                    currentPass = CapturePass.TextureDetail;
                    Debug.Log($"[GuidedCapture] Advanced to Pass 2: {currentPass} - Capturing close-up details.");
                    // Provide a satisfying controller rumble to signal the shift
                    OVRInput.SetControllerVibration(1.0f, 0.5f, OVRInput.Controller.RTouch);
                    Invoke(nameof(StopVibration), 0.2f);
                    break;
                case CapturePass.TextureDetail:
                    currentPass = CapturePass.ViewDependent;
                    Debug.Log($"[GuidedCapture] Advanced to Pass 3: {currentPass} - Orbiting for specular highlights.");
                    OVRInput.SetControllerVibration(1.0f, 0.5f, OVRInput.Controller.RTouch);
                    Invoke(nameof(StopVibration), 0.2f);
                    break;
                case CapturePass.ViewDependent:
                    Debug.Log($"[GuidedCapture] All passes complete! The user should ideally stop recording now.");
                    // Double pulse to signal completion
                    OVRInput.SetControllerVibration(1.0f, 1.0f, OVRInput.Controller.RTouch);
                    Invoke(nameof(StopVibration), 0.5f);
                    break;
            }

            lastPassChangeTime = Time.time;
            OnPassChanged?.Invoke(currentPass);
        }

        private void StopVibration()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }

        /// <summary>
        /// Returns the Hyperscape-inspired instruction string for the current pass.
        /// </summary>
        public string GetPassInstruction()
        {
            return currentPass switch
            {
                CapturePass.Geometry => "Pass 1/3: Paint broad strokes.",
                CapturePass.TextureDetail => "Pass 2/3: Get close (<1.5m).",
                CapturePass.ViewDependent => "Pass 3/3: Orbit surfaces.",
                _ => "Capture Active"
            };
        }
    }
}
