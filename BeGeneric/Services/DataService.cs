using AutoMapper;
using BeGeneric.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BeGeneric.Services
{
    public interface IDataService
    { }

    public class DataService: IDataService
    {
        protected readonly ControllerDbContext context;
        protected readonly IConfiguration config;
        protected readonly IMapper mapper;
        protected readonly ILogger logger;

        public DataService(ControllerDbContext context, IConfiguration config, IMapper mapper, ILogger logger = null)
        {
            this.context = context;
            this.config = config;
            this.mapper = mapper;
            this.logger = logger;
        }
    }
}
