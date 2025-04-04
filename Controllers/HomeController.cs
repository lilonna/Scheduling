using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using System.Diagnostics;

namespace Scheduling.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SchedulingContext _context;
        private const int MAX_CREDIT_HOURS_PER_DAY = 6;
        private const int MAX_LEARNING_HOURS_PER_DAY = 8;

        public HomeController(ILogger<HomeController> logger, SchedulingContext context)
        {
            _logger = logger;
            _context = context;
        }
        public IActionResult GenerateSchedule()
        {
            try
            {
                // Step 1: Sort Instructors
                var sortedInstructors = _context.Instructors.OrderBy(i => i.FullName).ToList();

                // Step 2: Assign Instructors to Courses
                foreach (var course in _context.Courses.ToList())
                {
                    var availableInstructor = sortedInstructors.FirstOrDefault(i =>
                        i.Allocations.Sum(a => a.Course.CreditHour) + course.CreditHour <= MAX_CREDIT_HOURS_PER_DAY);

                    if (availableInstructor != null)
                    {
                        var allocation = new Allocation
                        {
                            CourseId = course.Id,
                            InstructorId = availableInstructor.Id
                        };
                        _context.Allocations.Add(allocation);
                    }
                    else
                    {
                        Console.WriteLine($"No available instructor for course: {course.Name}");
                    }
                }
                _context.SaveChanges();

                // Step 3: Assign Sections to Courses
                foreach (var section in _context.Sections.ToList())
                {
                    foreach (var allocation in _context.Allocations.Where(a => a.SectionId == null).ToList())
                    {
                        if (section.Allocations.Sum(a => a.Course.CreditHour) + allocation.Course.CreditHour <= MAX_LEARNING_HOURS_PER_DAY)
                        {
                            allocation.SectionId = section.Id;
                        }
                        else
                        {
                            Console.WriteLine($"Section {section.Name} cannot take course {allocation.Course.Name} due to time limits.");
                        }
                    }
                }
                _context.SaveChanges();

                // Step 4: Assign Courses to Time Slots
                foreach (var allocation in _context.Allocations.ToList())
                {
                    var availableTimeslot = _context.TimeSlots.FirstOrDefault(t =>
                        !_context.Schedules.Any(s => s.TimeSlotId == t.Id && s.AllocationId == allocation.Id));

                    if (availableTimeslot != null)
                    {
                        var schedule = new Schedule
                        {
                            AllocationId = allocation.Id,
                            TimeSlotId = availableTimeslot.Id
                        };
                        _context.Schedules.Add(schedule);
                    }
                    else
                    {
                        Console.WriteLine($"No available timeslot for course {allocation.Course.Name}");
                    }
                }
                _context.SaveChanges();

                // Step 5: Optimize Instructor Schedules
                foreach (var instructor in sortedInstructors)
                {
                    var instructorSchedule = _context.Schedules
                        .Where(s => s.Allocation.InstructorId == instructor.Id)
                        .GroupBy(s => s.TimeSlot.DaysOfWeekId)
                        .ToList();

                    var daysWithOneClass = instructorSchedule.Where(g => g.Count() == 1).ToList();

                    foreach (var day in daysWithOneClass)
                    {
                        var targetDay = instructorSchedule.FirstOrDefault(g => g.Count() > 1);
                        if (targetDay != null)
                        {
                            var classToMove = day.First();
                            classToMove.TimeSlotId = targetDay.First().TimeSlotId;
                        }
                    }
                }
                _context.SaveChanges();

                // Step 6: Validate Schedule (Conflict Detection)
                foreach (var instructor in sortedInstructors)
                {
                    var schedule = _context.Schedules.Where(s => s.Allocation.InstructorId == instructor.Id).ToList();
                    var hasConflict = schedule.GroupBy(s => s.TimeSlotId).Any(g => g.Count() > 1);

                    if (hasConflict)
                    {
                        Console.WriteLine($"Conflict detected for instructor {instructor.FullName}");
                    }
                }

                foreach (var section in _context.Sections.ToList())
                {
                    var schedule = _context.Schedules.Where(s => s.Allocation.SectionId == section.Id).ToList();
                    var hasConflict = schedule.GroupBy(s => s.TimeSlotId).Any(g => g.Count() > 1);

                    if (hasConflict)
                    {
                        Console.WriteLine($"Conflict detected for section {section.Name}");
                    }
                }

                // Step 7: Generate Final Schedule and Return as View
                var finalSchedule = _context.Schedules
                    .Include(s => s.Allocation.Course)
                    .Include(s => s.Allocation.Instructor)
                    .Include(s => s.Allocation.Section)
                    .Include(s => s.TimeSlot)
                    .ToList();

                return View(finalSchedule);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating schedule: {ex.Message}");
                // Optionally, log the error or return a friendly message
                return View("GenerateSchedule", new List<Schedule>());
            }
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


    }
}
