# nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityLog.Common;

namespace RealityLog.UI
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
                deleteButton.onClick.AddListener(() => OnDeleteClicked?.Invoke(recordingDirectoryName));

            if (exportButton != null)
                exportButton.onClick.AddListener(() => OnExportClicked?.Invoke(recordingDirectoryName));
        }

        /// <summary>
        /// Sets the recording information to display.
        /// </summary>
        public void SetRecording(RecordingListManager.RecordingInfo info)
        {
            recordingInfo = info;
            recordingDirectoryName = info.DirectoryName;

            if (directoryNameText != null)
                directoryNameText.text = info.DirectoryName;

            if (dateText != null)
                dateText.text = info.FormattedDate;

            if (sizeText != null)
                sizeText.text = info.FormattedSize;
        }

        /// <summary>
        /// Gets the directory name of this recording.
        /// </summary>
        public string GetDirectoryName() => recordingDirectoryName;
    }
}

