using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class AlbumPhoto
    {
        public byte[] Content { get; }

        public string Name { get; }

        public string SyncPointer { get; }

        public AlbumPhoto(byte[] content, string name, string syncPointer)
        {
            Content = content;
            Name = name;
            SyncPointer = syncPointer;
        }
    }
}
