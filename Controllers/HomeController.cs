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
        private const int MaxTeachingLoad = 25;
        private const int MaxLearningHoursPerDay = 25;

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
                     .OrderBy(a => a.Instructor.FullName)
                    .ToListAsync();

                var timeSlots = await _context.TimeSlots
                    .Include(ts => ts.DaysOfWeek)
                    .AsNoTracking()
                    .ToListAsync();

                var orderedSlots = timeSlots
                    .OrderBy(ts => ts.DaysOfWeek.Id)
                    .ThenBy(ts => ts.From)
                    .ToList();

                var newSchedules = new List<Schedule>();

                var instructorSlotMap = new Dictionary<(int instructorId, int timeSlotId), bool>();
                var sectionSlotMap = new Dictionary<(int sectionId, int timeSlotId), bool>();
                var instructorHoursMap = new Dictionary<int, int>();
                var sectionDayHoursMap = new Dictionary<(int sectionId, string day), int>();
                var sectionScheduledDays = new Dictionary<int, HashSet<string>>();
                var instructorDaySlots = new Dictionary<(int instructorId, string day), List<TimeSlot>>();

                foreach (var allocation in allocations)
                {
                    var instructorId = allocation.InstructorId;
                    var sectionId = allocation.SectionId;
                    var creditHour = allocation.Course.CreditHour;

                    if (!sectionScheduledDays.ContainsKey(sectionId))
                        sectionScheduledDays[sectionId] = new HashSet<string>();

                    bool assigned = false;

                    var rotatedSlots = orderedSlots
                        .Skip(sectionId % 5)
                        .Concat(orderedSlots.Take(sectionId % 5))
                        .ToList();

                    var prioritizedSlots = rotatedSlots
                        .OrderByDescending(ts =>
                        {
                            var day = ts.DaysOfWeek.Name;
                            var score = 0;

                            if (!sectionScheduledDays[sectionId].Contains(day)) score += 2;

                            var existingSlots = instructorDaySlots.TryGetValue((instructorId, day), out var list) ? list : new List<TimeSlot>();
                            if (!existingSlots.Any()) score += 2; // no class that day
                            else
                            {
                           
                                var slotIndices = existingSlots.Select(es => orderedSlots.FindIndex(s => s.Id == es.Id)).ToList();
                                var currentSlotIndex = orderedSlots.FindIndex(s => s.Id == ts.Id);

                                // Hard penalty if squeezed between two other sessions
                                if (slotIndices.Contains(currentSlotIndex - 1) && slotIndices.Contains(currentSlotIndex + 1))
                                    return -1000; // skip this slot completely

                                // Strong reward if this is nicely spaced
                                if (!slotIndices.Contains(currentSlotIndex - 1) && !slotIndices.Contains(currentSlotIndex + 1))
                                    score += 3; // large reward

                                // Light penalty if it's back-to-back with existing slot
                                if (slotIndices.Contains(currentSlotIndex - 1) || slotIndices.Contains(currentSlotIndex + 1))
                                    score -= 1;

                                // Boost based on course weight
                                score *= allocation.Course.CreditHour;

                            }

                            return score;
                        })
                        .ToList();

                    foreach (var ts in prioritizedSlots)
                    {
                        var timeSlotId = ts.Id;
                        var day = ts.DaysOfWeek.Name;

                        var instructorBusy = instructorSlotMap.ContainsKey((instructorId, timeSlotId));
                        var sectionBusy = sectionSlotMap.ContainsKey((sectionId, timeSlotId));

                        var currentInstructorHours = instructorHoursMap.TryGetValue(instructorId, out var iHrs) ? iHrs : 0;
                        var currentSectionDayHours = sectionDayHoursMap.TryGetValue((sectionId, day), out var sHrs) ? sHrs : 0;

                        var exceedsTeachingLimit = currentInstructorHours + creditHour > MaxTeachingLoad;
                        var exceedsLearningLimit = currentSectionDayHours + creditHour > MaxLearningHoursPerDay;

                        var sectionHas5Days = sectionScheduledDays[sectionId].Count >= 5;
                        var isNewDayForSection = !sectionScheduledDays[sectionId].Contains(day);

                        if (!instructorBusy && !sectionBusy && !exceedsTeachingLimit && !exceedsLearningLimit)
                        {
                            if (sectionHas5Days && isNewDayForSection)
                                continue;

                            newSchedules.Add(new Schedule
                            {
                                AllocationId = allocation.Id,
                                TimeSlotId = timeSlotId
                            });

                            instructorSlotMap[(instructorId, timeSlotId)] = true;
                            sectionSlotMap[(sectionId, timeSlotId)] = true;

                            instructorHoursMap[instructorId] = currentInstructorHours + creditHour;
                            sectionDayHoursMap[(sectionId, day)] = currentSectionDayHours + creditHour;
                            sectionScheduledDays[sectionId].Add(day);

                            if (!instructorDaySlots.ContainsKey((instructorId, day)))
                                instructorDaySlots[(instructorId, day)] = new List<TimeSlot>();
                            instructorDaySlots[(instructorId, day)].Add(ts);

                            assigned = true;
                            break;
                        }
                    }

                    if (!assigned)
                    {
                        ViewBag.ErrorMessage = $"Unable to schedule course {allocation.Course.Name} for section {allocation.Section.Name}.";
                        return View("Generated", new List<Schedule>());
                    }
                }

                var conflictFound = newSchedules
                    .GroupBy(s => new { s.TimeSlotId, InstructorId = allocations.First(a => a.Id == s.AllocationId).InstructorId })
                    .Any(g => g.Count() > 1);

                if (conflictFound)
                {
                    ViewBag.ErrorMessage = "Conflict detected! Could not resolve schedule.";
                    return View("Generated", new List<Schedule>());
                }

                var existingSchedules = await _context.Schedules.AsNoTracking().ToListAsync();
                foreach (var schedule in existingSchedules)
                {
                    _context.Entry(schedule).State = EntityState.Deleted;
                }

                await _context.SaveChangesAsync();

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
