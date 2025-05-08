using BeGeneric.Backend.ApiModels;
using BeGeneric.Backend.Common;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers;

[Route("graphql")]
public class GraphQLController: BaseController
{
    private readonly IGenericDataService<Guid> genericService;

    public GraphQLController(IGenericDataService<Guid> genericDataService) : base()
    {
        this.genericService = genericDataService;
    }

    [HttpPost]
    public async Task<IActionResult> GetGraphQL([FromBody] GraphQLRequest request)
    {
        var (controllerName, page, pageSize, sortProperty, sortOrder, filter, properties) = Utility.GraphQLToGenericQueryConverter.ConvertGraphQLToGetParameters(request.Query);
        return await GetActionResult(this.genericService.Get(this.User, controllerName, page, pageSize ?? 10, sortProperty, sortOrder, filter, null, properties));
    }
}
