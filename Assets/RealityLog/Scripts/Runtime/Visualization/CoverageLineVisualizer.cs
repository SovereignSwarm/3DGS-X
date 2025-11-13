# nullable enable

using UnityEngine;
using RealityLog.Common;
using RealityLog.Depth;

namespace RealityLog.Visualization
{
    /// <summary>
    /// Visualizes scan coverage by emitting colored line particles at depth sample points.
    /// Each line points toward the camera position and is colored based on viewing angle.
    /// </summary>
    public class CoverageLineVisualizer : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Required: Reference to DepthMapExporter to subscribe to depth frame events")]
        [SerializeField] private DepthMapExporter depthMapExporter = default!;
        
        [Header("Particle System")]
        [SerializeField] private ParticleSystem coverageParticleSystem = default!;
        
        [Header("Line Settings")]
        [Tooltip("Length of each coverage line in meters")]
        [SerializeField] private float lineLength = 0.05f; // 5cm
        
        [Tooltip("How many depth pixels to skip (higher = better performance, fewer lines)")]
        [SerializeField] private int downsampleFactor = 16; // Sample every 16th pixel
        
        [Tooltip("Lifetime of coverage lines in seconds")]
        [SerializeField] private float lineLifetime = 30f;
        
        [Tooltip("Enable/disable coverage visualization")]
        [SerializeField] private bool isEnabled = true;
        
        [Header("Color Settings")]
        [Tooltip("Saturation of direction colors (0-1)")]
        [SerializeField] private float colorSaturation = 0.9f;
        
        [Tooltip("Brightness of direction colors (0-1)")]
        [SerializeField] private float colorBrightness = 1f;
        
        private ParticleSystem.EmitParams emitParams = new();
        private ParticleSystemRenderer? particleRenderer;
        private bool isInitialized = false;
        
