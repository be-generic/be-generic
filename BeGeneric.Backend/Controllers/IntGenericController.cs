using BeGeneric.Backend.Common;
using BeGeneric.Backend.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.ApiModels;

[ApiController]
[Route("", Order = int.MaxValue)]
public class IntGenericController : GenericController<int>
{
    public IntGenericController(IGenericDataService<int> genericService) : base(genericService)
    { }
}
