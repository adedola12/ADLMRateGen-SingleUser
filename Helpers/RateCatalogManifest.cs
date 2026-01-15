using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADLMRateGen.Helpers
{
    public class RateCatalogManifest
    {
        public string Zone { get; set; }
        public int CatalogVersion { get; set; }
        public string Message { get; set; }
        public DateTime PublishedAtUtc { get; set; }
    }

}
