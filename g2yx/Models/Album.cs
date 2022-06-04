using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class Album
    {
        public string Title { get; }

        public IReadOnlyCollection<string> PhotoIds { get; }

        public Album(string title, IReadOnlyCollection<string> photoIds)
        {
            Title = title;
            PhotoIds = photoIds;
        }
    }
}
