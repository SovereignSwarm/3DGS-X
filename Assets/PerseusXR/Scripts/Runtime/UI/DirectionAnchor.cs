# nullable enable

using UnityEngine;

namespace PerseusXR.UI
{
    /// <summary>
    /// For Pass 3: View-Dependent Light Scanning.
    /// Drops an anchor on a surface and tracks the user's viewing angle.
    /// Turns gold when the user orbits and looks at it from a sufficiently different angle (>30 deg).
    /// </summary>
    public class DirectionAnchor : MonoBehaviour
    {
        [Tooltip("The initial forward vector of the camera when this anchor was created.")]
        public Vector3 InitialViewDirection { get; set; }
        
        [Tooltip("Color of the anchor before orbital capture is achieved.")]
        public Color IncompleteColor = new Color(1f, 1f, 1f, 0.3f);
        
        [Tooltip("Color of the anchor once orbital capture is achieved.")]
        public Color CompleteColor = new Color(0f, 0.8f, 1f, 1f); // Glowing Cyan
        
        [Tooltip("The angle delta required to consider the orbital view captured.")]
        public float RequiredOrbitAngle = 30f;

        public bool IsComplete { get; private set; }

        private Renderer? anchorRenderer;
        private Transform? mainCamera;

        private void Start()
        {
            anchorRenderer = GetComponent<Renderer>();
            
            // Re-use OVRCameraRig's center eye anchor if possible, fallback to main
            var rig = FindObjectOfType<OVRCameraRig>();
            mainCamera = rig != null ? rig.centerEyeAnchor : UnityEngine.Camera.main?.transform;

            SetColor(IncompleteColor);
        }

        private void Update()
        {
            if (IsComplete || mainCamera == null) return;

            // Check the current viewing angle against the initial one
            Vector3 currentViewDirection = (transform.position - mainCamera.position).normalized;
            float angleDelta = Vector3.Angle(InitialViewDirection, currentViewDirection);

            // If the user has orbited enough and is still looking roughly AT the anchor
            if (angleDelta >= RequiredOrbitAngle)
            {
                // Verify the user is actually looking at it, not just standing somewhere else looking away
                float gazeAngle = Vector3.Angle(mainCamera.forward, currentViewDirection);
                if (gazeAngle < 15f) // The anchor is in the center of their view
                {
                    MarkComplete();
                }
            }
        }

        private void MarkComplete()
        {
            IsComplete = true;
            SetColor(CompleteColor);

            // Trigger a satisfying haptic buzz to let the user know they got the specular angle
            OVRInput.SetControllerVibration(1.0f, 0.8f, OVRInput.Controller.RTouch);
            Invoke(nameof(StopVibration), 0.1f);
        }

        private void StopVibration()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }

        private void SetColor(Color color)
        {
            if (anchorRenderer != null)
            {
                anchorRenderer.material.color = color;
            }
        }
    }
}
