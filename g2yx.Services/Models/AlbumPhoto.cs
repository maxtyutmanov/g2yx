using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class AlbumPhoto
    {
        private static Regex FolderHintRegex = new Regex(@"^IMG_(?<date>\d{8}).+$");

        public byte[] Content { get; }

        public string Name { get; }

        public string SyncPointer { get; }

        public string Folder { get; }

        public AlbumPhoto(byte[] content, string name, string syncPointer)
        {
            Content = content;
            SyncPointer = syncPointer;

            var folderHintMatch = FolderHintRegex.Match(name);
            if (folderHintMatch.Success)
            {
                Folder = folderHintMatch.Groups["date"].Value;
                Name = $"{Folder}/{name}";
            }
            else
            {
                Name = name;
            }
        }
    }
}
