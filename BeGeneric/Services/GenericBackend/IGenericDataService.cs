using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Endpoint = BeGeneric.Models.Endpoint;

namespace BeGeneric.Services.BeGeneric
{
    public interface IGenericDataService
    {
        Task<string> Get(ClaimsPrincipal user, string controllerName, Guid id);

        Task<string> Get(ClaimsPrincipal user, string controllerName, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null);

        Task<string> Get(ClaimsPrincipal user, Endpoint endpoint, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null);

        Task<string> Post(ClaimsPrincipal user, string controllerName, Dictionary<string, JsonNode> fieldValues);

        Task PostRelatedEntity(ClaimsPrincipal user, string controllerName, Guid id, string relatedEntityName, RelatedEntityObject relatedEntity);

        Task<Guid> Patch(ClaimsPrincipal user, string controllerName, Guid? id, Dictionary<string, JsonNode> fieldValues);

        Task Delete(ClaimsPrincipal user, string controllerName, Guid id);

        Task DeleteRelatedEntity(ClaimsPrincipal user, string controllerName, Guid id, string relatedEntityName, Guid relatedEntityId);
        
        Task<Endpoint> GetEndpoint(string endpointPath);
    }
}
