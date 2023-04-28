using System.Security.Claims;
using System.Text.Json.Nodes;
using Endpoint = BeGeneric.Backend.Models.Endpoint;

namespace BeGeneric.Backend.Services.BeGeneric
{
    public interface IGenericDataService<T>
    {
        Task<string> Get(ClaimsPrincipal user, string controllerName, T id);

        Task<string> Get(ClaimsPrincipal user, string controllerName, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null, SummaryRequestObject[] summaries = null);

        Task<string> Get(ClaimsPrincipal user, Endpoint endpoint, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", ComparerObject filterObject = null);

        Task<string> Post(ClaimsPrincipal user, string controllerName, Dictionary<string, JsonNode> fieldValues);

        Task PostRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, RelatedEntityObject relatedEntity);

        Task<T> Patch(ClaimsPrincipal user, string controllerName, T? id, Dictionary<string, JsonNode> fieldValues);

        Task Delete(ClaimsPrincipal user, string controllerName, T id);

        Task DeleteRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, T relatedEntityId);
    }
}
