# nullable enable

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
using PerseusXR.Common;
using PerseusXR.IO;

namespace PerseusXR.Depth
{
    public class DepthMapExporter : MonoBehaviour
    {
        private static readonly string[] descriptorHeader = new[]
            {
                "timestamp_ms", "ovr_timestamp",
                "create_pose_location_x", "create_pose_location_y", "create_pose_location_z",
                "create_pose_rotation_x", "create_pose_rotation_y", "create_pose_rotation_z", "create_pose_rotation_w",
                "fov_left_angle_tangent", "fov_right_angle_tangent", "fov_top_angle_tangent", "fov_down_angle_tangent",
                "near_z", "far_z",
                "width", "height"
            };

        [HideInInspector]
        [SerializeField] private ComputeShader copyDepthMapShader = default!;
        [SerializeField] private string directoryName = "";
        [SerializeField] private string leftDepthMapDirectoryName = "left_depth";
        [SerializeField] private string rightDepthMapDirectoryName = "right_depth";
        [SerializeField] private string leftDepthDescFileName = "left_depth_descriptors.csv";
        [SerializeField] private string rightDepthDescFileName = "right_depth_descriptors.csv";
        [Header("Synchronized Capture")]
        [Tooltip("Required: Reference to CaptureTimer for FPS-based capture timing.")]
        [SerializeField] private CaptureTimer captureTimer = default!;

        private DepthDataExtractor? depthDataExtractor;

        private DepthRenderTextureExporter? renderTextureExporter;
        private CsvWriter? leftDepthCsvWriter;
        private CsvWriter? rightDepthCsvWriter;

        private double baseOvrTimeSec;
        private long baseOvrTimeNs; // nanosecond base for integer-only conversion (P-8 fix)
        private long baseUnixTimeMs;

        private bool hasScenePermission = false;
        private bool depthSystemReady = false;
        private float permissionCheckTimer = 0f;

        public bool IsDepthSystemReady => depthSystemReady;

        public string DirectoryName
        {
            get => directoryName;
            set => directoryName = value;
        }

        public void StartExport()
        {
            leftDepthCsvWriter?.Dispose();
            rightDepthCsvWriter?.Dispose();

            // Reset base times when starting a new recording session
            // This ensures timestamps align with camera/pose data
            baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
            baseOvrTimeNs = (long)(baseOvrTimeSec * 1.0e9);
            baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Debug.Log($"[{Constants.LOG_TAG}] DepthMapExporter - Reset base times: OVR={baseOvrTimeSec:F3}s, Unix={baseUnixTimeMs}ms");

            leftDepthCsvWriter = new(Path.Join(Application.persistentDataPath, DirectoryName, leftDepthDescFileName), descriptorHeader);
            rightDepthCsvWriter = new(Path.Join(Application.persistentDataPath, DirectoryName, rightDepthDescFileName), descriptorHeader);

            Directory.CreateDirectory(Path.Join(Application.persistentDataPath, DirectoryName, leftDepthMapDirectoryName));
            Directory.CreateDirectory(Path.Join(Application.persistentDataPath, DirectoryName, rightDepthMapDirectoryName));
        }

        public void StopExport()
        {
            // Note: Timer stop is handled by RecordingManager
            // Just cleanup our resources here

            leftDepthCsvWriter?.Dispose();
            leftDepthCsvWriter = null;
            rightDepthCsvWriter?.Dispose();
            rightDepthCsvWriter = null;

            // Note: We keep depth enabled to avoid re-initialization overhead on next recording
        }

        private void Start()
        {
            baseOvrTimeSec = OVRPlugin.GetTimeInSeconds();
            baseOvrTimeNs = (long)(baseOvrTimeSec * 1.0e9);
            baseUnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            depthDataExtractor = new();
            renderTextureExporter = new(copyDepthMapShader);

            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission);

            // Note: We do NOT enable depth here anymore. We wait for permission in Update().
            Application.onBeforeRender += OnBeforeRender;
        }

