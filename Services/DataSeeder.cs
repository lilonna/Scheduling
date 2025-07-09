using Scheduling.Models;
using Scheduling.Utilities;
using Microsoft.EntityFrameworkCore;
using hu_utils;

namespace Scheduling.Services
{
    public class DataSeeder
    {
        public static async Task SeedFullDataAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SchedulingContext>();
            var random = new Random();

            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    if (await context.DepartmentAdmins.AnyAsync(da => da.Id == i))
                    {
                        Console.WriteLine($"⚠️ DeptAdmin ID {i} already exists. Skipping...");
                        continue;
                    }

                    var user = new User
                    {
                        GenderId = i % 2 == 0 ? 1003 : 1004,
                        FirstName = $"DeptAdmin{i}",
                        MiddleName = "D",
                        LastName = $"User{i}",
                        UserName = $"deptadmin{i}",
                        Email = $"deptadmin{i}@example.com",
                        Password = "departmentadmin",
                        PhoneNumber = $"09{i:00000000}",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };

                    context.Users.Add(user);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ User created: {user.UserName} (Id={user.Id})");

                    user.Password = Password._one_way_encrypt("deptadmin123", user.Id);
                    await context.SaveChangesAsync();

                    var userRole = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = 2003, // DepartmentAdmin
                        IsDefault = true,
                        IsActive = true,
                        IsDeleted = false
                    };
                    context.UserRoles.Add(userRole);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ UserRole assigned for userId={user.Id}");

                    var departmentAdmin = new DepartmentAdmin
                    {
                        Id = i,
                        UserId = user.Id,
                        DepartmentId = i
                    };
                    context.DepartmentAdmins.Add(departmentAdmin);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ DepartmentAdmin linked for userId={user.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error for DeptAdmin{i}: {ex.Message}");
                }
            }
            Console.WriteLine("✅ Seeding finished.");

        }

    }
}
