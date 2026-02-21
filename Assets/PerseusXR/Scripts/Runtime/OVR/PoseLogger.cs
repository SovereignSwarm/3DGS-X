# nullable enable

using System;
using System.IO;
using UnityEngine;
using PerseusXR.Common;
using PerseusXR.IO;

namespace PerseusXR.OVR
{
    public enum PoseStateMode
    {
        Immediate,
        Raw
    }

    public class PoseLogger : MonoBehaviour
    {
        private static readonly string[] HEADER = new string[]
            {
                "unix_time", "ovr_timestamp",
                "pos_x", "pos_y", "pos_z", 
                "rot_x", "rot_y", "rot_z", "rot_w", 
            };

        [SerializeField] private OVRPlugin.Node node = OVRPlugin.Node.Head;
        [SerializeField] private PoseStateMode mode = PoseStateMode.Immediate;
        [SerializeField] private string fileName = "poses.csv";
        [SerializeField] private string directoryName = "";
        [SerializeField] private bool startLoggingOnStart = false;
        [Header("Optional")]
        [SerializeField] private Transform trackingSpace = default!;
        [Tooltip("Gate pose logging to only fire when CaptureTimer signals a capture frame")]
        [SerializeField] private CaptureTimer? captureTimer = default;

        private CsvWriter? writer = null;

        private double baseOvrTimeSec;
        private long baseUnixTimeMs;

        private double latestTimestamp;

        public string DirectoryName
        {
            get => directoryName;
            set => directoryName = value;
        }

        public void StartLogging()
        {
            try
            {
                StopLogging();
                
                // Reset base times when starting a new recording session
                // This ensures timestamps align with camera/depth that also reset on session start
                baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
                baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                latestTimestamp = 0;
                
                Debug.Log($"[{Constants.LOG_TAG}] {fileName} - Reset base times: OVR={baseOvrTimeSec:F3}s, Unix={baseUnixTimeMs}ms");
                
                var filePath = Path.Combine(Application.persistentDataPath, DirectoryName, fileName);
                writer = new CsvWriter(filePath, HEADER);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] Failed to create CsvWriter: {ex.Message}");
                writer = null;
            }
        }

        public void StopLogging()
        {
            try
            {
                writer?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] Failed to dispose CsvWriter: {ex.Message}");
            }

            writer = null;
        }

        private void Start()
        {
            baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
            baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Debug.Log($"[Time Log] Base OVR Time (sec): {baseOvrTimeSec}, Base Unix Time (ms): {baseUnixTimeMs}");

            if (startLoggingOnStart)
            {
                StartLogging();
            }
        }

        private void LateUpdate()
        {
            if (writer == null)
                return;

            // If a CaptureTimer is assigned, only log when the timer signals capture.
            // This matches depth/camera behavior and prevents 50Hz ungated logging.
            if (captureTimer != null && (!captureTimer.IsCapturing || !captureTimer.ShouldCaptureThisFrame))
                return;

            EnqueueRowIfNeeded(writer);
        }

        private void EnqueueRowIfNeeded(CsvWriter writer)
        {
            var poseState = mode switch 
                {
                    PoseStateMode.Immediate => OVRPlugin.GetNodePoseStateImmediate(node),
                    PoseStateMode.Raw => OVRPlugin.GetNodePoseStateRaw(node, OVRPlugin.Step.Render),
                    _ => OVRPlugin.PoseStatef.identity,
                };

            var timestamp = poseState.Time;

            if (timestamp <= latestTimestamp)
            {
                return;
            }

            latestTimestamp = timestamp;

            var pose = poseState.Pose.ToOVRPose();

            var position = pose.position;
            var orientation = pose.orientation;

            if (trackingSpace != null)
            {
                position = trackingSpace.TransformPoint(position);
                orientation = trackingSpace.rotation * orientation;
            }

            writer.EnqueueRow(
                ConvertOvrSecToUnixTimeMs(timestamp), timestamp,
                position.x, position.y, position.z,
                orientation.x, orientation.y, orientation.z, orientation.w
            );
        }

        private long ConvertOvrSecToUnixTimeMs(double ovrTime)
        {
            var deltaSec = ovrTime - baseOvrTimeSec;
            var deltaMs = (long) (deltaSec * 1000.0);
            return baseUnixTimeMs + deltaMs;
        }

        private void OnDestroy()
        {
            writer?.Dispose();
            writer = null;
        }
    }
}