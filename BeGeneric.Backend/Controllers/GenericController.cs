using BeGeneric.Backend.Services.BeGeneric;
using BeGeneric.Backend.Services.BeGeneric.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("")]
    public class GenericController : BaseController
    {
        private readonly IGenericDataService genericService;

        public GenericController(IGenericDataService genericService)
        {
            this.genericService = genericService;
        }

        [HttpGet("{controllerName}/{id}")]
        public async Task<IActionResult> Get(string controllerName, Guid id)
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, id));
        }

        [HttpGet("{controllerName}")]
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC")
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder));
        }

        [HttpPost("{controllerName}/filter")]
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC", ComparerObject? filterObject = null)
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder, filterObject));
        }

        [HttpPost("{controllerName}")]
        public async Task<IActionResult> Post(string controllerName, Dictionary<string, JsonNode> fieldValues)
        {
            return await GetActionResult(this.genericService.Post(this.User, controllerName, fieldValues));
        }

        [HttpPost("{controllerName}/{id}/{relatedEntityName}")]
        public async Task<IActionResult> Post(string controllerName, Guid id, string relatedEntityName, [FromBody] RelatedEntityObject relatedEntity)
        {
            return await GetActionResult(this.genericService.PostRelatedEntity(this.User, controllerName, id, relatedEntityName, relatedEntity));
        }

        [HttpPut("{controllerName}/{id?}")]
        [HttpPatch("{controllerName}/{id?}")]
        public async Task<IActionResult> Patch(string controllerName, Guid? id, Dictionary<string, JsonNode> fieldValues)
        {
            try
            {
                await this.genericService.Patch(this.User, controllerName, id, fieldValues);
                return NoContent();
            }
            catch (GenericBackendSecurityException ex)
            {
                return this.HandleGenericException(ex);
            }
            catch
            {
                throw new Exception("Unknown error");
            }
        }

        [HttpPut("{controllerName}/return/{id?}")]
        [HttpPatch("{controllerName}/return/{id?}")]
        public async Task<IActionResult> PatchReturn(string controllerName, Guid? id, Dictionary<string, JsonNode> fieldValues)
        {
            Guid id1;
            try
            {
                id1 = await this.genericService.Patch(this.User, controllerName, id, fieldValues);
            }
            catch (GenericBackendSecurityException ex)
            {
                return this.HandleGenericException(ex);
            }
            catch
            {
                throw new Exception("Unknown error");
            }

            return await GetActionResult(this.genericService.Get(this.User, controllerName, id1));
        }

        [HttpDelete("{controllerName}/{id}")]
        public async Task<IActionResult> Delete(string controllerName, Guid id)
        {
            return await GetActionResult(this.genericService.Delete(this.User, controllerName, id));
        }

        [HttpDelete("{controllerName}/{id}/{relatedEntityName}/{relatedEntityId}")]
        public async Task<IActionResult> DeleteRelatedEntity(string controllerName, Guid id, string relatedEntityName, Guid relatedEntityId)
        {
            return await GetActionResult(this.genericService.DeleteRelatedEntity(this.User, controllerName, id, relatedEntityName, relatedEntityId));
        }
    }
}
