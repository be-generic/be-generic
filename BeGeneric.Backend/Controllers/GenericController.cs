using BeGeneric.Backend.Services.BeGeneric;
using BeGeneric.Backend.Services.BeGeneric.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeGeneric.Backend.Controllers
{
    [ApiController]
    [Route("")]
    public class GenericController<T> : BaseController
    {
        private readonly IGenericDataService<T> genericService;

        public GenericController(IGenericDataService<T> genericService)
        {
            this.genericService = genericService;
        }

        [HttpGet("{controllerName}/{id}")]
        public async Task<IActionResult> Get(string controllerName, T id)
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, id));
        }

        [HttpGet("{controllerName}")]
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC")
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder));
        }

        [HttpPost("{controllerName}/filter")]
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC", DataRequestObject? dataRequestObject = null)
        {
            if (!string.IsNullOrEmpty(dataRequestObject?.Property) || !string.IsNullOrEmpty(dataRequestObject?.Conjunction))
            {
                return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder, dataRequestObject, dataRequestObject?.Summaries));
            }
            else
            {
                ComparerObject comparer = null;

                if (dataRequestObject?.Filter != null)
                {
                    comparer = JsonSerializer.Deserialize<ComparerObject>(dataRequestObject?.Filter.ToString(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                }

                return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder, comparer, dataRequestObject?.Summaries));
            }
        }

        [HttpPost("{controllerName}")]
        public async Task<IActionResult> Post(string controllerName, Dictionary<string, JsonNode> fieldValues)
        {
            return await GetActionResult(this.genericService.Post(this.User, controllerName, fieldValues));
        }

        [HttpPost("{controllerName}/{id}/{relatedEntityName}")]
        public async Task<IActionResult> Post(string controllerName, T id, string relatedEntityName, [FromBody] RelatedEntityObject relatedEntity)
        {
            return await GetActionResult(this.genericService.PostRelatedEntity(this.User, controllerName, id, relatedEntityName, relatedEntity));
        }

        [HttpPut("{controllerName}/{id?}")]
        [HttpPatch("{controllerName}/{id?}")]
        public async Task<IActionResult> Patch(string controllerName, T? id, Dictionary<string, JsonNode> fieldValues)
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
        public async Task<IActionResult> PatchReturn(string controllerName, T? id, Dictionary<string, JsonNode> fieldValues)
        {
            T id1;
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
        public async Task<IActionResult> Delete(string controllerName, T id)
        {
            return await GetActionResult(this.genericService.Delete(this.User, controllerName, id));
        }

        [HttpDelete("{controllerName}/{id}/{relatedEntityName}/{relatedEntityId}")]
        public async Task<IActionResult> DeleteRelatedEntity(string controllerName, T id, string relatedEntityName, T relatedEntityId)
        {
            return await GetActionResult(this.genericService.DeleteRelatedEntity(this.User, controllerName, id, relatedEntityName, relatedEntityId));
        }
    }
}
