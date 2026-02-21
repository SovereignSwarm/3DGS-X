# nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PerseusXR;
using PerseusXR.Common;

namespace PerseusXR.UI
{
    /// <summary>
    /// UI component that displays a recording icon and timer when recording is active.
    /// </summary>
    public class RecordingIndicator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the RecordingManager to track recording state")]
        [SerializeField] private RecordingManager recordingManager = default!;
        
        [Header("UI Elements")]
        [Tooltip("The recording icon Image component (will be shown/hidden)")]
        [SerializeField] private Image recordingIcon = default!;
        
        [Tooltip("Text component to display the recording timer (MM:SS format)")]
        [SerializeField] private TextMeshProUGUI timerText = default!;

        [Header("Settings")]
        [Tooltip("Update interval for the timer display in seconds")]
        [SerializeField] private float updateInterval = 0.1f;

        private float lastUpdateTime = 0f;

        private void Update()
        {
            if (recordingManager == null)
                return;

            bool shouldShow = recordingManager.IsRecording;
            
            // Show/hide the recording icon
            if (recordingIcon != null)
            {
                recordingIcon.gameObject.SetActive(shouldShow);
            }

            // Update timer text
            if (timerText != null)
            {
                if (shouldShow)
                {
                    // Update timer at specified interval for performance
                    if (Time.time - lastUpdateTime >= updateInterval)
                    {
                        float duration = recordingManager.RecordingDuration;
                        timerText.text = FormatTime(duration);
                        lastUpdateTime = Time.time;
                    }
                }
                else
                {
                    timerText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// Formats time in seconds to MM:SS format.
        /// </summary>
        private string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes:D2}:{secs:D2}";
        }

        private void OnValidate()
        {
            if (recordingManager == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingIndicator: Missing RecordingManager reference!");
            
            if (recordingIcon == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingIndicator: Missing recording icon Image reference!");
            
            if (timerText == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingIndicator: Missing timer TextMeshProUGUI reference!");
        }
    }
}

