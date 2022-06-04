using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class AlbumMeta
    {
        public string Id { get; }

        public string Title { get; }

        public AlbumMeta(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }
}
