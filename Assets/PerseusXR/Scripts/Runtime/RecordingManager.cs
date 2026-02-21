# nullable enable

using System;
using UnityEngine;
using UnityEngine.Events;
using PerseusXR.Camera;
using PerseusXR.Common;
using PerseusXR.Depth;
using PerseusXR.OVR;

namespace PerseusXR
{
    /// <summary>
    /// Formal state machine for recording lifecycle (A-2 fix).
    /// </summary>
    public enum RecordingState
    {
        Idle,
        Recording,
        Finalizing
    }

    /// <summary>
    /// Central coordinator for all recording subsystems.
    /// Handles proper sequencing and lifecycle management of depth, camera, and pose recording.
    /// </summary>
    public class RecordingManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Recording Components")]
        [Tooltip("Manages depth map export")]
        [SerializeField] private DepthMapExporter depthMapExporter = default!;
        
        [Tooltip("Manages camera image capture (left, right, etc.)")]
        [SerializeField] private ImageReaderSurfaceProvider[] cameraProviders = default!;
        
        [Tooltip("Manages pose logging (HMD, controllers, etc.)")]
        [SerializeField] private PoseLogger[] poseLoggers = default!;
        
        [Tooltip("Manages FPS timing for synchronized capture")]
        [SerializeField] private CaptureTimer captureTimer = default!;

        [Header("Recording Settings")]
        [SerializeField] private bool generateTimestampedDirectories = true;

        [Header("Events")]
        [Tooltip("Invoked when recording stops and files are saved. Passes the directory name where files were saved.")]
        [SerializeField] private UnityEvent<string> onRecordingSaved = default!;

        [Tooltip("Invoked when recording starts.")]
        [SerializeField] private UnityEvent onRecordingStarted = default!;

        #endregion

        #region State

        private RecordingState state = RecordingState.Idle;
        private float recordingStartTime = 0f;
        private string? currentSessionDirectory = null;

        #endregion

        #region Public API

        public bool IsRecording => state == RecordingState.Recording;
        public RecordingState State => state;
        public UnityEvent<string> OnRecordingSaved => onRecordingSaved;
        public UnityEvent OnRecordingStarted => onRecordingStarted;
        
        /// <summary>
        /// Gets the elapsed recording time in seconds.
        /// Returns 0 if not currently recording.
        /// </summary>
        public float RecordingDuration => IsRecording ? Time.time - recordingStartTime : 0f;

        /// <summary>
        /// Starts recording from all subsystems in the proper order.
        /// </summary>
        public void StartRecording()
        {
            if (state != RecordingState.Idle)
            {
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: Cannot start — current state is {state}");
                return;
            }

            // Generate session directory name if needed
            if (generateTimestampedDirectories)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                currentSessionDirectory = timestamp;
                depthMapExporter.DirectoryName = timestamp;
                foreach (var provider in cameraProviders)
                {
                    provider.DataDirectoryName = timestamp;
                }
                foreach (var logger in poseLoggers)
                {
                    logger.DirectoryName = timestamp;
                }
            }
            else
            {
                currentSessionDirectory = depthMapExporter.DirectoryName;
            }

            Debug.Log($"[{Constants.LOG_TAG}] RecordingManager: Starting recording session '{currentSessionDirectory}'");

            // Step 1: Update camera paths for new session
            // This ensures format info and images are written to the new directory
            foreach (var provider in cameraProviders)
            {
                provider.UpdateDirectoryPaths();
            }

            // Step 2: Setup file writers and directories
            // (Depth and camera systems are already initialized from app start)
            depthMapExporter.StartExport();
            foreach (var logger in poseLoggers)
            {
                logger.StartLogging();
            }
            
            // Reset camera base time to sync with depth/pose timestamps.
            // Camera's Kotlin layer sets baseMonoTimeNs at construction (app start),
            // while depth and pose reset theirs here at session start.
            // Without this reset, camera timestamps drift by app-open duration,
            // causing PoseInterpolator to drop all frames (30ms window exceeded).
            foreach (var provider in cameraProviders)
            {
                provider.ResetBaseTime();
            }

            // Step 3: Start synchronized capture
            // This begins the actual frame capture loop
            captureTimer.StartCapture();

            state = RecordingState.Recording;
            recordingStartTime = Time.time;

            onRecordingStarted?.Invoke();

            Debug.Log($"[{Constants.LOG_TAG}] RecordingManager: Recording started successfully");
        }

        /// <summary>
        /// Stops recording from all subsystems in the proper order.
        /// </summary>
        public void StopRecording()
        {
            if (state != RecordingState.Recording)
            {
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: Cannot stop — current state is {state}");
                return;
            }

            state = RecordingState.Finalizing;
            Debug.Log($"[{Constants.LOG_TAG}] RecordingManager: Finalizing recording session");

            // Stop in reverse order
            // Step 1: Stop capture loop first
            captureTimer.StopCapture();

            // Step 2: Close file writers and cleanup
            depthMapExporter.StopExport();
            foreach (var logger in poseLoggers)
            {
                logger.StopLogging();
            }

            // Store directory name before resetting state
            string savedDirectory = currentSessionDirectory ?? "";
            
            state = RecordingState.Idle;
            recordingStartTime = 0f;
            currentSessionDirectory = null;

            // Fire saved event AFTER resetting to Idle.
            // External managers listening to this event can now immediately start new recordings.
            if (!string.IsNullOrEmpty(savedDirectory))
            {
                onRecordingSaved?.Invoke(savedDirectory);
            }

            Debug.Log($"[{Constants.LOG_TAG}] RecordingManager: Recording stopped successfully. Files saved to '{savedDirectory}'");
        }

        /// <summary>
        /// Toggle recording on/off. Useful for UI buttons.
        /// </summary>
        public void ToggleRecording()
        {
            if (IsRecording)
                StopRecording();
            else
                StartRecording();
        }

        #endregion

        #region Lifecycle

        private void OnValidate()
        {
            // Validate required references in editor
            if (depthMapExporter == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: Missing DepthMapExporter reference!");
            
            if (cameraProviders == null || cameraProviders.Length == 0)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: No ImageReaderSurfaceProviders assigned! Add left and right camera providers.");
            
            if (poseLoggers == null || poseLoggers.Length == 0)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: No PoseLoggers assigned! Add HMD, controllers, etc.");
            
            if (captureTimer == null)
                Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingManager: Missing CaptureTimer reference!");
        }

        private void OnDestroy()
        {
            // Safety: ensure recording stops on cleanup
            if (IsRecording)
                StopRecording();
        }

        #endregion
    }
}

