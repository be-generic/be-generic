using BeGeneric.Backend.Services.BeGeneric;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("")]
    public class GuidGenericController : GenericController<Guid>
    {
        public GuidGenericController(IGenericDataService<Guid> genericService) : base(genericService)
        { }
    }
}
