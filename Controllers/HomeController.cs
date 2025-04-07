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
        private const int MaxTeachingLoad = 12;
        private const int MaxLearningHoursPerDay = 6;

        public HomeController(ILogger<HomeController> logger, SchedulingContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Generate()
        {
            try
            {
                var allocations = await _context.Allocations
                    .Include(a => a.Course)
                    .Include(a => a.Instructor)
                    .Include(a => a.Section)
                    .AsNoTracking()
                    .ToListAsync();

                var timeSlots = await _context.TimeSlots
                    .Include(ts => ts.DaysOfWeek)
                    .AsNoTracking()
                    .ToListAsync();

                var newSchedules = new List<Schedule>();
                var instructorSlotMap = new Dictionary<(int instructorId, int timeSlotId), bool>();
                var sectionSlotMap = new Dictionary<(int sectionId, int timeSlotId), bool>();

                foreach (var allocation in allocations)
                {
                    var instructorId = allocation.InstructorId;
                    var sectionId = allocation.SectionId;
                    var creditHour = allocation.Course.CreditHour;

                    foreach (var ts in timeSlots)
                    {
                        var timeSlotId = ts.Id;
                        var instructorBusy = instructorSlotMap.ContainsKey((instructorId, timeSlotId));
                        var sectionBusy = sectionSlotMap.ContainsKey((sectionId, timeSlotId));

                        if (!instructorBusy && !sectionBusy)
                        {
                            // Assign this time slot
                            newSchedules.Add(new Schedule
                            {
                                AllocationId = allocation.Id,
                                TimeSlotId = timeSlotId
                            });

                            instructorSlotMap[(instructorId, timeSlotId)] = true;
                            sectionSlotMap[(sectionId, timeSlotId)] = true;
                            break;
                        }
                    }
                }

                // Double-check for duplicate conflicts
                var conflictFound = newSchedules
                    .GroupBy(s => new { s.TimeSlotId, InstructorId = allocations.First(a => a.Id == s.AllocationId).InstructorId })
                    .Any(g => g.Count() > 1);

                if (conflictFound)
                {
                    ViewBag.ErrorMessage = "Conflict detected! Could not resolve schedule.";
                    return View("Generated", new List<Schedule>());
                }

                // Clear previous schedules if needed
                var existingSchedules = await _context.Schedules.ToListAsync();
                _context.Schedules.RemoveRange(existingSchedules);
                await _context.SaveChangesAsync();

                // Save new schedules
                await _context.Schedules.AddRangeAsync(newSchedules);
                var saved = await _context.SaveChangesAsync();

                if (saved == 0)
                {
                    ViewBag.ErrorMessage = "Nothing was saved to the database. Possible issue with schedule data.";
                    return View("Generated", new List<Schedule>());
                }

                var finalSchedules = await _context.Schedules
                    .Include(s => s.Allocation).ThenInclude(a => a.Course)
                    .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                    .Include(s => s.Allocation).ThenInclude(a => a.Section)
                    .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                    .ToListAsync();

                ViewBag.SuccessMessage = "Schedule generated successfully and saved!";
                return View("Generated", finalSchedules);
            }
            catch (Exception ex)
            {
                var trackerLog = string.Join("\n", _context.ChangeTracker.Entries()
                    .Select(entry => $"Entity: {entry.Entity.GetType().Name}, State: {entry.State}"));

                ViewBag.TrackerLog = trackerLog;
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                return View("Generated", new List<Schedule>());
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
