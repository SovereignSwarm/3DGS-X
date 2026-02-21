# nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PerseusXR.Common;

namespace PerseusXR.UI
{
    /// <summary>
    /// UI component representing a single recording in the list.
    /// </summary>
    public class RecordingListItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Text component displaying the recording directory name")]
        [SerializeField] private TextMeshProUGUI directoryNameText = default!;

        [Tooltip("Text component displaying the recording date/time")]
        [SerializeField] private TextMeshProUGUI dateText = default!;

        [Tooltip("Text component displaying the recording size")]
        [SerializeField] private TextMeshProUGUI sizeText = default!;

        [Header("Buttons")]
        [Tooltip("Button to delete this recording")]
        [SerializeField] private Button deleteButton = default!;

        [Tooltip("Button to export this recording (compress and move to Downloads)")]
        [SerializeField] private Button exportButton = default!;

        private string recordingDirectoryName = string.Empty;
        private RecordingListManager.RecordingInfo? recordingInfo = null;

        /// <summary>
        /// Event fired when delete button is clicked. Passes directory name.
        /// </summary>
        public event Action<string>? OnDeleteClicked;

        /// <summary>
        /// Event fired when export button is clicked. Passes directory name.
        /// </summary>
        public event Action<string>? OnExportClicked;

        private void Awake()
        {
            if (deleteButton != null)
                deleteButton.onClick.AddListener(() => { PlayMicroClick(); OnDeleteClicked?.Invoke(recordingDirectoryName); });

            if (exportButton != null)
                exportButton.onClick.AddListener(() => { PlayMicroClick(); OnExportClicked?.Invoke(recordingDirectoryName); });
        }

        private void PlayMicroClick()
        {
            OVRInput.SetControllerVibration(1.0f, 0.2f, OVRInput.Controller.RTouch);
            Invoke(nameof(StopVibration), 0.05f);
        }

        private void StopVibration()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }

        /// <summary>
        /// Sets the recording information to display.
        /// </summary>
        public void SetRecording(RecordingListManager.RecordingInfo info)
        {
            recordingInfo = info;
            recordingDirectoryName = info.DirectoryName;

            if (directoryNameText != null)
            {
                directoryNameText.text = info.DirectoryName;
                directoryNameText.color = Color.white; // PerseusXR Cinematic Silver
            }

            if (dateText != null)
            {
                dateText.text = info.FormattedDate;
                dateText.color = Color.white;
            }

            if (sizeText != null)
            {
                sizeText.text = info.FormattedSize;
                sizeText.color = Color.white;
            }
        }

        /// <summary>
        /// Gets the directory name of this recording.
        /// </summary>
        public string GetDirectoryName() => recordingDirectoryName;
    }
}

