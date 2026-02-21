# nullable enable

using System.Collections.Generic;
using UnityEngine;

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
        
        private Queue<GameObject> voxelPool = new Queue<GameObject>();
        [Tooltip("Pre-warm pool size to prevent hiccups.")]
        [SerializeField] private int initialPoolSize = 1000;
        
        [Tooltip("Hard limit on active meshes to prevent Quest SLAM hitching during large room scans.")]
        [SerializeField] private int maxVoxelPrimitives = 3000;
        private Queue<GameObject> activeVoxelHistory = new Queue<GameObject>();
        
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

            // Pre-warm pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject obj = CreateNewVoxel();
                obj.SetActive(false);
                voxelPool.Enqueue(obj);
            }
        }

        private GameObject CreateNewVoxel()
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(obj.GetComponent<Collider>());
            if (heatmapMaterial != null)
            {
                var renderer = obj.GetComponent<Renderer>();
                renderer.material = new Material(heatmapMaterial); // unique material instance to safely shift colors
            }
            return obj;
        }

        private GameObject GetPooledVoxel()
        {
            if (voxelPool.Count > 0)
            {
                GameObject obj = voxelPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            return CreateNewVoxel();
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
#if META_XR_MR_UTILITY_KIT
            // Use MRUK Environment Raycast to find what the user is looking at
            if (Meta.XR.MRUtilityKit.MRUK.Instance != null && Meta.XR.MRUtilityKit.MRUK.Instance.GetCurrentRoom() != null)
            {
                var room = Meta.XR.MRUtilityKit.MRUK.Instance.GetCurrentRoom();
                
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
                          // PerseusXR Cinematic Virtual Production Palette
                        // Cobalt/Silver for standard scans. Amber for Warnings.
                        Color color = hitDistance > 1.5f ? new Color(1f, 0.55f, 0f, 0.45f) : new Color(0.75f, 0.75f, 0.75f, 0.6f); 
                        ProcessVoxelPass(hitPoint, color);
                    }
                    else if (guidedManager.CurrentPass == CapturePass.ViewDependent)
                    {
                        ProcessAnchorPass(hitPoint, hitInfo.Value.hit.normal);
                    }
                }
            }
#endif
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
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            return new Vector3Int(
                Mathf.FloorToInt(localPos.x / size),
                Mathf.FloorToInt(localPos.y / size),
                Mathf.FloorToInt(localPos.z / size)
            );
        }

        private Vector3 VoxelToWorld(Vector3Int voxelCoords, float size)
        {
            Vector3 localPos = new Vector3(
                (voxelCoords.x * size) + (size / 2f),
                (voxelCoords.y * size) + (size / 2f),
                (voxelCoords.z * size) + (size / 2f)
            );
            return transform.TransformPoint(localPos);
        }

        private void SpawnVoxelIndicator(Vector3Int voxelCoords, Color color)
        {
            GameObject voxelObj = GetPooledVoxel();
            voxelObj.name = $"Voxel_{voxelCoords.x}_{voxelCoords.y}_{voxelCoords.z}";
            voxelObj.transform.SetParent(transform, true);
            voxelObj.transform.position = VoxelToWorld(voxelCoords, currentVoxelSize);
            voxelObj.transform.localScale = Vector3.one * (currentVoxelSize * 0.9f);
            
            var existingAnchor = voxelObj.GetComponent<DirectionAnchor>();
            if (existingAnchor != null) Destroy(existingAnchor);
            
            if (heatmapMaterial != null)
            {
                var renderer = voxelObj.GetComponent<Renderer>();
                renderer.material.color = color;
            }
            
            RegisterActiveVoxel(voxelObj);
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
            GameObject anchorObj = GetPooledVoxel();
            anchorObj.name = "DirectionAnchor";
            anchorObj.transform.SetParent(transform, true);
            anchorObj.transform.position = position;
            anchorObj.transform.localScale = Vector3.one * 0.1f;

            if (heatmapMaterial != null)
            {
                var renderer = anchorObj.GetComponent<Renderer>();
                renderer.material.color = Color.white;
            }

            var anchorScript = anchorObj.GetComponent<DirectionAnchor>();
            if (anchorScript == null) anchorScript = anchorObj.AddComponent<DirectionAnchor>();
            
            anchorScript.InitialViewDirection = centerEyeAnchor.forward;

            RegisterActiveVoxel(anchorObj);
        }

        private void RegisterActiveVoxel(GameObject obj)
        {
            activeVoxelObjects.Add(obj);
            activeVoxelHistory.Enqueue(obj);

            // FIFO optimization: Strip oldest meshes if we exceed Quest drawing caps
            while (activeVoxelObjects.Count > maxVoxelPrimitives)
            {
                GameObject oldestObj = activeVoxelHistory.Dequeue();
                if (oldestObj != null)
                {
                    activeVoxelObjects.Remove(oldestObj);
                    oldestObj.SetActive(false);
                    voxelPool.Enqueue(oldestObj);
                    
                    // Calculate its hash coordinates simply by name parsing, and remove from fast-lookup set
                    if (oldestObj.name.StartsWith("Voxel_"))
                    {
                        string[] parts = oldestObj.name.Split('_');
                        if (parts.Length == 4)
                        {
                            if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y) && int.TryParse(parts[3], out int z))
                            {
                                scannedVoxels.Remove(new Vector3Int(x, y, z));
                            }
                        }
                    }
                }
            }
        }

        private void ClearHeatmap()
        {
            if (scannedVoxels.Count == 0 && activeVoxelObjects.Count == 0) return;

            foreach (var go in activeVoxelObjects)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    voxelPool.Enqueue(go);
                }
            }
            activeVoxelObjects.Clear();
            activeVoxelHistory.Clear();
            scannedVoxels.Clear();
        }
    }
}
