using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class SyncProgress
    {
        public DateTime? LastSyncedDate { get; set; }

        public bool IsRunning { get; set; }
    }
}
