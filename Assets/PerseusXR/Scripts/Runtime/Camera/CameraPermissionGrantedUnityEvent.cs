# nullable enable

using UnityEngine;
using UnityEngine.Events;

namespace PerseusXR.Camera
{
    class CameraPermissionGrantedUnityEvent : MonoBehaviour
    {
        [SerializeField] private CameraPermissionManager cameraPermissionManager = default!;
        [SerializeField] private UnityEvent permissionGranted = default!;

        private void Start()
        {
            var cmInstance = cameraPermissionManager.CameraManagerJavaInstance;
            if (cmInstance != null)
            {
                permissionGranted.Invoke();
                return;
            }

            cameraPermissionManager.CameraManagerInstantiated += OnCameraManagerInstantiated;
        }

        private void OnDestroy()
        {            
            cameraPermissionManager.CameraManagerInstantiated -= OnCameraManagerInstantiated;
        }

        private void OnCameraManagerInstantiated(AndroidJavaObject _)
        {
            permissionGranted.Invoke();
        }
    }
}