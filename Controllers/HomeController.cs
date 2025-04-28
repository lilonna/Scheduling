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
        private const int MaxTeachingLoad = 30;
        private const int MaxLearningHoursPerDay = 30;

        public HomeController(ILogger<HomeController> logger, SchedulingContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Generate()
        {
            try
            {
                var allocations = await GetAllocationsAsync();
                var orderedSlots = await GetOrderedTimeSlotsAsync();
                var scheduleSettings = await _context.ScheduleSettings.ToListAsync();
                var departmentToSSID = scheduleSettings.ToDictionary(ss => ss.DepartmentId, ss => ss.Id);

                var newSchedules = new List<Schedule>();
                var instructorSlotMap = new Dictionary<(int, int), bool>();
                var sectionSlotMap = new Dictionary<(int, int), bool>();
                var instructorHoursMap = new Dictionary<int, int>();
                var sectionDayHoursMap = new Dictionary<(int, string), int>();
                var sectionScheduledDays = new Dictionary<int, HashSet<string>>();
                var instructorDaySlots = new Dictionary<(int, string), List<TimeSlot>>();

                var groupedAllocations = allocations
                    .GroupBy(a => new { a.InstructorId, a.CourseId })
                    .ToList();

                foreach (var group in groupedAllocations)
                {
                    foreach (var allocation in group)
                    {
                        var assigned = TryAssignSchedule(
                            allocation, orderedSlots, newSchedules,
                            instructorSlotMap, sectionSlotMap,
                            instructorHoursMap, sectionDayHoursMap,
                            sectionScheduledDays, instructorDaySlots,
                            group.ToList(),
                                departmentToSSID
                        );

                        if (!assigned)
                        {
                            ViewBag.ErrorMessage = $"Unable to schedule course {allocation.Course.Name} for section {allocation.Section.Name}.";
                            return View("Generated", new List<Schedule>());
                        }
                    }
                }

                if (HasScheduleConflicts(newSchedules, allocations))
                {
                    ViewBag.ErrorMessage = "Conflict detected! Could not resolve schedule.";
                    return View("Generated", new List<Schedule>());
                }

                await ReplaceOldSchedulesAsync(newSchedules);

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

        private bool TryAssignSchedule(
            Allocation allocation,
            List<TimeSlot> orderedSlots,
            List<Schedule> newSchedules,
            Dictionary<(int, int), bool> instructorSlotMap,
            Dictionary<(int, int), bool> sectionSlotMap,
            Dictionary<int, int> instructorHoursMap,
            Dictionary<(int, string), int> sectionDayHoursMap,
            Dictionary<int, HashSet<string>> sectionScheduledDays,
            Dictionary<(int, string), List<TimeSlot>> instructorDaySlots,
            List<Allocation> sameCourseGroup,
            Dictionary<int, int> departmentToSSID
        )
        {
            // Schedule assignment logic here (as per your code)
            // Same logic for assigning schedule as already given
        }

        private async Task<List<Allocation>> GetAllocationsAsync()
        {
            return await _context.Allocations
                .Include(a => a.Course)
                .Include(a => a.Instructor)
                .Include(a => a.Section).ThenInclude(sec => sec.Department)
                .AsNoTracking()
                .OrderBy(a => a.Instructor.FullName).ThenBy(a => a.Course.Name)
                .ToListAsync();
        }

        private async Task<List<TimeSlot>> GetOrderedTimeSlotsAsync()
        {
            var slots = await _context.TimeSlots.Include(ts => ts.DaysOfWeek).AsNoTracking().ToListAsync();
            return slots.OrderBy(ts => ts.DaysOfWeek.Id).ThenBy(ts => ts.From).ToList();
        }

        private bool HasScheduleConflicts(List<Schedule> schedules, List<Allocation> allocations)
        {
            return schedules
                .GroupBy(s => new { s.TimeSlotId, InstructorId = allocations.First(a => a.Id == s.AllocationId).InstructorId })
                .Any(g => g.Count() > 1);
        }

        private async Task ReplaceOldSchedulesAsync(List<Schedule> newSchedules)
        {
            var old = await _context.Schedules.AsNoTracking().ToListAsync();
            foreach (var schedule in old)
            {
                _context.Entry(schedule).State = EntityState.Deleted;
            }
            await _context.SaveChangesAsync();

            await _context.Schedules.AddRangeAsync(newSchedules);
            await _context.SaveChangesAsync();
        }

        // Views for filtering by category
        public IActionResult ViewByCategory(int scheduleSettingId)
        {
            var categoryIds = _context.SstimeSlots
                .Where(sst => sst.Ssid == scheduleSettingId)
                .Select(sst => sst.CategoryId)
                .ToList();

            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                .Include(s => s.TimeSlot)
                    .ThenInclude(ts => ts.Category)
                .Where(s => categoryIds.Contains(s.TimeSlot.CategoryId))
                .ToList();

            return View(schedules);
        }

        // Views for filtering by batch
        public IActionResult ViewByBatch(int scheduleSettingId)
        {
            var batchIds = _context.Ssbatchs
                .Where(ssb => ssb.Ssid == scheduleSettingId)
                .Select(ssb => ssb.BatchId)
                .ToList();

            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                .Include(s => s.TimeSlot)
                .Where(s => batchIds.Contains(s.Allocation.Section.BatchId))
                .ToList();

            return View(schedules);
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
