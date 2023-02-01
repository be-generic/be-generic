using BeGeneric.Context;
using BeGeneric.DTOModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BeGeneric.Services.Common
{
    public interface IAutocompleteService
    {
        //Task<List<AutocompleteDTO>> GetXXX(string filter);
    }

    public class AutocompleteService : IAutocompleteService
    {
        private readonly ILogger<AutocompleteService> logger;
        private readonly EntityDbContext context;
        private readonly IConfiguration config;

        public AutocompleteService(
            ILogger<AutocompleteService> logger,
            EntityDbContext context,
            IConfiguration config)
        {
            this.logger = logger;
            this.context = context;
            this.config = config;
        }

        // TODO: Find a generic solution for this
        //public async Task<List<AutocompleteDTO>> GetXXX(string filter)
        //{
        //    return await XXX
        //        .Select(x => new AutocompleteDTO()
        //        {
        //            Text = x.XXX,
        //            Id = x.Id.ToString()
        //        })
        //        .FilterAutocomplete(filter)
        //        .OrderBy(x => x.Text)
        //        .Take(10)
        //        .ToListAsync();
        //}
    }

    public static class AutocompleteModelExtensions
    {
        public static IQueryable<AutocompleteDTO> FilterAutocomplete(this IQueryable<AutocompleteDTO> query, string filter)
        {
            if (filter != null)
            {
                string[] filterWords = filter.Split(" ");

                foreach (var term in filterWords)
                {
                    query = query.Where(x => x.Text.Contains(term));
                }
            }

            return query;
        }
    }
}
