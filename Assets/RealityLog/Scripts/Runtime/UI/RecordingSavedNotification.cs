# nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityLog;
using RealityLog.Common;

namespace RealityLog.UI
{
    /// <summary>
    /// UI component that displays a notification when a recording is saved.
    /// Shows "Saved to [directory]" message.
    /// </summary>
    public class RecordingSavedNotification : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("The notification panel/container GameObject")]
        [SerializeField] private GameObject notificationPanel = default!;
        
        [Tooltip("Text component to display the 'Saved to...' message")]
        [SerializeField] private TextMeshProUGUI messageText = default!;

        [Header("Settings")]
        [Tooltip("How long to show the notification in seconds")]
        [SerializeField] private float displayDuration = 3f;

        [Tooltip("Base message text (directory will be appended)")]
        [SerializeField] private string baseMessage = "Saved to ";

        private Coroutine? hideCoroutine = null;

        private void Start()
        {
            // Hide notification panel initially
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Stop any running coroutine
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }
        }

        /// <summary>
        /// Called when recording is saved. Can be connected to RecordingManager's onRecordingSaved UnityEvent in the inspector.
        /// </summary>
        public void OnRecordingSaved(string directory)
        {
            if (messageText != null)
            {
                messageText.text = $"{baseMessage}{directory}";
            }

            if (notificationPanel != null)
            {
                notificationPanel.SetActive(true);
            }

            // Stop any existing hide coroutine
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

            // Start new hide coroutine
            hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private System.Collections.IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
            
            hideCoroutine = null;
        }

        private void OnValidate()
        {
            if (notificationPanel == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingSavedNotification: Missing notification panel GameObject reference!");
            
            if (messageText == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingSavedNotification: Missing message TextMeshProUGUI reference!");
        }
    }
}

