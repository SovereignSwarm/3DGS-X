# nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RealityLog.Common;

namespace RealityLog.UI
{
    /// <summary>
    /// Manages scanning and listing recording directories from the file system.
    /// </summary>
    public class RecordingListManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Base directory name pattern for recordings (e.g., YYYYMMDD_HHMMSS)")]
        [SerializeField] private string recordingDirectoryPattern = "\\d{8}_\\d{6}";

        private List<RecordingInfo> recordings = new List<RecordingInfo>();

        /// <summary>
        /// Information about a recording session.
        /// </summary>
        [Serializable]
        public class RecordingInfo
        {
            public string DirectoryName { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public DateTime CreationTime { get; set; }
            public long SizeBytes { get; set; }
            public string FormattedSize => FormatBytes(SizeBytes);
            public string FormattedDate => CreationTime.ToString("yyyy-MM-dd HH:mm:ss");

            private static string FormatBytes(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// Event fired when the recording list is updated.
        /// </summary>
        public event Action<List<RecordingInfo>>? OnRecordingsUpdated;

        /// <summary>
        /// Gets the current list of recordings.
        /// </summary>
        public List<RecordingInfo> Recordings => new List<RecordingInfo>(recordings);

        /// <summary>
        /// Scans the persistent data path for recording directories.
        /// </summary>
        public void RefreshRecordings()
        {
            recordings.Clear();

            try
            {
                string dataPath = Application.persistentDataPath;
                
                if (!Directory.Exists(dataPath))
                {
                    Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingListManager: Data path does not exist: {dataPath}");
                    OnRecordingsUpdated?.Invoke(recordings);
                    return;
                }

                var directories = Directory.GetDirectories(dataPath);
                
                foreach (var dirPath in directories)
                {
                    string dirName = Path.GetFileName(dirPath);
                    
                    // Check if directory name matches recording pattern (YYYYMMDD_HHMMSS)
                    if (System.Text.RegularExpressions.Regex.IsMatch(dirName, recordingDirectoryPattern))
                    {
                        try
                        {
                            var info = new DirectoryInfo(dirPath);
                            var recordingInfo = new RecordingInfo
                            {
                                DirectoryName = dirName,
                                FullPath = dirPath,
                                CreationTime = info.CreationTime,
                                SizeBytes = CalculateDirectorySize(dirPath)
                            };
                            
                            recordings.Add(recordingInfo);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[{Constants.LOG_TAG}] RecordingListManager: Error reading directory {dirName}: {e.Message}");
                        }
                    }
                }

                // Sort by creation time, newest first
                recordings = recordings.OrderByDescending(r => r.CreationTime).ToList();

                Debug.Log($"[{Constants.LOG_TAG}] RecordingListManager: Found {recordings.Count} recordings");
                OnRecordingsUpdated?.Invoke(recordings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{Constants.LOG_TAG}] RecordingListManager: Error scanning recordings: {e.Message}");
                OnRecordingsUpdated?.Invoke(recordings);
            }
        }

        /// <summary>
        /// Calculates the total size of a directory in bytes.
        /// </summary>
        private long CalculateDirectorySize(string directoryPath)
        {
            long size = 0;
            try
            {
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        size += fileInfo.Length;
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
            }
            catch
            {
                // Return 0 if we can't calculate size
            }
            return size;
        }

        private void Start()
        {
            RefreshRecordings();
        }
    }
}

