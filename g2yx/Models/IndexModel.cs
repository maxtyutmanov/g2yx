using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace g2yx.Models
{
    public class IndexModel
    {
        public string YandexAccessToken { get; set; }

        public bool LoggedInYandex => !string.IsNullOrEmpty(YandexAccessToken);

        public SyncProgress Progress { get; set; }
    }
}
