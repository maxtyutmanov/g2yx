using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class AlbumPhoto
    {
        public byte[] Content { get; set; }

        public string Name { get; set; }

        public DateTime CreationDateTime { get; set; }

        public string Etag { get; set; }
    }
}
