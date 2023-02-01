using BeGeneric.Context;
using BeGeneric.GenericModels;
using BeGeneric.Models;
using BeGeneric.Services.BeGeneric.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BeGeneric.Services.Authentication
{
    public interface IAccountService
    {
        Task<Account> GetOneAccount(string userName);

        Task<PagedResult<Account>> GetAllAccountsPaged(int? page = null, int pageSize = 10, string filter = null);

        Task UpdateAccount(string userName, NewAccount account);

        Task UpdateOwnAccount(ClaimsPrincipal currentUser, NewAccount account);

        Task CreateNewAccount(NewAccount account);

        Task DeleteAccount(string userName);
    }

    public class AccountService : IAccountService
    {
        private readonly ILogger<AccountService> logger;
        private readonly ControllerDbContext context;
        private readonly IConfiguration config;
        private readonly IPasswordService passwordService;

        public AccountService(ILogger<AccountService> logger,
            ControllerDbContext context,
            IConfiguration config,
            IPasswordService passwordService)
        {
            this.logger = logger;
            this.context = context;
            this.config = config;
            this.passwordService = passwordService;
        }

        public async Task<Account> GetOneAccount(string userName)
        {
            return await this.context.Accounts.Include(x => x.Role).FirstOrDefaultAsync(x => x.Username == userName);
        }


        public async Task<PagedResult<Account>> GetAllAccountsPaged(int? page = null, int pageSize = 10, string filter = null)
        {
            var accounts = this.context.Accounts.Include(x => x.Role).AsQueryable();

            int totalCount = await accounts.CountAsync();

            if (filter != null)
            {
                accounts = accounts.
                    Where(x => x.Username.Contains(filter) || (x.EmailAddress != null && x.EmailAddress.Contains(filter)));
            }

            int filteredTotalCount = await accounts.CountAsync();

            if (page != null)
            {
                accounts = accounts
                    .Skip((page.Value - 1) * pageSize)
                    .Take(pageSize);
            }

            return new PagedResult<Account>()
            {
                Data = await accounts.ToListAsync(),
                Page = page ?? 1,
                PageSize = pageSize,
                RecordsFiltered = filteredTotalCount,
                RecordsTotal = totalCount
            };
        }

        public async Task UpdateAccount(string userName, NewAccount account)
        {
            if (account.Username != null && account.Username != userName)
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Username mismatch");
            }

            if (!string.IsNullOrEmpty(account.EmailAddress))
            {
                try
                {
                    MailAddress m = new(account.EmailAddress);
                }
                catch (FormatException)
                {
                    throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Email address is not valid");
                }
            }

            Account user = await context.Accounts.FirstOrDefaultAsync(u => u.Username.ToLower() == account.Username.ToLower());

            if (user == null)
            {
                throw new GenericBackendSecurityException(SecurityStatus.NotFound);
            }

            Role role = null;
            if (account.Role != Guid.Empty && account.Role != null)
            {
                role = await context.Roles.FindAsync(account.Role);
            }
            else
            {
                role = await context.Roles.Where(x => (x.RoleName == "admin" && account.IsAdmin) || (x.RoleName != "admin" && !account.IsAdmin)).FirstOrDefaultAsync();
            }

            user.EmailAddress = account.EmailAddress;
            user.RoleId = role.Id;

            if (!string.IsNullOrEmpty(account.Password))
            {
                passwordService.Hash(account.Password, out byte[] hashedPassword);
                user.PasswordHash = hashedPassword;
            }

            await this.context.SaveChangesAsync();
        }

        public async Task UpdateOwnAccount(ClaimsPrincipal currentUser, NewAccount account)
        {
            ClaimsIdentity userData = currentUser.Identity as ClaimsIdentity;
            string userName = new(userData.FindFirst("id").Value);

            if (account.Username != null && account.Username != userName)
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Username mismatch");
            }

            Account user = await context.Accounts.FirstOrDefaultAsync(u => u.Username.ToLowerInvariant() == userName.ToLowerInvariant());

            if (user == null)
            {
                throw new GenericBackendSecurityException(SecurityStatus.NotFound);
            }

            if (!string.IsNullOrEmpty(account.EmailAddress))
            {
                try
                {
                    MailAddress m = new(account.EmailAddress);
                }
                catch (FormatException)
                {
                    throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Email address is not valid");
                }

                user.EmailAddress = account.EmailAddress;
            }

            if (!string.IsNullOrEmpty(account.Password))
            {
                passwordService.Hash(account.Password, out byte[] hashedPassword);
                user.PasswordHash = hashedPassword;
            }

            await this.context.SaveChangesAsync();
        }

        public async Task CreateNewAccount(NewAccount account)
        {
            if (string.IsNullOrEmpty(account.Username))
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Username is required");
            }

            // TODO: Check for complexity, not only existance
            if (string.IsNullOrEmpty(account.Password))
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Password is required");
            }

            if (string.IsNullOrEmpty(account.EmailAddress))
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Email address is required");
            }
            else
            {
                try
                {
                    MailAddress m = new(account.EmailAddress);
                }
                catch (FormatException)
                {
                    throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Email address is not valid");
                }
            }

            Role role = null;
            if (account.Role != Guid.Empty && account.Role != null)
            {
                role = await context.Roles.FindAsync(account.Role);
            }
            else
            {
                role = await context.Roles.Where(x => (x.RoleName == "admin" && account.IsAdmin) || (x.RoleName != "admin" && !account.IsAdmin)).FirstOrDefaultAsync();
            }

            Account user = await context.Accounts.FirstOrDefaultAsync(u => u.EmailAddress.ToLower() == account.Username.ToLower() ||
                                u.Username.ToLower() == account.Username.ToLower());

            if (user != null)
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Email address or user name are already used");
            }

            Account newAccount = new()
            {
                EmailAddress = account.EmailAddress,
                RoleId = role.Id,
                Username = account.Username
            };

            passwordService.Hash(account.Password, out byte[] hashedPassword);
            newAccount.PasswordHash = hashedPassword;

            await this.context.AddAsync(newAccount);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAccount(string userName)
        {
            Account account = await this.context.Accounts.Include(x => x.Role).FirstOrDefaultAsync(x => x.Username == userName);
            if (account == null)
            {
                throw new GenericBackendSecurityException(SecurityStatus.NotFound);
            }

            if (account.Role.RoleName == "admin")
            {
                if (!await this.context.Accounts.AnyAsync(x => x.Username != userName && x.Role.RoleName == "admin"))
                {
                    throw new GenericBackendSecurityException(SecurityStatus.Conflict, "User is the last administrator and cannot be deleted");
                }
            }

            this.context.Accounts.Remove(account);
            await this.context.SaveChangesAsync();
        }
    }
}
