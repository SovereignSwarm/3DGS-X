# nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PerseusXR.UI
{
    /// <summary>
    /// A Hyperscape-inspired minimalist HUD that lazy-follows the user's peripheral vision.
    /// Provides critical 3DGS capture metrics (motion blur warnings, time) without breaking immersion.
    /// </summary>
    public class ViewfinderHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The main camera (center eye anchor) to follow")]
        [SerializeField] private Transform centerEyeAnchor = default!;
        [SerializeField] private RecordingManager recordingManager = default!;
        [SerializeField] private GuidedCaptureManager guidedManager = default!;

        [Header("UI Elements")]
        [Tooltip("Canvas group to fade in/out the HUD")]
        [SerializeField] private CanvasGroup hudGroup = default!;
        [SerializeField] private TextMeshProUGUI timerText = default!;
        [SerializeField] private Image motionWarningIndicator = default!;
        [SerializeField] private TextMeshProUGUI motionWarningText = default!;
        [Tooltip("Text element to display current guided pass instructions")]
        [SerializeField] private TextMeshProUGUI instructionText = default!;

        [Header("Follow Settings")]
        [Tooltip("Distance in front of the camera (meters)")]
        [SerializeField] private float distance = 1.0f;
        [Tooltip("Vertical offset from center (negative = lower peripheral)")]
        [SerializeField] private float heightOffset = -0.3f;
        [Tooltip("How smoothly the HUD catches up to the head (higher = slower damping)")]
        [SerializeField] private float followSpeed = 4.0f;
        [Tooltip("Degrees of head rotation before the HUD starts following")]
        [SerializeField] private float deadzoneAngle = 10f;

        [Header("Blur Warning Settings")]
        [Tooltip("Angular velocity threshold (deg/sec) before warning the user to slow down")]
        [SerializeField] private float maxAngularVelocity = 45f;
        
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        
        // Kinematic Dampening References
        private Vector3 velocity = Vector3.zero;
        
        private Vector3 lastHeadForward;
        private float lastUpdateTime;

        private void Start()
        {
            if (hudGroup != null) hudGroup.alpha = 0f;
            lastHeadForward = centerEyeAnchor.forward;
        }

        private void Update()
        {
            if (recordingManager == null || centerEyeAnchor == null) return;

            bool isRecording = recordingManager.IsRecording;
            
            // Fade logic
            if (hudGroup != null)
            {
                hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, isRecording ? 1f : 0f, Time.deltaTime * 3f);
            }

            if (!isRecording && hudGroup != null && hudGroup.alpha <= 0.01f)
            {
                return;
            }

            UpdateHUDMetrics();
            UpdateLazyFollow();
        }

        private void UpdateHUDMetrics()
        {
            // Update Timer
            float duration = recordingManager.RecordingDuration;
            int mins = Mathf.FloorToInt(duration / 60);
            int secs = Mathf.FloorToInt(duration % 60);
            if (timerText != null) timerText.text = $"{mins:D2}:{secs:D2}";

            // Update Current Pass Instruction
            if (instructionText != null && guidedManager != null)
            {
                instructionText.text = guidedManager.GetPassInstruction();
            }

            // Calculate angular velocity for Blur Warning
            float angleDelta = Vector3.Angle(lastHeadForward, centerEyeAnchor.forward);
            float dt = Time.time - lastUpdateTime;
            
            if (dt > 0)
            {
                float angularVelocity = angleDelta / dt;
                
                bool isMovingTooFast = angularVelocity > maxAngularVelocity;
                
                if (motionWarningIndicator != null)
                {
                    // PerseusXR Cinematic Virtual Production Palette (Amber / Translucent White)
                    motionWarningIndicator.color = isMovingTooFast ? new Color(1f, 0.55f, 0f, 0.8f) : new Color(1f, 1f, 1f, 0.2f);
                }
                
                if (motionWarningText != null)
                {
                    motionWarningText.text = isMovingTooFast ? "SLOW DOWN" : "STABLE";
                    motionWarningText.color = isMovingTooFast ? new Color(1f, 0.55f, 0f, 1f) : Color.white;
                }
            }

            lastHeadForward = centerEyeAnchor.forward;
            lastUpdateTime = Time.time;
        }

        private void UpdateLazyFollow()
        {
            // Calculate where the HUD *should* be based on current head look
            Vector3 idealPosition = centerEyeAnchor.position + (centerEyeAnchor.forward * distance);
            // Drop it into the peripheral vision
            idealPosition += Vector3.up * heightOffset;
            
            // Only update the target if the user has turned their head past the deadzone
            float angleToIdeal = Vector3.Angle(centerEyeAnchor.forward, transform.position - centerEyeAnchor.position);
            
            // Check translational detachment (walking away without rotating head)
            float distanceToIdeal = Vector3.Distance(transform.position, idealPosition);
            bool isSpatiallyDetached = distanceToIdeal > 0.5f; // 0.5 meters of physical drift tolerance
            
            if (angleToIdeal > deadzoneAngle || isSpatiallyDetached || targetPosition == Vector3.zero)
            {
                targetPosition = idealPosition;
                
                // Keep the HUD upright, pointing at the camera
                Vector3 directionToCamera = centerEyeAnchor.position - targetPosition;
                directionToCamera.y = 0; // Lock pitch/roll, only yaw
                if (directionToCamera != Vector3.zero)
                {
                    targetRotation = Quaternion.LookRotation(-directionToCamera, Vector3.up);
                }
            }

            // PerseusXR Kinematic Optimization: Replace linear Lerp with Critically Damped Spring logic
            float smoothTime = 1f / followSpeed;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
        }
    }
}
