using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class IndexModel
    {
        public bool LoggedInYandex { get; set; }

        public SyncProgress Progress { get; set; }

        public List<AlbumMeta> Albums { get; set; }
    }
}
