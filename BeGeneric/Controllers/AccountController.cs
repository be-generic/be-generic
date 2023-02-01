using BeGeneric.GenericModels;
using BeGeneric.Models;
using BeGeneric.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace BeGeneric.Controllers
{
    [ApiController]
    [Route("account")]
    public class AccountController : BaseController
    {
        private readonly IAccountService accountService;

        public AccountController(IAccountService accountService)
        {
            this.accountService = accountService;
        }

        [Authorize(Roles = "admin")]
        [HttpGet("{userName}")]
        public async Task<IActionResult> Get(string userName)
        {
            var account = await this.accountService.GetOneAccount(userName);

            if (account == null)
            {
                return NotFound();
            }

            return Ok(new UserModel
                {
                    UserName = account.Username,
                    EmailAddress = account.EmailAddress,
                    Roles = new string[] { account.Role.RoleName }
                });
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> Get(int? page = null, int pageSize = 10, string filter = null)
        {
            var accounts = await this.accountService.GetAllAccountsPaged(page, pageSize, filter);

            return Ok(accounts.ToClass(x => new UserModel
                {
                    UserName = x.Username,
                    EmailAddress = x.EmailAddress,
                    Roles = new string[] { x.Role.RoleName }
                }));
        }

        [Authorize(Roles = "admin")]
        [HttpPatch("{userName}")]
        [HttpPut("{userName}")]
        public async Task<IActionResult> Patch(string userName, NewAccount account)
        {
            return await this.GetActionResult(this.accountService.UpdateAccount(userName, account));
        }

        [Authorize]
        [HttpPatch("own/")]
        [HttpPut("own/")]
        public async Task<IActionResult> PatchOwn(NewAccount account)
        {
            return await this.GetActionResult(this.accountService.UpdateOwnAccount(this.User, account));
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> Post(NewAccount account)
        {
            return await this.GetActionResult(this.accountService.CreateNewAccount(account));
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{userName}")]
        public async Task<IActionResult> Delete(string userName)
        {
            return await this.GetActionResult(accountService.DeleteAccount(userName));
        }
    }
}
