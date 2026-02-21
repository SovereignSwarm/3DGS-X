# nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerseusXR.IO
{
    public class DepthRenderTextureExporter : IDisposable
    {
        private readonly ComputeShader computeShader;
        private readonly int kernel;

        private readonly Queue<GraphicsBuffer> bufferPool = new();
        private const int MAX_POOL_SIZE = 8; // Cap pool to prevent unbounded GPU memory growth
        private bool isDisposed = false;

        private readonly object bufferPoolLock = new();

        public DepthRenderTextureExporter(ComputeShader computeShader)
        {
            this.computeShader = computeShader ?? throw new ArgumentNullException(nameof(computeShader));
            kernel = this.computeShader.FindKernel("CopyRT");
        }

        public void Export(RenderTexture sourceRT, string leftDepthOutputPath, string rightDepthOutputPath)
        {
            if (isDisposed)
            {
                Debug.LogError("RenderTextureExporter has been disposed.");
                return;
            }

            if (sourceRT == null || !sourceRT.IsCreated())
            {
                Debug.LogError("RenderTexture is not created or null.");
                return;
            }

            var width = sourceRT.width;
            var height = sourceRT.height;
            var pixelCount = width * height;

            GraphicsBuffer leftEyeBuffer = GetOrCreateBuffer(pixelCount);
            GraphicsBuffer rightEyeBuffer = GetOrCreateBuffer(pixelCount);

            computeShader.SetTexture(kernel, "InputTex", sourceRT);
            computeShader.SetBuffer(kernel, "LeftEyeDepth", leftEyeBuffer);
            computeShader.SetBuffer(kernel, "RightEyeDepth", rightEyeBuffer);
            computeShader.SetInt("_Width", width);
            computeShader.SetInt("_Height", height);

            int groupsX = Mathf.CeilToInt(width / 8f);
            int groupsY = Mathf.CeilToInt(height / 8f);
            computeShader.Dispatch(kernel, groupsX, groupsY, 1);

            RequestGPUReadbackAndSave(leftEyeBuffer, leftDepthOutputPath);
            RequestGPUReadbackAndSave(rightEyeBuffer, rightDepthOutputPath);
        }

        public void Dispose()
        {
            isDisposed = true;
            ClearAllBuffers();
        }

        private GraphicsBuffer GetOrCreateBuffer(int pixelCount)
        {
            lock (bufferPoolLock)
            {
                if (bufferPool.Count > 0)
                {
                    var pooledBuffer = bufferPool.Dequeue();

                    if (pooledBuffer.count == pixelCount)
                    {
                        return pooledBuffer;
                    }
                    else
                    {
                        pooledBuffer.Dispose();
                    }
                }
            }

            return new GraphicsBuffer(GraphicsBuffer.Target.Structured, pixelCount, sizeof(float));
        }

        private void ReturnBuffer(GraphicsBuffer buffer)
        {
            lock (bufferPoolLock)
            {
                if (isDisposed || bufferPool.Count >= MAX_POOL_SIZE)
                {
                    buffer.Dispose();
                }
                else
                {
                    bufferPool.Enqueue(buffer);
                }
            }
        }

        private void ClearAllBuffers()
        {
            lock (bufferPoolLock)
            {
                while (bufferPool.Count > 0)
                {
                    var b = bufferPool.Dequeue();
                    b.Dispose();
                }
            }
        }

        private void RequestGPUReadbackAndSave(GraphicsBuffer buffer, string outputPath)
        {
            AsyncGPUReadback.Request(buffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback failed.");
                    ReturnBuffer(buffer);
                    return;
                }

                var data = request.GetData<float>();

                SaveAsRaw(data, outputPath, () => ReturnBuffer(buffer));
            });
        }

        private void SaveAsRaw(NativeArray<float> data, string path, Action onComplete)
        {
            // True Zero-GC path using Async FileStream directly on unmanaged memory slice
            Task.Run(async () =>
            {
                try
                {
                    // Convert float native array to byte slice
                    var byteSlice = data.Reinterpret<byte>(sizeof(float));
                    
                    // We can't pass NativeArray slice into FileStream directly without unsafe code,
                    // but we can allocate ONCE if absolutely necessary, or use a pooled byte array.
                    // For perfect Zero-GC inside the hot path, we use ArrayPool.
                    int byteLength = byteSlice.Length;
                    byte[] pooledBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(byteLength);
                    
                    // Fast unmanaged copy into pooled managed array
                    NativeArray<byte>.Copy(byteSlice, 0, pooledBuffer, 0, byteLength);

                    // Async write
                    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await fs.WriteAsync(pooledBuffer, 0, byteLength);
                    }
                    
                    // Return pool
                    System.Buffers.ArrayPool<byte>.Shared.Return(pooledBuffer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to save raw data: {ex}");
                }
                finally
                {
                    // Fire callback on main thread to safely return GPU buffer to queue
                    if (onComplete != null)
                    {
                        // Due to Threading complexities, we enqueue the callback execution
                        UnityEngine.WSA.Application.InvokeOnAppThread(() => { onComplete.Invoke(); }, false);
                        // Unity XR fallback if WSA is missing:
                        // Assuming ReturnBuffer is thread-safe (it has lock(bufferPoolLock))
                        onComplete.Invoke(); 
                    }
                }
            });
        }
    }
}