using Scheduling.Models;
using Scheduling.Utilities;
using Microsoft.EntityFrameworkCore;
using hu_utils;

namespace Scheduling.Services
{
    public class DataSeeder
    {
        public static void Seed(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SchedulingContext>();

            //if (context.Students.Any() || context.Instructors.Any())
            //    return;

            var random = new Random();

            var maleNames = new[] { "Daniel", "Michael", "Elias", "Samuel", "Noah", "Teddy" };
            var femaleNames = new[] { "Anna", "Sara", "Hanna", "Lily", "Sophia", "Selam" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller" };

            var sectionIds = context.Sections.Select(s => s.Id).ToList();

            // --- Seed 600 Students ---
            for (int i = 1; i <= 600; i++)
            {
                bool isFemale = i % 2 == 0;
                string first = isFemale ? femaleNames[random.Next(femaleNames.Length)] : maleNames[random.Next(maleNames.Length)];
                string middle = isFemale ? femaleNames[random.Next(femaleNames.Length)] : maleNames[random.Next(maleNames.Length)];
                string last = lastNames[random.Next(lastNames.Length)];
                string username = $"{first.ToLower()}{i:D4}";
                string email = $"{first.ToLower()}.{middle.ToLower()}{i:D4}@uv.edu";

                var user = new User
                {
                    UserName = username,
                    Email = email,
                    FirstName = first,
                    MiddleName = middle,
                    LastName = last,
                    GenderId = isFemale ? 1004 : 1003,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false,
                    PhoneNumber = $"09{random.Next(10000000, 99999999)}"
                };

                context.Users.Add(user);
                context.SaveChanges(); // Get identity ID

                user.Password = Password._one_way_encrypt("Pass123@", user.Id);
                context.Users.Update(user);

                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = 1003, // Student
                    IsActive = true,
                    IsDeleted = false,
                    IsDefault = true
                });

                int sectionId = sectionIds[i % sectionIds.Count];
                int batchId = context.Sections.Where(s => s.Id == sectionId).Select(s => s.BatchId).FirstOrDefault();

                context.Students.Add(new Student
                {
                    Id = user.Id,
                    Name = $"{first} {middle} {last}",
                    UserId = user.Id,
                    BatchId = batchId,
                    SectionId = sectionId
                });

                context.SaveChanges();
            }

            // --- Seed 150 Instructors ---
            for (int i = 1; i <= 150; i++)
            {
                bool isFemale = i % 2 == 1;
                string first = isFemale ? femaleNames[random.Next(femaleNames.Length)] : maleNames[random.Next(maleNames.Length)];
                string middle = isFemale ? femaleNames[random.Next(femaleNames.Length)] : maleNames[random.Next(maleNames.Length)];
                string last = lastNames[random.Next(lastNames.Length)];
                string username = $"{first.ToLower()}_inst{i:D3}";
                string email = $"{first.ToLower()}.{middle.ToLower()}{i + 600}@uv.edu";

                var user = new User
                {
                    UserName = username,
                    Email = email,
                    FirstName = first,
                    MiddleName = middle,
                    LastName = last,
                    GenderId = isFemale ? 1004 : 1003,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false,
                    PhoneNumber = $"09{random.Next(10000000, 99999999)}"
                };

                context.Users.Add(user);
                context.SaveChanges();

                user.Password = Password._one_way_encrypt("Pass123@", user.Id);
                context.Users.Update(user);

                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = 1004, // Instructor
                    IsActive = true,
                    IsDeleted = false,
                    IsDefault = true
                });

                context.Instructors.Add(new Instructor
                {
                    Id = user.Id,
                    UserId = user.Id,
                    FullName = $"{first} {middle} {last}"
                });

                context.SaveChanges();
            }
        }
    }
}
