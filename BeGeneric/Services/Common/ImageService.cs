using BeGeneric.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeGeneric.Services.Common
{
    public interface IImageService
    {
    }

    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> logger;
        private readonly EntityDbContext context;
        private readonly IConfiguration config;
        private readonly AppSettings appSettings;

        public ImageService(
            ILogger<ImageService> logger,
            EntityDbContext context,
            IConfiguration config,
            IOptions<AppSettings> appSettings)
        {
            this.logger = logger;
            this.context = context;
            this.config = config;
            this.appSettings = appSettings.Value;
        }

        // Here you can implement own image saving logic
    }
}
