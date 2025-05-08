using BeGeneric.Backend.Common;
using BeGeneric.Backend.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.ApiModels;

[ApiController]
[Route("", Order = int.MaxValue)]
public class GuidGenericController : GenericController<Guid>
{
    public GuidGenericController(IGenericDataService<Guid> genericService) : base(genericService)
    { }
}
