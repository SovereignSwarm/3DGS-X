# nullable enable

using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using RealityLog.Common;

namespace RealityLog.FileOperations
{
    /// <summary>
    /// Handles file operations on recordings: delete, move to downloads, compress.
    /// </summary>
    public class RecordingOperations : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Base path for downloads folder on Android")]
        [SerializeField] private string downloadsBasePath = "/sdcard/Download";

        /// <summary>
        /// Event fired when an operation completes. Passes (operation, success, message).
        /// </summary>
        public event Action<string, bool, string>? OnOperationComplete;

        /// <summary>
        /// Deletes a recording directory and all its contents.
        /// </summary>
        public void DeleteRecording(string directoryName)
        {
            try
            {
                string fullPath = Path.Join(Application.persistentDataPath, directoryName);
                
                if (!Directory.Exists(fullPath))
                {
                    OnOperationComplete?.Invoke("Delete", false, $"Directory not found: {directoryName}");
                    return;
                }

                Directory.Delete(fullPath, true);
                Debug.Log($"[{Constants.LOG_TAG}] RecordingOperations: Deleted recording {directoryName}");
                OnOperationComplete?.Invoke("Delete", true, $"Deleted {directoryName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] RecordingOperations: Error deleting {directoryName}: {e.Message}");
                OnOperationComplete?.Invoke("Delete", false, $"Error: {e.Message}");
            }
        }

        /// <summary>
        /// Moves a recording from app data to the Downloads folder.
        /// </summary>
        public void MoveToDownloads(string directoryName)
        {
            try
            {
                string sourcePath = Path.Join(Application.persistentDataPath, directoryName);
                string destPath = Path.Join(downloadsBasePath, directoryName);

                if (!Directory.Exists(sourcePath))
                {
                    OnOperationComplete?.Invoke("MoveToDownloads", false, $"Directory not found: {directoryName}");
                    return;
                }

                // Create downloads directory if it doesn't exist
                if (!Directory.Exists(downloadsBasePath))
                {
                    Directory.CreateDirectory(downloadsBasePath);
                }

                // If destination exists, add timestamp suffix
                if (Directory.Exists(destPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    destPath = Path.Join(downloadsBasePath, $"{directoryName}_{timestamp}");
                }

                // Move directory
                Directory.Move(sourcePath, destPath);
                Debug.Log($"[{Constants.LOG_TAG}] RecordingOperations: Moved {directoryName} to Downloads");
                OnOperationComplete?.Invoke("MoveToDownloads", true, $"Moved to Downloads: {Path.GetFileName(destPath)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] RecordingOperations: Error moving {directoryName}: {e.Message}");
                OnOperationComplete?.Invoke("MoveToDownloads", false, $"Error: {e.Message}");
            }
        }

        /// <summary>
        /// Compresses a recording directory into a ZIP file.
        /// </summary>
        public void CompressRecording(string directoryName)
        {
            try
            {
                string sourcePath = Path.Join(Application.persistentDataPath, directoryName);
                string zipPath = Path.Join(Application.persistentDataPath, $"{directoryName}.zip");

                if (!Directory.Exists(sourcePath))
                {
                    OnOperationComplete?.Invoke("Compress", false, $"Directory not found: {directoryName}");
                    return;
                }

                // Delete existing zip if it exists
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                // Create zip file
                ZipFile.CreateFromDirectory(sourcePath, zipPath);
                
                Debug.Log($"[{Constants.LOG_TAG}] RecordingOperations: Compressed {directoryName} to {zipPath}");
                OnOperationComplete?.Invoke("Compress", true, $"Compressed to {directoryName}.zip");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] RecordingOperations: Error compressing {directoryName}: {e.Message}");
                OnOperationComplete?.Invoke("Compress", false, $"Error: {e.Message}");
            }
        }

        /// <summary>
        /// Exports a recording by compressing it and moving the ZIP to Downloads.
        /// </summary>
        public void ExportRecording(string directoryName)
        {
            try
            {
                string sourcePath = Path.Join(Application.persistentDataPath, directoryName);
                string zipName = $"{directoryName}.zip";
                string zipPath = Path.Join(Application.persistentDataPath, zipName);
                string destZipPath = Path.Join(downloadsBasePath, zipName);

                if (!Directory.Exists(sourcePath))
                {
                    OnOperationComplete?.Invoke("Export", false, $"Directory not found: {directoryName}");
                    return;
                }

                // Create downloads directory if it doesn't exist
                if (!Directory.Exists(downloadsBasePath))
                {
                    Directory.CreateDirectory(downloadsBasePath);
                }

                // Delete existing zip if it exists (both in persistent data and downloads)
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                // Create zip file in persistent data path first
                ZipFile.CreateFromDirectory(sourcePath, zipPath);
                
                // If destination zip exists, add timestamp suffix
                if (File.Exists(destZipPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string zipNameWithoutExt = Path.GetFileNameWithoutExtension(zipName);
                    destZipPath = Path.Join(downloadsBasePath, $"{zipNameWithoutExt}_{timestamp}.zip");
                }

                // Move zip to downloads
                File.Move(zipPath, destZipPath);
                
                Debug.Log($"[{Constants.LOG_TAG}] RecordingOperations: Exported {directoryName} to {destZipPath}");
                OnOperationComplete?.Invoke("Export", true, $"Exported to Downloads: {Path.GetFileName(destZipPath)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] RecordingOperations: Error exporting {directoryName}: {e.Message}");
                OnOperationComplete?.Invoke("Export", false, $"Error: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the Downloads folder path for the current platform.
        /// </summary>
        public string GetDownloadsPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return downloadsBasePath;
#else
            // For editor/testing, use a local downloads folder
            return Path.Join(Application.persistentDataPath, "Downloads");
#endif
        }
    }
}