        private void Update()
        {
            // Try to "prime" the depth system by fetching one frame at startup
            // Once we get a valid frame, mark the system as ready and stop trying
            if (!depthSystemReady && depthDataExtractor != null)
            {
                // Check for permission first
                if (!hasScenePermission)
                {
                    permissionCheckTimer += Time.unscaledDeltaTime;
                    if (permissionCheckTimer < 1f) return; // Only check once per second
                    permissionCheckTimer = 0f;

                    hasScenePermission = Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission);
                    if (!hasScenePermission) return; // Wait for permission
                    
                    // Permission granted, enable depth
                    depthDataExtractor.SetDepthEnabled(true);
                    Debug.Log($"[{Constants.LOG_TAG}] DepthMapExporter - Scene permission granted, enabling depth system...");
                }

                if (depthDataExtractor.TryGetUpdatedDepthTexture(out var renderTexture, out var frameDescriptors))
                {
                    if (renderTexture != null && renderTexture.IsCreated())
                    {
                        depthSystemReady = true;
                        Debug.Log($"[{Constants.LOG_TAG}] DepthMapExporter - Depth system warmed up and ready!");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up depth system
            depthDataExtractor?.SetDepthEnabled(false);
            
            renderTextureExporter?.Dispose();
            renderTextureExporter = null;

            Application.onBeforeRender -= OnBeforeRender;
        }

        private void OnBeforeRender()
        {
            // Early exit if resources not ready
            if (renderTextureExporter == null || depthDataExtractor == null
                || leftDepthCsvWriter == null || rightDepthCsvWriter == null)
            {
                return;
            }

            // Check if timer says we should capture this frame
            // Timer handles FPS timing internally
            if (!captureTimer.IsCapturing || !captureTimer.ShouldCaptureThisFrame)
            {
                return;
            }
            
#if UNITY_EDITOR
            // Debug: Log when we're about to capture
            Debug.Log($"[DepthExporter] Capturing depth at Unity time={Time.unscaledTime:F3}s");
#endif

            if (!hasScenePermission)
            {
                // JNI calls here in OnBeforeRender cause severe performance drops.
                // We rely on Update() to poll for permission instead.
                return;
            }

            if (depthDataExtractor.TryGetUpdatedDepthTexture(out var renderTexture, out var frameDescriptors))
            {
                // Depth system is ready (we already warmed it up in Update())
                // Just capture the frame data

                const int FRAME_DESC_COUNT = 2;

                if (renderTexture == null || !renderTexture.IsCreated())
                {
                    Debug.LogError("RenderTexture is not created or null.");
                    return;
                }

                if (frameDescriptors.Length != FRAME_DESC_COUNT)
                {
                    Debug.LogError("Expected exactly two depth frame descriptors (left and right).");
                    return;
                }

                var width = renderTexture.width;
                var height = renderTexture.height;

                var unixTime = ConvertTimestampNsToUnixTimeMs(frameDescriptors[0].timestampNs);

                var leftDepthFilePath = Path.Join(Application.persistentDataPath, DirectoryName, $"{leftDepthMapDirectoryName}/{unixTime}.raw");
                var rightDepthFilePath = Path.Join(Application.persistentDataPath, DirectoryName, $"{rightDepthMapDirectoryName}/{unixTime}.raw");

                renderTextureExporter.Export(renderTexture, leftDepthFilePath, rightDepthFilePath);

                for (var i = 0; i < FRAME_DESC_COUNT; ++i)
                {
                    var frameDesc = frameDescriptors[i];

                    var timestampMs = ConvertTimestampNsToUnixTimeMs(frameDesc.timestampNs);
                    var ovrTimestamp = frameDesc.timestampNs / 1.0e9;

                    // Create a new array instance per row to prevent background I/O race conditions
                    // where the main thread overwrites the same array reference before it saves.
                    double[] row = new double[17];
                    row[0] = timestampMs;
                    row[1] = ovrTimestamp;
                    row[2] = frameDesc.createPoseLocation.x;
                    row[3] = frameDesc.createPoseLocation.y;
                    row[4] = frameDesc.createPoseLocation.z;
                    row[5] = frameDesc.createPoseRotation.x;
                    row[6] = frameDesc.createPoseRotation.y;
                    row[7] = frameDesc.createPoseRotation.z;
                    row[8] = frameDesc.createPoseRotation.w;
                    row[9] = frameDesc.fovLeftAngleTangent;
                    row[10] = frameDesc.fovRightAngleTangent;
                    row[11] = frameDesc.fovTopAngleTangent;
                    row[12] = frameDesc.fovDownAngleTangent;
                    row[13] = frameDesc.nearZ;
                    row[14] = frameDesc.farZ;
                    row[15] = width;
                    row[16] = height;

                    if (i == 0)
                    {
                        leftDepthCsvWriter?.EnqueueRow(row);
                    }
                    else
                    {
                        rightDepthCsvWriter?.EnqueueRow(row);
                    }
                }
            } else {
                Debug.LogError("Failed to get updated depth texture.");
            }
        }

        private long ConvertTimestampNsToUnixTimeMs(long timestampNs)
        {
            // Integer-only arithmetic to avoid floating-point precision loss after 4.6+ hours
            var deltaNs = timestampNs - baseOvrTimeNs;
            var deltaMs = deltaNs / 1_000_000;
            return baseUnixTimeMs + deltaMs;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            const string COPY_DEPTH_MAP_SHADER_PATH = "Assets/PerseusXR/ComputeShaders/CopyDepthMap.compute";

            if (copyDepthMapShader == null)
            {
                var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(COPY_DEPTH_MAP_SHADER_PATH);
                if (shader == null)
                {
                    Debug.LogError($"Failed to load ComputeShader at path: {COPY_DEPTH_MAP_SHADER_PATH}");
                }
                else
                {
                    copyDepthMapShader = shader;
                    Debug.Log($"Successfully loaded ComputeShader: {COPY_DEPTH_MAP_SHADER_PATH}");
                }
            }
        }
# endif
    }
}