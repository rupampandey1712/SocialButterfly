using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// Self-registration always creates regular users. Admin accounts are provisioned
    /// from configuration instead (Admin:Email / Admin:Password, e.g. ADMIN_EMAIL and
    /// ADMIN_PASSWORD in .env): the account is created, or an existing one promoted.
    /// </summary>
    public static class AdminSeeder
    {
        public static async Task EnsureAdminAsync(ApplicationDbContext context, IConfiguration configuration, ILogger logger)
        {
            var email = IdentityRules.NormalizeEmail(configuration["Admin:Email"]);
            var password = configuration["Admin:Password"];
            if (email == null)
            {
                return;
            }

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(password) || password.Length < IdentityRules.MinimumPasswordLength)
                {
                    logger.LogWarning("Admin:Email is set but Admin:Password is missing or shorter than {Length} characters; no admin was created.",
                        IdentityRules.MinimumPasswordLength);
                    return;
                }

                context.Users.Add(new User
                {
                    Name = "Administrator",
                    Username = await FreeUsernameAsync(context, "admin"),
                    Email = email,
                    EmailConfirmed = true,
                    Role = "Admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt())
                });
                logger.LogInformation("Created admin account {Email}", IdentityRules.Mask(email));
            }
            else if (user.Role != "Admin")
            {
                user.Role = "Admin";
                logger.LogInformation("Promoted {Email} to admin", IdentityRules.Mask(email));
            }
            else
            {
                return;
            }

            await context.SaveChangesAsync();
        }

        private static async Task<string> FreeUsernameAsync(ApplicationDbContext context, string seed)
        {
            var candidate = seed;
            for (var i = 1; await context.Users.AnyAsync(u => u.Username == candidate); i++)
            {
                candidate = $"{seed}{i}";
            }
            return candidate;
        }
    }
}
