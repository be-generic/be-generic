namespace BeGeneric.Backend.Services.BeGeneric
{
    public class SummaryRequestObject
    {
        public string Property { get; set; }
        public string SummaryType { get; set; }
    }

    public static class SummaryTypes
    {
        public const string SUM = "sum";
        public const string AVG = "avg";
        public const string MAX = "max";
        public const string MIN = "min";
        public const string COUNT = "count";
        public const string COUNT_DISTINCT = "count_distinct";
    }
}
