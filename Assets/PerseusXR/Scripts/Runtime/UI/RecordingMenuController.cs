# nullable enable

using UnityEngine;
using PerseusXR.Common;
using OVR;

namespace PerseusXR.UI
{
    /// <summary>
    /// Controls the recording menu visibility with Y button input.
    /// </summary>
    public class RecordingMenuController : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("The recording menu panel GameObject (should have Canvas with OVROverlayCanvas)")]
        [SerializeField] private GameObject menuPanel = default!;

        [Tooltip("WorldSpaceMenuPositioner component to position menu in front of player when opened")]
        [SerializeField] private WorldSpaceMenuPositioner? menuPositioner = default!;

        [Header("Input Settings")]
        [Tooltip("Button to toggle menu (Y button on Quest controllers)")]
        [SerializeField] private OVRInput.Button toggleButton = OVRInput.Button.Four;

        [Tooltip("Controller to check for button input")]
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.All;

        private bool isMenuVisible = false;

        private CanvasGroup? menuCanvasGroup;
        private RecordingListManager? listManager;

        private void Start()
        {
            if (menuPanel != null)
            {
                // Ensure we have a CanvasGroup
                menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();

                // Ensure the GameObject is active so Oculus tracker works
                menuPanel.SetActive(true);
                
                // Initialize state (hidden)
                UpdateMenuVisibility();
            }

            listManager = GetComponent<RecordingListManager>();
        }

        private void Update()
        {
            if (OVRInput.GetDown(toggleButton, controller))
            {
                ToggleMenu();
            }
        }

        /// <summary>
        /// Toggles the recording menu visibility.
        /// </summary>
        public void ToggleMenu()
        {
            // PerseusXR Spatial UX: Inject physical weight into the UI interaction.
            PlayMicroClickHaptic();

            isMenuVisible = !isMenuVisible;
            
            if (menuPanel != null)
            {
                if (listManager)
                {
                    listManager.RefreshRecordings();
                }
                UpdateMenuVisibility();
                
                if (isMenuVisible)
                {
                    Debug.Log($"[{Constants.LOG_TAG}] RecordingMenuController: Menu opened");
                    
                    // Position menu in front of player when opening
                    if (menuPositioner != null)
                    {
                        menuPositioner.PositionInFront();
                    }
                    else
                    {
                        Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingMenuController: Menu opened but menuPositioner is null!");
                    }
                }
                else
                {
                    Debug.Log($"[{Constants.LOG_TAG}] RecordingMenuController: Menu closed");
                    if (menuPositioner != null)
                    {
                        menuPositioner.PositionAway();
                    }
                }
            }
        }

        private void UpdateMenuVisibility()
        {
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = isMenuVisible ? 1f : 0f;
                menuCanvasGroup.interactable = isMenuVisible;
                menuCanvasGroup.blocksRaycasts = isMenuVisible;
            }
        }

        /// <summary>
        /// Shows the recording menu.
        /// </summary>
        public void ShowMenu()
        {
            if (!isMenuVisible)
            {
                ToggleMenu();
            }
        }

        /// <summary>
        /// Hides the recording menu.
        /// </summary>
        public void HideMenu()
        {
            if (isMenuVisible)
            {
                ToggleMenu();
            }
        }

        // --- PerseusXR Haptic Architecture ---
        private void PlayMicroClickHaptic()
        {
            // Meta OS Standard: High frequency, critically low amplitude for premium feel (Click, not a buzz)
            OVRInput.SetControllerVibration(1.0f, 0.2f, OVRInput.Controller.RTouch);
            Invoke(nameof(StopHaptic), 0.05f); // 50ms pulse
        }

        private void StopHaptic()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }

        private void OnValidate()
        {
            if (menuPanel == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingMenuController: Missing menu panel GameObject reference!");
            if (menuPositioner == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingMenuController: Missing menu positioner reference!");
        }
    }
}
