using BeGeneric.Context;
using BeGeneric.DTOModels;
using BeGeneric.Services.Common;
using BeGeneric.Services.BeGeneric.Exceptions;
using BeGeneric.Utility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;

namespace BeGeneric.Services.Authentication
{
    public interface IPasswordService
    {
        /// <summary>
        /// Hashes the specified password.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <param name="hash">The hash.</param>
        /// <param name="salt">The salt.</param>
        void Hash(string password, out byte[] passwordHash);

        /// <summary>
        /// Verifies the specified password.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <param name="hash">The hash.</param>
        /// <param name="salt">The salt.</param>
        /// <returns><c>true</c> if password has been verified successfully, <c>false</c> otherwise.</returns>
        bool Verify(string password, byte[] passwordHash);

        /// <summary>
        /// Creates and sends reset password link via email.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <returns></returns>
        Task ResetPasswordWithEmail(string email);

        /// <summary>
        /// Verifies the reset code and sets password.
        /// </summary>
        /// <param name="resetRequest">Reset request data</param>
        /// <returns></returns>
        Task VerifyAndResetPassword(ResetRequestDTO resetRequest);
    }

    public class PasswordService : IPasswordService
    {
        protected readonly ControllerDbContext context;
        protected readonly IMessagingService messagingService;
        private readonly AppSettings appSettings;

        public PasswordService(
            ControllerDbContext context,
            IOptions<AppSettings> appSettings,
            IMessagingService messagingService)
        {
            this.context = context;
            this.messagingService = messagingService;
            this.appSettings = appSettings.Value;
        }

        public void Hash(string password, out byte[] passwordHash)
        {
            byte[] salt;
            RandomNumberGenerator.Fill(salt = new byte[16]);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000);
            byte[] hash = pbkdf2.GetBytes(20);

            byte[] hashBytes = new byte[36];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 20);

            passwordHash = hashBytes;
        }

        public bool Verify(string password, byte[] passwordHash)
        {
            byte[] salt = new byte[16];
            Array.Copy(passwordHash, 0, salt, 0, 16);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000);
            byte[] hash = pbkdf2.GetBytes(20);

            for (int i = 0; i < 20; i++)
            {
                if (passwordHash[i + 16] != hash[i])
                {
                    return false;
                }
            }

            return true;
        }

        public async Task VerifyAndResetPassword(ResetRequestDTO resetRequest)
        {
            var reset = await context.ResetPasswords.FindAsync(resetRequest.Id);
            if (reset == null)
            {
                throw new UnauthorizedAccessException("Code already used.");
            }
            if (reset.Expires <= DateTime.UtcNow || reset.CodeHash == null)
            {
                throw new UnauthorizedAccessException("Code is expired or already used.");
            }

            if (!Verify(resetRequest.Code, reset.CodeHash))
            {
                throw new UnauthorizedAccessException("Code is invalid.");
            }

            if (string.IsNullOrEmpty(resetRequest.Password))
            {
                throw new GenericBackendSecurityException(SecurityStatus.BadRequest, "Password is required");
            }

            var user = await context.Accounts.FindAsync(reset.Username);
            resetRequest.Username = user.Username;

            Hash(resetRequest.Password, out byte[] hashedPassword);

            user.PasswordHash = hashedPassword;
            await context.SaveChangesAsync();

            context.ResetPasswords.Remove(reset);
            context.ResetPasswords.RemoveRange(context.ResetPasswords.Where(x => x.Expires <= DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        public async Task ResetPasswordWithEmail(string email)
        {
            var user = await context.Accounts.Where(x => x.EmailAddress.ToLower() == email.ToLower()).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var idAndCode = await CreateResetPassword(user.Username);

            var url = $"{this.appSettings.ClientUrl}/auth/reset-password?id={idAndCode.Item1}&code={HttpUtility.UrlEncode(idAndCode.Item2)}";

            await messagingService.ResetPasswordMessage(user.EmailAddress, url);
        }

        private async Task<Tuple<Guid, string>> CreateResetPassword(string userName)
        {
            string code = RandomString.CreateRandomPassword(15);
            Hash(code, out byte[] hashedPassword);

            var entity = new Models.ResetPassword
            {
                Id = Guid.NewGuid(),
                Username = userName,
                Expires = DateTime.UtcNow.AddHours(1),
                CodeHash = hashedPassword
            };

            context.ResetPasswords.Add(entity);
            await context.SaveChangesAsync();

            return new(entity.Id, code);
        }
    }
}
