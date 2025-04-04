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
                var sortedInstructors = _context.Instructors.OrderBy(i => i.FullName).ToList();
                //if (sortedInstructors.Count == 0)
                //{
                //    ViewBag.ErrorMessage = "No instructors found in the system.";
                //    return View("GenerateSchedule");
                //}

                var courses = _context.Courses.ToList();
                foreach (var course in courses)
                {
                    var availableInstructor = sortedInstructors.FirstOrDefault(i =>
                        i.Allocations.Sum(a => a.Course.CreditHour) + course.CreditHour <= MAX_CREDIT_HOURS_PER_DAY);

                    if (availableInstructor == null)
                    {
                        ViewBag.ErrorMessage = $"No available instructor for course: {course.Name}";
                        return View("GenerateSchedule");
                    }

                    _context.ChangeTracker.Clear(); // Ensure no conflicts
                    var allocation = new Allocation
                    {
                        CourseId = course.Id,
                        InstructorId = availableInstructor.Id
                    };

                    _context.Allocations.Add(allocation);
                }
                _context.SaveChanges();

                var sections = _context.Sections.ToList();
                if (sections.Count == 0)
                {
                    ViewBag.ErrorMessage = "No sections found in the system.";
                    return View("GenerateSchedule");
                }

                foreach (var section in sections)
                {
                    var allocations = _context.Allocations.Where(a => a.SectionId == null).ToList();

                    foreach (var allocation in allocations)
                    {
                        if (section.Allocations.Sum(a => a.Course.CreditHour) + allocation.Course.CreditHour > MAX_LEARNING_HOURS_PER_DAY)
                        {
                            ViewBag.ErrorMessage = $"Section {section.Name} cannot take course {allocation.Course.Name} due to time limits.";
                            return View("GenerateSchedule");
                        }

                        var trackedAllocation = _context.Allocations.Find(allocation.Id);
                        if (trackedAllocation != null)
                        {
                            trackedAllocation.SectionId = section.Id;
                            _context.Entry(trackedAllocation).State = EntityState.Modified;
                        }
                    }
                }
                _context.SaveChanges();

                var allocationsList = _context.Allocations.ToList();
                foreach (var allocation in allocationsList)
                {
                    var availableTimeslot = _context.TimeSlots
                        .FirstOrDefault(t => !_context.Schedules.Any(s => s.TimeSlotId == t.Id && s.AllocationId == allocation.Id));

                    if (availableTimeslot == null)
                    {
                        ViewBag.ErrorMessage = $"No available timeslot for course {allocation.Course.Name}";
                        return View("GenerateSchedule");
                    }

                    _context.ChangeTracker.Clear(); // Prevent duplicate tracking
                    var schedule = new Schedule
                    {
                        AllocationId = allocation.Id,
                        TimeSlotId = availableTimeslot.Id
                    };

                    _context.Schedules.Add(schedule);
                }
                _context.SaveChanges();

                foreach (var instructor in sortedInstructors)
                {
                    var schedule = _context.Schedules.Where(s => s.Allocation.InstructorId == instructor.Id).ToList();
                    if (schedule.GroupBy(s => s.TimeSlotId).Any(g => g.Count() > 1))
                    {
                        ViewBag.ErrorMessage = $"Conflict detected for instructor {instructor.FullName}";
                        return View("GenerateSchedule");
                    }
                }

                foreach (var section in sections)
                {
                    var schedule = _context.Schedules.Where(s => s.Allocation.SectionId == section.Id).ToList();
                    if (schedule.GroupBy(s => s.TimeSlotId).Any(g => g.Count() > 1))
                    {
                        ViewBag.ErrorMessage = $"Conflict detected for section {section.Name}";
                        return View("GenerateSchedule");
                    }
                }

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
                ViewBag.ErrorMessage = $"Error generating schedule: {ex.Message}";
                if (ex.InnerException != null)
                {
                    ViewBag.ErrorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }
                return View("GenerateSchedule");
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
