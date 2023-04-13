﻿using BeGeneric.Backend.Services.BeGeneric;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("")]
    public class IntGenericController : GenericController<int>
    {
        public IntGenericController(IGenericDataService<int> genericService) : base(genericService)
        { }
    }
}
