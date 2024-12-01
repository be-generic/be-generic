using System;
using System.Collections.Generic;
using System.Linq;

namespace BeGeneric.Backend.GenericModels
{
    public class PagedResult<T>
    {
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public Dictionary<string, object> Aggregation { get; set; }
        public List<T> Data { get; set; }
    }

    public static class PagedResultExtensions
    {
        public static PagedResult<R> ToClass<R, Q>(this PagedResult<Q> pagedResult, Func<Q, R> mapping)
        {
            return new PagedResult<R>
            {
                RecordsTotal = pagedResult.RecordsTotal,
                RecordsFiltered = pagedResult.RecordsFiltered,
                Data = pagedResult.Data.Select(x => mapping(x)).ToList(),
                Page = pagedResult.Page,
                PageSize = pagedResult.PageSize
            };
        }
    }
}
