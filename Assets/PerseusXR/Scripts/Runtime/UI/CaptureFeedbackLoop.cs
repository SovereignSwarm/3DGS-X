# nullable enable

using UnityEngine;
using Meta.XR.MRUtilityKit;

namespace PerseusXR.UI
{
    /// <summary>
    /// Provides multisensory feedback (Audio + Haptics) to gamify and encourage
    /// high-quality PerseusXR capture patterns based on Hyperscape principles.
    /// </summary>
    public class CaptureFeedbackLoop : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecordingManager recordingManager = default!;
        [SerializeField] private PostCaptureDashboard postCaptureDashboard = default!;
        [Tooltip("Audio source for playing capture success 'ticks'")]
        [SerializeField] private AudioSource successAudioSource = default!;
        [Tooltip("Audio source for playing motion blur 'warnings'")]
        [SerializeField] private AudioSource warningAudioSource = default!;

        [Header("Haptics")]
        [Tooltip("Controller to vibrate during capture")]
        [SerializeField] private OVRInput.Controller hapticController = OVRInput.Controller.RTouch;
        [Tooltip("Vibration amplitude for successful capture (tick)")]
        [SerializeField] private float successAmplitude = 0.2f;
        [Tooltip("Vibration amplitude for blur warning")]
        [SerializeField] private float warningAmplitude = 0.8f;

        [Header("Metrics")]
        [Tooltip("Angular velocity threshold (deg/sec) before playing warning feedback")]
        [SerializeField] private float maxAngularVelocity = 45f;
        [Tooltip("Delay between success ticks (seconds) to prevent audio spam")]
        [SerializeField] private float tickInterval = 0.5f;

        private Transform? centerEyeAnchor;
        private Vector3 lastHeadForward;
        private float lastUpdateTime;
        private float lastTickTime;
        private float lastWarningTime;

        private void Start()
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                centerEyeAnchor = rig.centerEyeAnchor;
                lastHeadForward = centerEyeAnchor.forward;
            }
        }

        private void Update()
        {
            if (recordingManager == null || centerEyeAnchor == null || !recordingManager.IsRecording) 
            {
                return;
            }

            CheckMotionWarning();
            CheckSpatialScanning();
        }

        private void CheckMotionWarning()
        {
            float angleDelta = Vector3.Angle(lastHeadForward, centerEyeAnchor.forward);
            float dt = Time.time - lastUpdateTime;
            
            if (dt > 0)
            {
                float angularVelocity = angleDelta / dt;
                
                // If the user whips their head too fast, play a warning sound + heavy rumble
                if (angularVelocity > maxAngularVelocity)
                {
                    if (warningAudioSource != null && !warningAudioSource.isPlaying)
                    {
                        warningAudioSource.Play();
                    }

                    if (postCaptureDashboard != null && Time.time - lastWarningTime > 1.0f)
                    {
                        postCaptureDashboard.RegisterBlurWarning();
                        lastWarningTime = Time.time;
                    }
                    
                    OVRInput.SetControllerVibration(1.0f, warningAmplitude, hapticController);
                }
                else
                {
                    // Turn off warning rumble
                    OVRInput.SetControllerVibration(0, 0, hapticController);
                }
            }

            lastHeadForward = centerEyeAnchor.forward;
            lastUpdateTime = Time.time;
        }

        private void CheckSpatialScanning()
        {
            // Simulate playing a 'success tick' if the user is steadily exploring new space.
            // In a full implementation, this would be hooked to an event from SpatialHeatmapVisualizer
            // whenever a NEW voxel is painted green.
            
            if (Time.time - lastTickTime > tickInterval)
            {
                // Play a satisfying, subtle 'bubble' or 'tick' sound to gamify the capture
                if (successAudioSource != null)
                {
                    successAudioSource.Play();
                }
                
                // Add a very subtle, satisfying micro-haptic "click"
                OVRInput.SetControllerVibration(1.0f, successAmplitude, hapticController);
                
                // Turn it right off to make it a discrete "click" rather than a buzz
                Invoke(nameof(StopSuccessHaptic), 0.05f);
                
                lastTickTime = Time.time;
            }
        }

        private void StopSuccessHaptic()
        {
            OVRInput.SetControllerVibration(0, 0, hapticController);
        }
    }
}