        // Store camera position when OnDepthFrameCaptured fires, use when OnDepthDataReady arrives
        private Vector3 pendingCameraPos;
        
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                if (particleRenderer != null)
                {
                    particleRenderer.enabled = isEnabled;
                }
            }
        }
        
        private void OnEnable()
        {
            // Subscribe to depth frame events
            if (depthMapExporter != null)
            {
                depthMapExporter.OnDepthFrameCaptured += OnDepthFrameCaptured;
                depthMapExporter.OnDepthDataReady += OnDepthDataReady;
            }
        }
        
        private void OnDisable()
        {
            // Unsubscribe from depth frame events
            if (depthMapExporter != null)
            {
                depthMapExporter.OnDepthFrameCaptured -= OnDepthFrameCaptured;
                depthMapExporter.OnDepthDataReady -= OnDepthDataReady;
            }
        }
        
        private void Start()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            if (isInitialized) return;
            
            if (coverageParticleSystem == null)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] CoverageLineVisualizer: ParticleSystem not assigned!");
                return;
            }
            
            // Verify particle system is configured correctly
            particleRenderer = coverageParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] CoverageLineVisualizer: ParticleSystemRenderer not found!");
                return;
            }
            
            // Verify material is assigned
            if (particleRenderer.material == null)
            {
                Debug.LogWarning($"[{Constants.LOG_TAG}] CoverageLineVisualizer: No material assigned! Particles may not render.");
            }
            else
            {
                Debug.Log($"[{Constants.LOG_TAG}] CoverageLineVisualizer: Using material '{particleRenderer.material.name}' with shader '{particleRenderer.material.shader.name}'");
            }
            
            // Update lifetime from inspector setting
            var main = coverageParticleSystem.main;
            main.startLifetime = lineLifetime;
            
            // Update stretch scale from inspector setting  
            particleRenderer.lengthScale = lineLength * 20f;
            
            // Start the particle system
            if (!coverageParticleSystem.isPlaying)
            {
                coverageParticleSystem.Play();
            }
            
            isInitialized = true;
            Debug.Log($"[{Constants.LOG_TAG}] CoverageLineVisualizer initialized successfully");
            
            // TEST: Emit some test particles to verify rendering works
            // TODO: Remove after testing!
            EmitTestParticles();
        }
        
        /// <summary>
        /// Emits test particles in a pattern to verify the system works.
        /// Remove this after confirming particles render correctly!
        /// </summary>
        private void EmitTestParticles()
        {
            Debug.Log($"[{Constants.LOG_TAG}] Emitting test particles...");
            Debug.Log($"[{Constants.LOG_TAG}] Particle system alive count BEFORE: {coverageParticleSystem.particleCount}");
            
            // Emit particles in a circle pattern in front of the camera
            Transform camTransform = Camera.main.transform;
            Vector3 centerPos = camTransform.position + camTransform.forward * 2f; // 2m in front
            
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad; // 30 degree intervals
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.5f;
                Vector3 worldPos = centerPos + offset;
                
                // Different color per particle
                Color color = Color.HSVToRGB(i / 12f, 1f, 1f);
                Vector3 direction = (camTransform.position - worldPos).normalized;
                
                Debug.Log($"[{Constants.LOG_TAG}] Emitting particle {i} at {worldPos} with color {color}");
                EmitCoverageLine(worldPos, direction, color);
            }
            
            Debug.Log($"[{Constants.LOG_TAG}] Particle system alive count AFTER: {coverageParticleSystem.particleCount}");
            Debug.Log($"[{Constants.LOG_TAG}] Test particles emitted!");
        }
        
        /// <summary>
        /// Event handler called when a depth frame is captured.
        /// Stores camera position for when depth data arrives via OnDepthDataReady (1-2 frames later).
        /// Frame descriptors are passed through AsyncGPUReadback to avoid race conditions.
        /// </summary>
        /// <param name="depthFrameDescs">Depth frame descriptors (not used, passed via OnDepthDataReady)</param>
        /// <param name="depthTexture">Depth render texture (not used, data comes via OnDepthDataReady)</param>
        /// <param name="cameraTransform">Camera transform at time of capture</param>
        private void OnDepthFrameCaptured(
            DepthFrameDesc[] depthFrameDescs, 
            RenderTexture depthTexture,
            Transform cameraTransform)
        {
            if (!isEnabled || !isInitialized) return;
            
            // Store camera position for when depth data arrives (async, 1-2 frames later)
            // Note: Frame descriptors are passed through the AsyncGPUReadback callback
            pendingCameraPos = cameraTransform.position;
        }
        
        /// <summary>
        /// Event handler called when depth data is ready from AsyncGPUReadback.
        /// Processes the depth data and emits coverage line particles.
        /// </summary>
        /// <param name="depthData">Depth values in meters (row-major order)</param>
        /// <param name="width">Width of the depth image for this eye</param>
        /// <param name="height">Height of the depth image for this eye</param>
        /// <param name="eyeIndex">Eye index (0=left, 1=right)</param>
        /// <param name="frameDesc">Frame descriptor for this eye (passed through AsyncGPUReadback)</param>
        private void OnDepthDataReady(Unity.Collections.NativeArray<float> depthData, int width, int height, int eyeIndex, DepthFrameDesc frameDesc)
        {
            if (!isEnabled || !isInitialized) return;
            
            Vector3 cameraPos = pendingCameraPos;
            
            int particlesEmitted = 0;
            int validDepthSamples = 0;
            
            // Downsample for performance
            for (int y = 0; y < height; y += downsampleFactor)
            {
                for (int x = 0; x < width; x += downsampleFactor)
                {
                    // Get depth value from NativeArray (row-major order)
                    int index = y * width + x;
                    
                    if (index >= depthData.Length)
                        continue;
                    
                    float depth = depthData[index];
                    
                    // Skip invalid depth values
                    if (depth < 0.1f || depth > 10f)
                        continue;
                    
                    validDepthSamples++;
                    
                    // Unproject pixel to 3D world space
                    Vector3 worldPos = UnprojectDepthPixel(x, y, width, height, frameDesc, depth);
                    
                    // Check if valid point
                    float distance = Vector3.Distance(worldPos, cameraPos);
                    if (distance < 0.1f || distance > 10f)
                        continue;
                    
                    // Calculate view direction (from surface to camera)
                    Vector3 viewDir = (cameraPos - worldPos).normalized;
                    
                    // Color based on viewing angle
                    Color lineColor = DirectionToColor(viewDir);
                    
                    // Emit coverage line particle
                    EmitCoverageLine(worldPos, viewDir, lineColor);
                    particlesEmitted++;
                }
            }
            
            Debug.Log($"[{Constants.LOG_TAG}] Eye {eyeIndex}: {validDepthSamples} valid depth samples, {particlesEmitted} particles emitted");
        }
        
        /// <summary>
        /// Unprojects a depth pixel to 3D world space using the depth frame descriptor.
        /// Uses the frame descriptor's FOV and pose information for accurate unprojection.
        /// </summary>
        private Vector3 UnprojectDepthPixel(int x, int y, int width, int height, DepthFrameDesc frameDesc, float depth)
        {
            // Normalize pixel coordinates to [0, 1]
            float u = (float)x / width;
            float v = (float)y / height;
            
            // Interpolate FOV tangents based on pixel position
            // These tangents define the frustum of the depth camera
            float tanX = Mathf.Lerp(frameDesc.fovLeftAngleTangent, frameDesc.fovRightAngleTangent, u);
            float tanY = Mathf.Lerp(frameDesc.fovDownAngleTangent, frameDesc.fovTopAngleTangent, v);
            
            // Unproject to camera-local space
            // The FOV tangents directly give us the direction, scaled by depth
            Vector3 localPos = new Vector3(
                tanX * depth,
                -tanY * depth,  // Flip Y (texture coords are top-down, world is bottom-up)
                depth
            );
            
            // Transform to world space using frame descriptor pose
            Quaternion rotation = frameDesc.createPoseRotation;
            Vector3 position = frameDesc.createPoseLocation;
            
            Vector3 worldPos = position + rotation * localPos;
            
            return worldPos;
        }
        
        /// <summary>
        /// Emits a single coverage line particle at the specified position and direction.
        /// </summary>
        private void EmitCoverageLine(Vector3 position, Vector3 direction, Color color)
        {
            emitParams.ResetPosition();
            emitParams.ResetRotation();
            emitParams.ResetVelocity();
            
            emitParams.position = position;
            emitParams.velocity = direction * 0.00001f; // Tiny velocity in the direction (stretch uses this)
            emitParams.startColor = color;
            emitParams.startSize = lineLength; // Use actual line length
            emitParams.startLifetime = lineLifetime;
            
            // Orient line to point in viewing direction (for stretched billboards)
            emitParams.rotation3D = Quaternion.LookRotation(direction).eulerAngles;
            
            coverageParticleSystem.Emit(emitParams, 1);
        }
        
        /// <summary>
        /// Converts a view direction vector to a color using horizontal angle as hue.
        /// </summary>
        private Color DirectionToColor(Vector3 viewDirection)
        {
            // Calculate horizontal angle (0-360 degrees)
            float angle = Mathf.Atan2(viewDirection.x, viewDirection.z) * Mathf.Rad2Deg;
            
            // Normalize to 0-1 for hue
            float hue = (angle + 180f) / 360f;
            
            // Convert HSV to RGB
            return Color.HSVToRGB(hue, colorSaturation, colorBrightness);
        }
        
        /// <summary>
        /// Clears all existing coverage lines.
        /// </summary>
        public void Clear()
        {
            if (coverageParticleSystem != null)
            {
                coverageParticleSystem.Clear();
                Debug.Log($"[{Constants.LOG_TAG}] CoverageLineVisualizer: Cleared all coverage lines");
            }
        }
        
        private void OnValidate()
        {
            // Clamp values in editor
            lineLength = Mathf.Max(0.01f, lineLength);
            downsampleFactor = Mathf.Max(1, downsampleFactor);
            lineLifetime = Mathf.Max(1f, lineLifetime);
            colorSaturation = Mathf.Clamp01(colorSaturation);
            colorBrightness = Mathf.Clamp01(colorBrightness);
        }
    }
}

