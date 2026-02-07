using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace IMS.Infrastructure.DBSeeder
{
    public class Seeder
    {
        private readonly ILogger<Seeder> _logger;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMS_DbContext _ims;
        public Seeder(IMS_DbContext ims,ILogger<Seeder> logger, RoleManager<IdentityRole<Guid>> roleManager, UserManager<AppUser> userManager)
        {
            _logger = logger;
            _roleManager = roleManager;
            _userManager = userManager;
            _ims = ims;
        }

        public async Task RoleSeeder()
        {
            _logger.LogInformation("Seeding...");

            if (await _roleManager.Roles.AnyAsync())
            {
                _logger.LogInformation("Roles already exist. Skipping seeding.");
                return;
            }

            var roles = Enum.GetValues(typeof(Roles))
                .Cast<Roles>()
                .Select(role => new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = role.ToString(),
                    NormalizedName = role.ToString().ToUpper()
                })
                .ToList();

            foreach (var role in roles)
            {
                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Role {RoleName} created successfully.", role.Name);
                }
                else
                {
                    _logger.LogError("Error creating role {RoleName}: {@Errors}",
                        role.Name, result.Errors);
                }
            }
            _logger.LogInformation("Roles seeding completed.");
        }



        public async Task AdminSeeder(IConfiguration configuration)
        {
            _logger.LogInformation("Seeding default user...");

            // Checking if users already exist
            if (await _userManager.Users.AnyAsync())
            {
                _logger.LogInformation("Users already exist. Skipping seeding.");
                return;
            }

            // Getting seed user from configuration
            var seedUser = configuration.GetSection("AppUser").Get<SeedUser>();
            if (seedUser is null)
            {
                _logger.LogError("Seed user configuration is missing.");
                throw new InvalidOperationException("Seed user configuration is not set.");
            }

            var company = new Company
            {
                Name = "Default Company",
                CompanyEmail = "info@defaultcompany.com"
            };
           _ims.Companies.Add(company);
            _ims.SaveChanges();

            // Creating the admin user
            var admin = new AppUser
            {
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
                Email = seedUser.Email,
                UserName = seedUser.UserName,
                CompanyId = company.Id
            };

            var result = await _userManager.CreateAsync(admin, seedUser.Password);
            if (result.Succeeded)
            {
                // Assigning the user to the Admin role
                await _userManager.AddToRoleAsync(admin, Roles.Admin.ToString());
                _logger.LogInformation("Default user created and assigned to role {RoleName}.", Roles.Admin.ToString());
            }
            else
            {
                _logger.LogError("Error creating default user: {@Errors}", result.Errors);
                throw new InvalidOperationException("Failed to create default user. See logs for details.");
            }
        }
    }
}
