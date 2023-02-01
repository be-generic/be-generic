using BeGeneric.Models;
using BeGeneric.Services.BeGeneric;
using BeGeneric.Services.BeGeneric.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BeGeneric.Controllers
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
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", string filter = null)
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder, filter));
        }

        [HttpGet("{controllerName1}/{controllerName2?}/{controllerName3?}/{controllerName4?}")]
        public async Task<IActionResult> Get(string controllerName1, string controllerName2 = "", string controllerName3 = "", string controllerName4 = "", int? page = null, int? pageSize = null, string sortProperty = null, string sortOrder = null)
        {
            string endpointPath = controllerName1;
            if (!string.IsNullOrEmpty(controllerName2))
            {
                endpointPath += "/" + controllerName2;
                if (!string.IsNullOrEmpty(controllerName3))
                {
                    endpointPath += "/" + controllerName3;
                    if (!string.IsNullOrEmpty(controllerName4))
                    {
                        endpointPath += "/" + controllerName4;
                    }
                }
            }

            Endpoint endpoint = await this.genericService.GetEndpoint(endpointPath);
            if (endpoint == null)
            {
                return NotFound();
            }

            ComparerObject filterObject = null;

            if (!string.IsNullOrEmpty(endpoint.Filter))
            {
                string filterDefinition = endpoint.Filter;
                foreach (var key in Request.Query.Keys)
                {
                    filterDefinition = endpoint.Filter.Replace($"${key}$", JsonSerializer.Serialize(Request.Query[key].ToString()));
                }

                filterObject = JsonSerializer.Deserialize<ComparerObject>(filterDefinition, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            }

            return await GetActionResult(this.genericService.Get(this.User, 
                endpoint, 
                page ?? endpoint.DefaultPageNumber, 
                (pageSize ?? endpoint.DefaultPageNumber) ?? 10, 
                sortProperty ?? endpoint.DefaultSortOrderProperty, 
                (sortOrder ?? endpoint.DefaultSortOrder) ?? "ASC",
                filterObject: filterObject));
        }

        [HttpPost("{controllerName}/filter")]
        public async Task<IActionResult> Get(string controllerName, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null)
        {
            return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize, sortProperty, sortOrder, null, filterObject));
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
