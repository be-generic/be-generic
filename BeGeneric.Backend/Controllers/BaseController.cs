using BeGeneric.Backend.Services.GenericBackend.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BeGeneric.Backend.Controllers
{
    public class BaseController: ControllerBase
    {
        protected async Task<IActionResult> GetActionResult(Task action, Func<IActionResult> defaultResult = null)
        {
            try
            {
                await action;

                if (defaultResult != null)
                {
                    return defaultResult();
                }

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

        protected async Task<IActionResult> GetActionResult<T>(Task<T> action)
        {
            try
            {
                var tmp = await action;
                if (tmp != null)
                {
                    return Content(tmp.ToString(), "application/json");
                }

                return Ok();
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

        protected IActionResult HandleGenericException(GenericBackendSecurityException exception)
        {
            return exception.SecurityStatus switch
            {
                SecurityStatus.Ok => Ok(),
                SecurityStatus.BadRequest => BadRequest(exception.ErrorObject),
                SecurityStatus.NotFound => NotFound(),
                SecurityStatus.Unauthorised => Unauthorized(),
                SecurityStatus.Forbidden => Forbid(),
                _ => BadRequest(),
            };
        }
    }
}
