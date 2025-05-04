using BeGeneric.Backend.Common.Models;
using BeGeneric.Backend.Database;

namespace BeGeneric.Backend.Services.GenericBackend
{
    public class DataRequestObject : ComparerObject
    {
        public SummaryRequestObject[]? Summaries { get; set; }
    }
}
