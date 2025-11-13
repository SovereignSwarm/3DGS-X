# nullable enable

using UnityEngine;

namespace RealityLog.Common
{
    /// <summary>
    /// Describes a depth frame's metadata (pose, FOV, timestamp, etc.)
    /// Shared between Depth and IO layers to avoid circular dependencies.
    /// </summary>
    public struct DepthFrameDesc
    {
        public long timestampNs;
        public Vector3 createPoseLocation;
        public Quaternion createPoseRotation;
        public float fovLeftAngleTangent;
        public float fovRightAngleTangent;
        public float fovTopAngleTangent;
        public float fovDownAngleTangent;
        public float nearZ;
        public float farZ;
    }
}
