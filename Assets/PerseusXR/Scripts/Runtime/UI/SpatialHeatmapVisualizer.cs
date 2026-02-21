# nullable enable

using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

namespace PerseusXR.UI
{
    /// <summary>
    /// Implements a 2026-style MRUK voxel heatmap to provide users with 
    /// spatial recording coverage feedback. Integrates 3-pass guided capture logic.
    /// </summary>
    [RequireComponent(typeof(OVRCameraRig))]
    public class SpatialHeatmapVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecordingManager recordingManager = default!;
        [SerializeField] private GuidedCaptureManager guidedManager = default!;
        
        [Header("Heatmap Settings")]
        [Tooltip("Material used to render the heatmap voxels.")]
        [SerializeField] private Material heatmapMaterial = default!;
        
        [Tooltip("Maximum distance to raycast for coverage (meters).")]
        [SerializeField] private float raycastDistance = 4.0f;

        private HashSet<Vector3Int> scannedVoxels = new HashSet<Vector3Int>();
        private List<GameObject> activeVoxelObjects = new List<GameObject>();
        
        private Transform centerEyeAnchor = default!;
        private CapturePass lastProcessedPass = CapturePass.Geometry;
        private float currentVoxelSize = 0.5f;

        private void Start()
        {
            var rig = GetComponent<OVRCameraRig>();
            centerEyeAnchor = rig.centerEyeAnchor;

            if (guidedManager != null)
            {
                guidedManager.OnPassChanged += HandlePassChange;
            }
        }

        private void OnDestroy()
        {
            if (guidedManager != null)
            {
                guidedManager.OnPassChanged -= HandlePassChange;
            }
        }

        private void HandlePassChange(CapturePass newPass)
        {
            if (newPass != lastProcessedPass)
            {
                // Clear previous pass heatmap to not clutter the user's view
                ClearHeatmap();
                lastProcessedPass = newPass;

                if (newPass == CapturePass.Geometry) currentVoxelSize = 0.5f;
                else if (newPass == CapturePass.TextureDetail) currentVoxelSize = 0.1f;
                // Pass 3 doesn't use the voxel grid
            }
        }

        private void Update()
        {
            if (recordingManager == null || !recordingManager.IsRecording || guidedManager == null)
            {
                ClearHeatmap();
                return;
            }

            UpdateCoverage();
        }

        private void UpdateCoverage()
        {
            // Use MRUK Environment Raycast to find what the user is looking at
            if (MRUK.Instance != null && MRUK.Instance.GetCurrentRoom() != null)
            {
                var room = MRUK.Instance.GetCurrentRoom();
                
                bool hit = room.Raycast(new Ray(centerEyeAnchor.position, centerEyeAnchor.forward), raycastDistance, out var hitInfo);
                
                if (hit && hitInfo.HasValue)
                {
                    Vector3 hitPoint = hitInfo.Value.hit.point;
                    float hitDistance = Vector3.Distance(centerEyeAnchor.position, hitPoint);
                    
                    if (guidedManager.CurrentPass == CapturePass.Geometry)
                    {
                        ProcessVoxelPass(hitPoint, new Color(1f, 1f, 1f, 0.2f)); // Ghostly white point cloud
                    }
                    else if (guidedManager.CurrentPass == CapturePass.TextureDetail)
                    {
                        // Enforce proximity
                        Color color = hitDistance > 1.5f ? new Color(1f, 0f, 0.8f, 0.4f) : new Color(0f, 1f, 1f, 0.6f); // Magenta warning, Cyan success
                        ProcessVoxelPass(hitPoint, color);
                    }
                    else if (guidedManager.CurrentPass == CapturePass.ViewDependent)
                    {
                        ProcessAnchorPass(hitPoint, hitInfo.Value.hit.normal);
                    }
                }
            }
        }

        private void ProcessVoxelPass(Vector3 hitPoint, Color voxelColor)
        {
            Vector3Int voxelCoords = WorldToVoxel(hitPoint, currentVoxelSize);
            
            if (scannedVoxels.Add(voxelCoords))
            {
                SpawnVoxelIndicator(voxelCoords, voxelColor);
            }
            else
            {
                // If it already exists but we are in TextureDetail and close enough now, we can update it to green
                if (guidedManager.CurrentPass == CapturePass.TextureDetail && voxelColor.g > 0.5f)
                {
                    UpdateVoxelColor(voxelCoords, voxelColor);
                }
            }
        }

        private void ProcessAnchorPass(Vector3 hitPoint, Vector3 normal)
        {
            // For Pass 3, drop an anchor if there isn't one nearby
            Vector3Int gridPos = WorldToVoxel(hitPoint, 0.4f); // Space them out roughly 0.4m
            if (scannedVoxels.Add(gridPos))
            {
                SpawnDirectionAnchor(hitPoint, normal);
            }
        }

        private Vector3Int WorldToVoxel(Vector3 worldPos, float size)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / size),
                Mathf.FloorToInt(worldPos.y / size),
                Mathf.FloorToInt(worldPos.z / size)
            );
        }

        private Vector3 VoxelToWorld(Vector3Int voxelCoords, float size)
        {
            return new Vector3(
                (voxelCoords.x * size) + (size / 2f),
                (voxelCoords.y * size) + (size / 2f),
                (voxelCoords.z * size) + (size / 2f)
            );
        }

        private void SpawnVoxelIndicator(Vector3Int voxelCoords, Color color)
        {
            GameObject voxelObj = GameObject.CreatePrimitive(PrimitiveType.Sphere); // Sleek Point-Cloud Style
            voxelObj.name = $"Voxel_{voxelCoords.x}_{voxelCoords.y}_{voxelCoords.z}";
            voxelObj.transform.position = VoxelToWorld(voxelCoords, currentVoxelSize);
            voxelObj.transform.localScale = Vector3.one * (currentVoxelSize * 0.9f);
            
            Destroy(voxelObj.GetComponent<Collider>());
            
            if (heatmapMaterial != null)
            {
                var renderer = voxelObj.GetComponent<Renderer>();
                renderer.material = heatmapMaterial;
                renderer.material.color = color;
            }
            
            activeVoxelObjects.Add(voxelObj);
        }

        private void UpdateVoxelColor(Vector3Int voxelCoords, Color color)
        {
            string searchName = $"Voxel_{voxelCoords.x}_{voxelCoords.y}_{voxelCoords.z}";
            foreach (var go in activeVoxelObjects)
            {
                if (go.name == searchName)
                {
                    if (heatmapMaterial != null)
                    {
                        var renderer = go.GetComponent<Renderer>();
                        renderer.material.color = color;
                    }
                    break;
                }
            }
        }

        private void SpawnDirectionAnchor(Vector3 position, Vector3 normal)
        {
            GameObject anchorObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchorObj.transform.position = position;
            anchorObj.transform.localScale = Vector3.one * 0.1f;
            
            Destroy(anchorObj.GetComponent<Collider>());

            if (heatmapMaterial != null)
            {
                var renderer = anchorObj.GetComponent<Renderer>();
                renderer.material = heatmapMaterial;
            }

            var anchorScript = anchorObj.AddComponent<DirectionAnchor>();
            anchorScript.InitialViewDirection = centerEyeAnchor.forward;

            activeVoxelObjects.Add(anchorObj);
        }

        private void ClearHeatmap()
        {
            if (scannedVoxels.Count == 0 && activeVoxelObjects.Count == 0) return;

            foreach (var go in activeVoxelObjects)
            {
                if (go != null) Destroy(go);
            }
            activeVoxelObjects.Clear();
            scannedVoxels.Clear();
        }
    }
}
