using g2yx.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace g2yx.Services
{
    public class GoogleTakeoutReader : IPhotosReader
    {
        private readonly string _takeoutDir;

        public GoogleTakeoutReader(string takeoutDir)
        {
            _takeoutDir = takeoutDir;
        }

        public async IAsyncEnumerable<AlbumPhoto> Read(string syncPointer, [EnumeratorCancellation] CancellationToken ct)
        {
            var prevSyncPointer = SyncPointer.Parse(syncPointer);

            foreach (var (filePath, fileNumber) in GetSortedZipArchivePaths())
            {
                // skip already processed files
                if (fileNumber < prevSyncPointer.FileNumber)
                    continue;

                using var zipFile = ZipFile.OpenRead(filePath);
                IEnumerable<ZipArchiveEntry> orderedEntries = zipFile.Entries
                    .Where(x => !x.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !x.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.FullName);

                if (fileNumber == prevSyncPointer.FileNumber && !string.IsNullOrEmpty(prevSyncPointer.LastSyncedEntry))
                {
                    orderedEntries = orderedEntries
                        // skip everything up to the last synced entry
                        .SkipWhile(entry => entry.FullName != prevSyncPointer.LastSyncedEntry)
                        // skip the last synced entry itself
                        .Skip(1);
                }

                foreach (var entry in orderedEntries)
                {
                    using var stream = entry.Open();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var imageBytes = ms.ToArray();
                    var imageName = Path.GetFileName(entry.FullName);
                    var newSyncPointer = new SyncPointer(fileNumber, entry.FullName);
                    yield return new AlbumPhoto(imageBytes, imageName, newSyncPointer.ToString());
                }
            }
        }

        private IEnumerable<(string filePath, int number)> GetSortedZipArchivePaths()
        {
            var zipFiles = Directory.EnumerateFiles(_takeoutDir, "*.zip").ToList();
            var orderedArchives = zipFiles
                .Select(x => new
                {
                    Path = x,
                    Number = ExtractArchiveNumberFromFilePath(x)
                })
                .OrderBy(x => x.Number)
                .Select(x => (x.Path, x.Number))
                .ToList();

            // assert that archive numbers are consecutive from 1 to N

            var expectedNumberSequence = Enumerable.Range(1, orderedArchives.Count);
            var actualNumberSequence = orderedArchives.Select(x => x.Number);

            if (!Enumerable.SequenceEqual(expectedNumberSequence, actualNumberSequence))
                throw new InvalidOperationException($"Archive numbers in {_takeoutDir} directory must be consecutive from 1 to N");

            return orderedArchives;
        }

        private int ExtractArchiveNumberFromFilePath(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!int.TryParse(fileName, out var number))
            {
                throw new FormatException(
                    $"Unrecognized file {filePath}. All files under {_takeoutDir} directory must have file name format of xxx.zip, e.g. 1.zip, 2.zip, etc.");
            }

            return number;
        }

        private struct SyncPointer
        {
            public int FileNumber { get; }

            public string LastSyncedEntry { get; }

            public static SyncPointer Parse(string str)
            {
                if (string.IsNullOrEmpty(str))
                    return new SyncPointer(0, null);

                var parts = str.Split('|');
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException($"Unrecognized sync pointer string: {str}");
                }

                var (numberStr, lastSyncedEntry) = (parts[0], parts[1]);
                if (!int.TryParse(numberStr, out var number))
                {
                    throw new InvalidOperationException($"Unrecognized sync pointer string: {str}");
                }

                return new SyncPointer(number, lastSyncedEntry);
            }

            public SyncPointer(int fileNumber, string lastSyncedEntry)
            {
                FileNumber = fileNumber;
                LastSyncedEntry = lastSyncedEntry;

                if (lastSyncedEntry?.Contains('|') == true)
                    throw new FormatException($"Invalid last synced entry path '{lastSyncedEntry}': the path must not contain the '|' character");
            }

            public override string ToString()
            {
                return $"{FileNumber}|{LastSyncedEntry}";
            }
        }
    }
}
