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

            // --- 1. Departments (use existing) ---
            var departmentNames = new[]
            {
                "Computer Science", "Information Systems", "Electrical Engineering", "Mechanical Engineering", "Civil Engineering",
                "Software Engineering", "Physics", "Mathematics", "Biology", "Chemistry", "Economics", "Accounting",
                "Management", "Marketing", "Statistics"
            };

            var departments = await context.Departments.ToListAsync();
            if (!departments.Any())
            {
                departments = departmentNames
                    .Select((name, idx) => new Department { Id = idx + 1, Name = name })
                    .ToList();
                context.Departments.AddRange(departments);
                await context.SaveChangesAsync();
            }

            // --- 2. Courses ---
            if (!context.Courses.Any())
            {
                var courseList = new List<Course>();
                int courseId = 1;
                foreach (var dept in departments)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        var course = new Course
                        {
                            Id = courseId++,
                            Name = $"{dept.Name.Split(' ')[0]} {100 + i}",
                            CreditHour = random.Next(2, 4)
                        };
                        courseList.Add(course);
                    }
                }
                context.Courses.AddRange(courseList);
                await context.SaveChangesAsync();
            }

            // --- 3. Categories ---
            if (!context.Categories.Any())
            {
                var categoryNames = new[] { "Day", "Evening", "Weekend" };
                var categories = categoryNames
                    .Select((name, idx) => new Category { Id = idx + 1, Name = name })
                    .ToList();
                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }

            // --- 4. Days of Week ---
            if (!context.DaysOfWeeks.Any())
            {
                var dayNames = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
                var days = dayNames
                    .Select((name, idx) => new DaysOfWeek { Id = idx + 1, Name = name })
                    .ToList();
                context.DaysOfWeeks.AddRange(days);
                await context.SaveChangesAsync();
            }

            // --- 5. TimeSlots ---
            if (!context.TimeSlots.Any())
            {
                var categories = await context.Categories.ToListAsync();
                var days = await context.DaysOfWeeks.ToListAsync();

                int tsId = 1;
                var timeSlots = new List<TimeSlot>();
                foreach (var cat in categories)
                {
                    foreach (var day in days)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            var from = DateTime.Today.AddHours(8 + i);
                            var to = from.AddHours(1);
                            timeSlots.Add(new TimeSlot
                            {
                                Id = tsId++,
                                Name = $"{day.Name} {from:hh:mm tt}-{to:hh:mm tt}",
                                CategoryId = cat.Id,
                                DaysOfWeekId = day.Id,
                                From = from,
                                To = to
                            });
                        }
                    }
                }
                context.TimeSlots.AddRange(timeSlots);
                await context.SaveChangesAsync();
            }

            // --- 6. Schedule Settings ---
            if (!context.ScheduleSettings.Any())
            {
                var scheduleSettings = departments
                    .Select((d, idx) => new ScheduleSetting
                    {
                        Id = idx + 1,
                        DepartmentId = d.Id,
                        Title = $"{d.Name} Main Setting"
                    }).ToList();

                context.ScheduleSettings.AddRange(scheduleSettings);
                await context.SaveChangesAsync();
            }

            // --- 7. SSBatchs ---
            var scheduleSettingsList = await context.ScheduleSettings.ToListAsync();
            var ssBatchs = new List<Ssbatch>();
            int ssbatchId = 1;

            foreach (var ss in scheduleSettingsList)
            {
                for (int batchId = 1; batchId <= 5; batchId++)
                {
                    if (!await context.Ssbatchs.AnyAsync(b => b.Ssid == ss.Id && b.BatchId == batchId))
                    {
                        ssBatchs.Add(new Ssbatch
                        {
                            Id = ssbatchId++,
                            Ssid = ss.Id,
                            BatchId = batchId
                        });
                    }
                }
            }

            context.Ssbatchs.AddRange(ssBatchs);
            await context.SaveChangesAsync();

            // --- 8. SSTimeSlots ---
            var categoryList = await context.Categories.ToListAsync();
            var ssTimeSlots = new List<SstimeSlot>();
            int sstimeSlotId = 1;
            foreach (var ss in scheduleSettingsList)
            {
                foreach (var cat in categoryList)
                {
                    if (!await context.SstimeSlots.AnyAsync(t => t.Ssid == ss.Id && t.CategoryId == cat.Id))
                    {
                        ssTimeSlots.Add(new SstimeSlot
                        {
                            Id = sstimeSlotId++,
                            Ssid = ss.Id,
                            CategoryId = cat.Id
                        });
                    }
                }
            }
            context.SstimeSlots.AddRange(ssTimeSlots);
            await context.SaveChangesAsync();

            // --- 9. Allocations ---
            if (!context.Allocations.Any())
            {
                var instructors = await context.Instructors.Select(i => i.Id).ToListAsync();
                var sections = await context.Sections.Select(s => s.Id).ToListAsync();
                var courses = await context.Courses.ToListAsync();

                var allocations = new List<Allocation>();
                int allocationId = 1;
                foreach (var sectionId in sections)
                {
                    var courseSubset = courses.OrderBy(_ => random.Next()).Take(5).ToList();
                    foreach (var course in courseSubset)
                    {
                        var instructorId = instructors[random.Next(instructors.Count)];
                        allocations.Add(new Allocation
                        {
                            Id = allocationId++,
                            CourseId = course.Id,
                            InstructorId = instructorId,
                            SectionId = sectionId,
                            ContactHour = course.CreditHour
                        });
                    }
                }
                context.Allocations.AddRange(allocations);
                await context.SaveChangesAsync();
            }

            Console.WriteLine("✅ Static seed data inserted or skipped if exists.");
        }
    }
}
