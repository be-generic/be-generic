using AutoMapper;
using BeGeneric.Context;
using BeGeneric.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BeGeneric.Services.Authentication
{
    public interface IAuthenticationService : IDataService
    {
        /// <summary>
        /// Generates the JSON web token.
        /// </summary>
        /// <param name="userInfo">User info.</param>
        /// <returns>Generated JSON web token in string format.</returns>
        string GenerateJSONWebToken(UserModel userInfo);

        /// <summary>
        /// Authenticates the user using given login.
        /// </summary>
        /// <param name="login">Login information of the user to authenticate.</param>
        /// <returns>User model with user data if the authentication was successful.</returns>
        Task<UserModel> Authenticate(LoginModel login);

        /// <summary>
        /// Logout process.
        /// </summary>
        /// <returns>true.</returns>
        bool Logout();
    }

    public class AuthenticationService : DataService, IAuthenticationService
    {
        private readonly IPasswordService passwordService;

        public AuthenticationService(
            ControllerDbContext context,
            IMapper mapper,
            IConfiguration config,
            IPasswordService passwordService)
            : base(context, config, mapper, null)
        {
            this.passwordService = passwordService;
        }

        public string GenerateJSONWebToken(UserModel userInfo)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new List<Claim>()
            {
                new Claim("id", userInfo.UserName.ToString()),
                new Claim(ClaimTypes.Email, userInfo.EmailAddress),
                new Claim(ClaimTypes.NameIdentifier, userInfo.UserName),
            };

            foreach (string role in userInfo.Roles)
            {
                claims.Add(new Claim("role", role));
            }

            var key = Encoding.UTF8.GetBytes(this.config["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = this.config["Jwt:Issuer"],
                Audience = this.config["Jwt:Issuer"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            foreach (string role in userInfo.Roles)
            {
                tokenDescriptor.Subject.AddClaim(new Claim(role.ToLowerInvariant(), "true"));
            }

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        public virtual async Task<UserModel> Authenticate(LoginModel login)
        {
            Account user = await context.Accounts
                .Include(x => x.Role)
                .FirstOrDefaultAsync(u => u.EmailAddress.ToLower() == login.UserName.ToLower() ||
                    u.Username.ToLower() == login.UserName.ToLower());

            if (user == null || !passwordService.Verify(login.Password, user.PasswordHash))
            {
                return null;
            }

            return new UserModel()
            {
                EmailAddress = user.EmailAddress,
                Roles = new string[] { user.Role.RoleName },
                UserName = user.Username
            };
        }

        public bool Logout()
        {
            return true;
        }
    }
}
