# nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PerseusXR.IO
{
    public class CsvWriter : IDisposable
    {
        private readonly BlockingCollection<string> _queue = new();
        private readonly string _filePath;
        private readonly string[] _header;
        private readonly Task _writerTask;
        private bool _disposed = false;

        public CsvWriter(string filePath, string[]? header = null)
        {
            _filePath = filePath;
            _header = header ?? Array.Empty<string>();
            _writerTask = Task.Run(WriteLoop);
        }

        public void EnqueueRow(params double[] columns)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CsvWriter));
            // Format to a single string BEFORE queueing to avoid array reference races
            // without using LINQ allocations.
            var line = string.Join(",", columns);
            _queue.Add(line);
        }

        public void EnqueueRow(params string[] columns)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CsvWriter));
            var line = string.Join(",", columns);
            _queue.Add(line);
        }

        private void WriteLoop()
        {
            var directoryName = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            using var writer = new StreamWriter(_filePath, append: false);

            if (_header.Length > 0)
            {
                writer.WriteLine(string.Join(",", _header));
            }

            foreach (var line in _queue.GetConsumingEnumerable())
            {
                writer.WriteLine(line);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _queue.CompleteAdding();
            _writerTask.Wait();
            _disposed = true;
        }
    }
}
