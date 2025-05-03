using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("", Order = int.MaxValue)]
    public class IntGenericController : GenericController<int>
    {
        public IntGenericController(IGenericDataService<int> genericService) : base(genericService)
        { }
    }
}
