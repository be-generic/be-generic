using BeGeneric.Backend.Common.Models;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace BeGeneric.Backend.Common;

public interface IGenericDataService<T>
{
    Task<string> Get(ClaimsPrincipal user, string controllerName, T id);

    Task<string> Get(ClaimsPrincipal user, string controllerName, int? page = null, int pageSize = 10, string sortProperty = null, string sortOrder = "ASC", IComparerObject? filterObject = null, SummaryRequestObject[] summaries = null);

    Task<string> Post(ClaimsPrincipal user, string controllerName, Dictionary<string, JsonNode> fieldValues);

    Task PostRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, RelatedEntityObject<T> relatedEntity);

    Task<T> Patch(ClaimsPrincipal user, string controllerName, T? id, Dictionary<string, JsonNode> fieldValues);

    Task Delete(ClaimsPrincipal user, string controllerName, T id);

    Task DeleteRelatedEntity(ClaimsPrincipal user, string controllerName, T id, string relatedEntityName, T relatedEntityId);
}
