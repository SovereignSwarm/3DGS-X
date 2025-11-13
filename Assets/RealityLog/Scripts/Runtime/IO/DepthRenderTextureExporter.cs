# nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using RealityLog.Common;

namespace RealityLog.IO
{
    public class DepthRenderTextureExporter : IDisposable
    {
        private readonly ComputeShader computeShader;
        private readonly int kernel;

        private readonly Queue<GraphicsBuffer> bufferPool = new();
        private bool isDisposed = false;

        private readonly object bufferPoolLock = new();
        
        // Event fired when depth data is ready (after AsyncGPUReadback completes)
        // Fires separately for each eye (eyeIndex: 0=left, 1=right)
        // Parameters: (depthData, width, height, eyeIndex, frameDescriptor)
        public event Action<NativeArray<float>, int, int, int, DepthFrameDesc>? OnDepthDataReady;

        public DepthRenderTextureExporter(ComputeShader computeShader)
        {
            this.computeShader = computeShader ?? throw new ArgumentNullException(nameof(computeShader));
            kernel = this.computeShader.FindKernel("CopyRT");
        }

        public void Export(RenderTexture sourceRT, string leftDepthOutputPath, string rightDepthOutputPath, DepthFrameDesc[] frameDescriptors)
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
            
            // Each eye is half the total width
            int eyeWidth = width / 2;
            int eyeHeight = height;

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

            RequestGPUReadbackAndSave(leftEyeBuffer, leftDepthOutputPath, eyeWidth, eyeHeight, 0, frameDescriptors[0]);
            RequestGPUReadbackAndSave(rightEyeBuffer, rightDepthOutputPath, eyeWidth, eyeHeight, 1, frameDescriptors[1]);
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
                if (isDisposed)
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

        private void RequestGPUReadbackAndSave(GraphicsBuffer buffer, string outputPath, int width, int height, int eyeIndex, DepthFrameDesc frameDescriptor)
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
                
                // Fire event for visualization/processing (with frame descriptor)
                // Subscribers can process the data while it's still valid
                try
                {
                    OnDepthDataReady?.Invoke(data, width, height, eyeIndex, frameDescriptor);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in OnDepthDataReady handler: {ex}");
                }

            });
        }

        private void SaveAsRaw(NativeArray<float> data, string path, Action onComplete)
        {
            Task.Run(() =>
            {
                try
                {
                    int byteLength = data.Length * sizeof(float);
                    byte[] rawBytes = new byte[byteLength];
                    Buffer.BlockCopy(data.ToArray(), 0, rawBytes, 0, byteLength);
                    File.WriteAllBytes(path, rawBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to save raw data: {ex}");
                }
                finally
                {
                    onComplete?.Invoke();
                }
            });
        }
    }
}