using BeGeneric.Backend.Services.GenericBackend;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers;

[ApiController]
[Route("", Order = int.MaxValue)]
public class GuidGenericController(IGenericDataService<Guid> genericService) : GenericController<Guid>(genericService)
{
}
