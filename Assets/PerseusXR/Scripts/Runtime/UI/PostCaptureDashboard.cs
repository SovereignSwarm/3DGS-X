# nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PerseusXR.UI
{
    /// <summary>
    /// A persistent, Hyperscape-inspired dashboard that appears after a capture completes.
    /// It provides vital session statistics and explicit instructions for the offline PerseusXR 
    /// python processing phase. Forces the user to deliberately acknowledge the completion.
    /// </summary>
    public class PostCaptureDashboard : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("The main dashboard panel GameObject")]
        [SerializeField] private GameObject dashboardPanel = default!;
        
        [Tooltip("Text to display the saved directory path")]
        [SerializeField] private TextMeshProUGUI directoryPathText = default!;
        
        [Tooltip("Text to display the required Python command")]
        [SerializeField] private TextMeshProUGUI pythonCommandText = default!;
        
        [Tooltip("Text to display capture statistics (Duration, Blur warnings)")]
        [SerializeField] private TextMeshProUGUI statisticsText = default!;

        [Tooltip("Button the user must click to dismiss the dashboard")]
        [SerializeField] private Button acknowledgeButton = default!;

        [Header("References")]
        [SerializeField] private RecordingManager recordingManager = default!;
        [SerializeField] private ViewfinderHUD viewfinderHUD = default!; // Optional: to get blur stats

        // Track statistics during the recording
        private int blurWarningsTriggered = 0;
        private float sessionDuration = 0f;
        private bool wasRecording = false;
        private float stopHapticTime = -1f;

        private void Start()
        {
            if (dashboardPanel != null)
            {
                dashboardPanel.SetActive(false);
            }

            if (acknowledgeButton != null)
            {
                acknowledgeButton.onClick.AddListener(DismissDashboard);
            }

            if (recordingManager != null)
            {
                recordingManager.OnRecordingSaved.AddListener(ShowDashboard);
            }
        }

        private void Update()
        {
            if (stopHapticTime > 0 && Time.time >= stopHapticTime)
            {
                StopVibration();
            }

            if (recordingManager == null) return;

            bool isCurrentlyRecording = recordingManager.IsRecording;

            if (isCurrentlyRecording)
            {
                sessionDuration = recordingManager.RecordingDuration;
                
                // If we have a viewfinder, we could theoretically hook into a "blur warning fired" event.
                // For simplicity in this script, we just assume that logic is handled or we hook a public property later.
            }

            wasRecording = isCurrentlyRecording;
        }

        private void OnDestroy()
        {
            if (acknowledgeButton != null)
            {
                acknowledgeButton.onClick.RemoveListener(DismissDashboard);
            }
            if (recordingManager != null)
            {
                recordingManager.OnRecordingSaved.RemoveListener(ShowDashboard);
            }
        }

        /// <summary>
        /// Triggered when the RecordingManager finishes saving files to disk.
        /// </summary>
        /// <param name="directoryName">The folder name where data was saved.</param>
        public void ShowDashboard(string directoryName)
        {
            if (dashboardPanel != null)
            {
                dashboardPanel.SetActive(true);
            }

            // Provide a satisfying success rumble (PerseusXR Micro-click)
            TriggerVibration(0.3f);
            stopHapticTime = Time.time + 0.05f;

            if (directoryPathText != null)
            {
                directoryPathText.text = $"Saved to Quest: PerseusXR/Recordings/{directoryName}";
            }

            if (pythonCommandText != null)
            {
                // Explicit PerseusXR pipeline instructions (Silver/Amber Branding)
                pythonCommandText.text = $"PC Processing Command:\n<color=#FF8C00>python perseusxr_process.py {directoryName}</color>";
            }

            if (statisticsText != null)
            {
                int mins = Mathf.FloorToInt(sessionDuration / 60);
                int secs = Mathf.FloorToInt(sessionDuration % 60);
                
                // Determine quality based on arbitrary blur warning thresholds for demonstration
                // PerseusXR Cinematic Virtual Production Palette
                string qualityTier = blurWarningsTriggered > 5 ? "<color=#FF8C00>Sub-Optimal (Fast Movement)</color>" : "<color=#FFFFFF>Professional Grade</color>";
                
                statisticsText.text = $"Capture Time: {mins:D2}:{secs:D2}\nQuality Rating: {qualityTier}";
            }

            // Reset stats for next session
            blurWarningsTriggered = 0;
            sessionDuration = 0f;
        }

        public void DismissDashboard()
        {
            if (dashboardPanel != null)
            {
                dashboardPanel.SetActive(false);
            }
            // Add a small click haptic (PerseusXR Micro-click)
            TriggerVibration(0.1f);
            stopHapticTime = Time.time + 0.05f;
        }

        private void TriggerVibration(float amplitude)
        {
            OVRInput.SetControllerVibration(1.0f, amplitude, OVRInput.Controller.RTouch);
        }

        private void StopVibration()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            stopHapticTime = -1f;
        }

        /// <summary>
        /// Called externally (e.g., by CaptureFeedbackLoop or ViewfinderHUD) when the user triggers a "Slow Down" warning.
        /// </summary>
        public void RegisterBlurWarning()
        {
            blurWarningsTriggered++;
        }
    }
}
