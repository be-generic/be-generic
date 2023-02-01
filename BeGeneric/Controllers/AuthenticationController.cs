using BeGeneric.DTOModels;
using BeGeneric.Models;
using BeGeneric.Services.Authentication;
using BeGeneric.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BeGeneric.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService authenticationService;
        private readonly IPasswordService passwordService;
        private readonly IMemoryCacheService memoryCacheService;
        private readonly IMessagingService messagingService;

        public AuthenticationController(IAuthenticationService authenticationService,
                                        IMemoryCacheService memoryCacheService,
                                        IPasswordService passwordService,
                                        IMessagingService messagingService)
        {
            this.authenticationService = authenticationService;
            this.passwordService = passwordService;
            this.memoryCacheService = memoryCacheService;
            this.messagingService = messagingService;
        }

        [AllowAnonymous]
        [HttpPost(Name = "Login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            IActionResult response = Unauthorized();
            UserModel user = await authenticationService.Authenticate(login);

            if (user != null)
            {
                var tokenString = authenticationService.GenerateJSONWebToken(user);
                response = Ok(new { token = tokenString, isAdmin = user.Roles.Any(x => x.ToLowerInvariant() == "admin") });

                Response.Headers.Authorization = new StringValues("Bearer " + tokenString);
            }

            return response;
        }

        [AllowAnonymous]
        [HttpPost("code", Name = "VerifyPasswordReset")]
        public async Task<IActionResult> VerifyPasswordReset(ResetRequestDTO data)
        {
            try
            {
                await passwordService.VerifyAndResetPassword(data);
                return Ok();
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet(Name = "RequestResetPassword")]
        public async Task<IActionResult> RequestResetPassword([FromQuery] string email)
        {
            if (!new EmailAddressAttribute().IsValid(email))
            {
                return BadRequest("Invalid username");
            }

            try
            {
                await passwordService.ResetPasswordWithEmail(email);
            }
            catch (UnauthorizedAccessException)
            {
                return BadRequest("Invalid username");
            }
            catch
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }
    }
}
