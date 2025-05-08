using BeGeneric.Backend.ApiModels;
using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Exceptions;
using BeGeneric.Backend.Common.Models;
using BeGeneric.Backend.Database;
using BeGeneric.Backend.Services.GenericBackend;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BeGeneric.Backend.Controllers;

[ApiController]
[Route("", Order = int.MaxValue)]
public class GenericController<T> : BaseController
{
    private readonly IGenericDataService<T> genericService;

    public GenericController(IGenericDataService<T> genericService)
    {
        this.genericService = genericService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(T id)
    {
        return await GetActionResult(genericService.Get(User, ControllerContext.RouteData.Values["controller"].ToString(), id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC")
    {
        return await GetActionResult(genericService.Get(User, ControllerContext.RouteData.Values["controller"].ToString(), page, pageSize, sortProperty, sortOrder));
    }

    [HttpPost("filter")]
    public async Task<IActionResult> GetWithFilter(int? page = null, int pageSize = 10, string? sortProperty = null, string? sortOrder = "ASC", DataRequestObject? dataRequestObject = null)
    {
        if (!string.IsNullOrEmpty(dataRequestObject?.Property) || !string.IsNullOrEmpty(dataRequestObject?.Conjunction))
        {
            return await GetActionResult(genericService.Get(User, ControllerContext.RouteData.Values["controller"].ToString(), page, pageSize, sortProperty, sortOrder, dataRequestObject, dataRequestObject?.Summaries));
        }
        else
        {
            ComparerObject comparer = null;

            if (dataRequestObject?.Filter != null)
            {
                comparer = JsonSerializer.Deserialize<ComparerObject>(dataRequestObject?.Filter.ToString(), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            }

            return await GetActionResult(genericService.Get(User, ControllerContext.RouteData.Values["controller"].ToString(), page, pageSize, sortProperty, sortOrder, comparer, dataRequestObject?.Summaries));
        }
    }

    [HttpPost]
    [ReadableBodyStream]
    public async Task<IActionResult> Post(object fieldValues)
    {
        Dictionary<string, JsonNode> actualFeldValues;

        HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
        using (var stream = new StreamReader(Request.Body))
        {
            var body = await stream.ReadToEndAsync();
            actualFeldValues = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(body);
        }

        return await GetActionResult(genericService.Post(User, ControllerContext.RouteData.Values["controller"].ToString(), actualFeldValues));
    }

    [HttpPost("{id}/{relatedEntityName}")]
    public async Task<IActionResult> PostRelatedEntity(T id, [FromBody] RelatedEntityObject<T> relatedEntity)
    {
        string relatedEntityName = ControllerContext.ActionDescriptor.ActionName.Substring("post-".Length);
        return await GetActionResult(genericService.PostRelatedEntity(User, ControllerContext.RouteData.Values["controller"].ToString(), id, relatedEntityName, relatedEntity));
    }

    [HttpPut("{id?}")]
    [HttpPatch("{id?}")]
    [ReadableBodyStream]
    public async Task<IActionResult> Patch(T? id, object fieldValues)
    {
        Dictionary<string, JsonNode> actualFeldValues;

        HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
        using (var stream = new StreamReader(Request.Body))
        {
            var body = await stream.ReadToEndAsync();
            actualFeldValues = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(body);
        }

        try
        {
            await genericService.Patch(User, ControllerContext.RouteData.Values["controller"].ToString(), id, actualFeldValues);
            return NoContent();
        }
        catch (GenericBackendSecurityException ex)
        {
            return HandleGenericException(ex);
        }
        catch
        {
            throw new Exception("Unknown error");
        }
    }

    [HttpPut("return/{id?}")]
    [HttpPatch("return/{id?}")]
    [ReadableBodyStream]
    public async Task<IActionResult> PatchReturn(T? id, object fieldValues)
    {
        Dictionary<string, JsonNode> actualFeldValues;

        HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
        using (var stream = new StreamReader(Request.Body))
        {
            var body = await stream.ReadToEndAsync();
            actualFeldValues = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(body);
        }

        T id1;
        try
        {
            id1 = await genericService.Patch(User, ControllerContext.RouteData.Values["controller"].ToString(), id, actualFeldValues);
        }
        catch (GenericBackendSecurityException ex)
        {
            return HandleGenericException(ex);
        }
        catch
        {
            throw new Exception("Unknown error");
        }

        return await GetActionResult(genericService.Get(User, ControllerContext.RouteData.Values["controller"].ToString(), id1));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(T id)
    {
        return await GetActionResult(genericService.Delete(User, ControllerContext.RouteData.Values["controller"].ToString(), id));
    }

    [HttpDelete("{id}/{relatedEntityName}/{relatedEntityId}")]
    public async Task<IActionResult> DeleteRelatedEntity(T id, T relatedEntityId)
    {
        string relatedEntityName = ControllerContext.ActionDescriptor.ActionName.Substring("delete-".Length);
        return await GetActionResult(genericService.DeleteRelatedEntity(User, ControllerContext.RouteData.Values["controller"].ToString(), id, relatedEntityName, relatedEntityId));
    }
}

public class ReadableBodyStreamAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        context.HttpContext.Request.EnableBuffering();
    }
}
