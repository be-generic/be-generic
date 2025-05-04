using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BeGeneric.Backend.Services;

public interface IDataService
{ }

public class DataService: IDataService
{
    protected readonly IConfiguration config;
    protected readonly ILogger logger;

    public DataService(IConfiguration config, ILogger logger = null)
    {
        this.config = config;
        this.logger = logger;
    }
}
