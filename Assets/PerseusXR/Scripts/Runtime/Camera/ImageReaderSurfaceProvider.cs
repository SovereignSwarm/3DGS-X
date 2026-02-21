# nullable enable

using System;
using System.IO;
using UnityEngine;
using PerseusXR.Common;

namespace PerseusXR.Camera
{
    public class ImageReaderSurfaceProvider : SurfaceProviderBase
    {
        private const string IMAGE_READER_SURFACE_PROVIDER_CLASS_NAME = "com.perseusxr.camera.io.ImageReaderSurfaceProvider";
        
        private const string RESET_BASE_TIME_METHOD_NAME = "resetBaseTime";
        private const string UPDATE_DIRECTORY_PATHS_METHOD_NAME = "updateDirectoryPaths";
        private const string CAPTURE_NEXT_FRAME_METHOD_NAME = "captureNextFrame";
        private const string CLOSE_METHOD_NAME = "close";

        [SerializeField] private string dataDirectoryName = string.Empty;
        [SerializeField] private string imageSubdirName = "left_camera";
        [SerializeField] private string formatInfoFileName = "left_camera_image_format.json";
        [SerializeField] private int bufferPoolSize = 5;
        [Header("Synchronized Capture")]
        [Tooltip("Required: Reference to CaptureTimer for FPS-based capture timing.")]
        [SerializeField] private CaptureTimer captureTimer = default!;

        private AndroidJavaObject? currentInstance;
        private CameraMetadata? cameraMetadata;

        // Properties have been exposed to allow external controllers to inject dynamic names (e.g. left_eye, right_eye)
        public string DataDirectoryName { get => dataDirectoryName; set => dataDirectoryName = value; }
        public string ImageSubdirName { get => imageSubdirName; set => imageSubdirName = value; }
        public string FormatInfoFileName { get => formatInfoFileName; set => formatInfoFileName = value; }
        
        // Base absolute path injected by session controller
        public string BaseSessionPath { get; set; } = string.Empty;

        public override AndroidJavaObject? GetJavaInstance(CameraMetadata metadata)
        {
            Close();

            // Store metadata for writing when recording starts (via external controller)
            // Don't write here because dataDirectoryName might be empty/default at app startup
            cameraMetadata = metadata;

            // Fallback to absolute persistent path if controller hasn't injected yet, though controller should manage paths.
            var basePath = string.IsNullOrEmpty(BaseSessionPath) ? Application.persistentDataPath : BaseSessionPath;
            var dataDirPath = Path.Join(basePath, dataDirectoryName);

            var imageFileDirPath = Path.Join(dataDirPath, imageSubdirName);
            var formatInfoFilePath = Path.Join(dataDirPath, formatInfoFileName);

            var size = metadata.sensor.pixelArraySize;

            currentInstance = new AndroidJavaObject(
                IMAGE_READER_SURFACE_PROVIDER_CLASS_NAME,
                size.width,
                size.height,
                imageFileDirPath,
                formatInfoFilePath,
                bufferPoolSize
            );

            Debug.Log($"[ImageReaderSurfaceProvider] Camera initialized -- will respond to capture signals from CaptureTimer");

            return currentInstance;
        }

        /// <summary>
        /// Resets the base time for camera timestamps.
        /// Should be called when recording starts to sync with depth timestamps.
        /// </summary>
        public void ResetBaseTime()
        {
            if (currentInstance != null)
            {
                currentInstance.Call(RESET_BASE_TIME_METHOD_NAME);
                Debug.Log($"[ImageReaderSurfaceProvider] Reset camera base time");
            }
        }

        /// <summary>
        /// Updates the directory paths for a new recording session.
        /// Must be called before starting capture to ensure files are written to the correct location.
        /// Controller injects exact strings to avoid Unity component hardcoding.
        /// </summary>
        public void UpdateDirectoryPaths()
        {
            if (currentInstance != null)
            {
                var basePath = string.IsNullOrEmpty(BaseSessionPath) ? Application.persistentDataPath : BaseSessionPath;
                var dataDirPath = Path.Join(basePath, dataDirectoryName);

                var imageFileDirPath = Path.Join(dataDirPath, imageSubdirName);
                var formatInfoFilePath = Path.Join(dataDirPath, formatInfoFileName);
                
                currentInstance.Call(UPDATE_DIRECTORY_PATHS_METHOD_NAME, imageFileDirPath, formatInfoFilePath);
                Debug.Log($"[ImageReaderSurfaceProvider] Updated directory paths for session: {dataDirPath}");
            }
        }

        private void LateUpdate()
        {
            // Use LateUpdate to ensure CaptureTimer's Update() has run first
            // This guarantees we see the latest ShouldCaptureThisFrame value
            if (currentInstance == null || captureTimer == null)
                return;

            // Signal Java to capture next frame when timer says so
            // This ensures camera and depth are triggered at the exact same Unity frame
            if (captureTimer.IsCapturing && captureTimer.ShouldCaptureThisFrame)
            {
#if UNITY_EDITOR
                Debug.Log($"[ImageReader] Signaling camera capture at Unity time={Time.unscaledTime:F3}s");
#endif
                currentInstance.Call(CAPTURE_NEXT_FRAME_METHOD_NAME);
            }
        }

        private void OnDestroy()
        {
            Close();
        }

        private void Close()
        {
            currentInstance?.Call(CLOSE_METHOD_NAME);
            currentInstance?.Dispose();
            currentInstance = null;
        }
    }
}