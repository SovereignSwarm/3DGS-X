# nullable enable

using UnityEngine;

namespace PerseusXR.Camera
{
    public interface ISurfaceProvider
    {
        AndroidJavaObject? GetJavaInstance(CameraMetadata metadata);
    }

    public abstract class SurfaceProviderBase : MonoBehaviour, ISurfaceProvider
    {
        public abstract AndroidJavaObject? GetJavaInstance(CameraMetadata metadata);
    }
}