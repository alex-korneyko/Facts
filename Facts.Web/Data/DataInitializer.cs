using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Facts.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Facts.Web.Data
{
    public static class DataInitializer
    {
        public static async Task InitializeAsync(IServiceProvider scopeServiceProvider)
        {
            var scope = scopeServiceProvider.CreateScope();
            await using var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
            var isExists = context!.GetService<IDatabaseCreator>() is RelationalDatabaseCreator databaseCreator &&
                           await databaseCreator.ExistsAsync();

            if (isExists)
            {
                return;
            }

            await context.Database.MigrateAsync();

            var roles = AppData.Roles.ToList();
            var roleStore = new RoleStore<IdentityRole>(context);

            foreach (var role in roles)
            {
                if (!context.Roles.Any(rl => rl.Name == role))
                {
                    await roleStore.CreateAsync(new IdentityRole(role)
                    {
                        NormalizedName = role.ToUpper()
                    });
                }
            }

            const string userName = "a.korneyko@gmail.com";

            if (context.Users.Any(usr => usr.Email == userName))
            {
                return;
            }

            var user = new IdentityUser()
            {
                Email = userName,
                EmailConfirmed = true,
                NormalizedEmail = userName.ToUpper(),
                UserName = userName,
                NormalizedUserName = userName.ToUpper(),
                SecurityStamp = Guid.NewGuid().ToString("D")
            };

            var passwordHasher = new PasswordHasher<IdentityUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, "111");

            var userStore = new UserStore(context);
            var identityResult = await userStore.CreateAsync(user, CancellationToken.None);

            if (!identityResult.Succeeded)
            {
                var errorMessage = string
                    .Join(", ", identityResult.Errors
                        .Select(err => $"{err.Code}: {err.Description}"));

                throw new DataException(errorMessage);
            }

            var userManager = scope.ServiceProvider.GetService<UserManager<IdentityUser>>();
            await userManager!.AddToRolesAsync(user, roles);

            await context.SaveChangesAsync();
        }
    }
}