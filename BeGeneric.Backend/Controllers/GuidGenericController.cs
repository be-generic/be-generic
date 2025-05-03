using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("", Order = int.MaxValue)]
    public class GuidGenericController : GenericController<Guid>
    {
        public GuidGenericController(IGenericDataService<Guid> genericService) : base(genericService)
        { }
    }
}
