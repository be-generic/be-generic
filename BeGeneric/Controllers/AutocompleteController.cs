using BeGeneric.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Controllers
{
    [Route("autocomplete")]
    [ApiController]
    public class AutocompleteController : ControllerBase
    {
        private readonly IAutocompleteService autocompleteService;

        public AutocompleteController(IAutocompleteService autocompleteService)
            : base()
        {
            this.autocompleteService = autocompleteService;
        }
    }
}
