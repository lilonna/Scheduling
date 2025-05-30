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
                var roomSlotMap = new Dictionary<(int roomId, int timeSlotId), bool>();


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
                              roomSlotMap,
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
                    .Include(s => s.Room)
                    .Include(s => s.Allocation).ThenInclude(a => a.Course)
                    .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                    .Include(s => s.Allocation).ThenInclude(a => a.Section)
                    .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                    .Include(s => s.Allocation) .ThenInclude(a => a.Section)
                    .ThenInclude(sec => sec.Room)
                    .ToListAsync();

                TempData["SuccessMessage"] = "Schedule generated successfully and saved!";
                return RedirectToAction("Generated");

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
      Dictionary<(int roomId, int timeSlotId), bool> roomSlotMap,
      Dictionary<int, int> departmentToSSID
  )
        {
            var instructorId = allocation.InstructorId;
            var sectionId = allocation.SectionId;
            var creditHour = allocation.Course.CreditHour;

            if (!sectionScheduledDays.ContainsKey(sectionId))
                sectionScheduledDays[sectionId] = new HashSet<string>();

            var rotatedSlots = orderedSlots
                .Skip(sectionId % 5)
                .Concat(orderedSlots.Take(sectionId % 5))
                .ToList();

            var prioritizedSlots = rotatedSlots.OrderByDescending(ts =>
            {
                var day = ts.DaysOfWeek.Name;
                var score = 0;

                if (!sectionScheduledDays[sectionId].Contains(day)) score += 2;

                var existing = instructorDaySlots.TryGetValue((instructorId, day), out var list) ? list : new();
                if (!existing.Any()) score += 2;
                else
                {
                    var slotIndices = existing.Select(e => orderedSlots.FindIndex(s => s.Id == e.Id)).ToList();
                    var index = orderedSlots.FindIndex(s => s.Id == ts.Id);

                    if (slotIndices.Contains(index - 1) && slotIndices.Contains(index + 1)) return -1000;
                    if (!slotIndices.Contains(index - 1) && !slotIndices.Contains(index + 1)) score += 3;
                    if (slotIndices.Contains(index - 1) || slotIndices.Contains(index + 1)) score -= 1;

                    score *= creditHour;
                }

                // Bonus for back-to-back same-course same-instructor
                if (sameCourseGroup.Count > 1)
                {
                    var sameDaySchedules = newSchedules
                        .Where(s => sameCourseGroup.Any(g => g.Id == s.AllocationId) &&
                                    orderedSlots.FirstOrDefault(x => x.Id == s.TimeSlotId)?.DaysOfWeek.Name == day)
                        .ToList();

                    if (sameDaySchedules.Any(s =>
                        Math.Abs(orderedSlots.FindIndex(x => x.Id == s.TimeSlotId) -
                                 orderedSlots.FindIndex(x => x.Id == ts.Id)) == 1))
                    {
                        score += 5;
                    }
                }

                return score;
            }).ToList();

            foreach (var ts in prioritizedSlots)
            {
                var timeSlotId = ts.Id;
                var day = ts.DaysOfWeek.Name;

                if (instructorSlotMap.ContainsKey((instructorId, timeSlotId)) ||
                    sectionSlotMap.ContainsKey((sectionId, timeSlotId)))
                    continue;

                var currentIHours = instructorHoursMap.TryGetValue(instructorId, out var iHrs) ? iHrs : 0;
                var currentSDHours = sectionDayHoursMap.TryGetValue((sectionId, day), out var sHrs) ? sHrs : 0;

                if (currentIHours + creditHour > MaxTeachingLoad ||
                    currentSDHours + creditHour > MaxLearningHoursPerDay)
                    continue;

                var has5Days = sectionScheduledDays[sectionId].Count >= 5;
                var isNewDay = !sectionScheduledDays[sectionId].Contains(day);
                if (has5Days && isNewDay) continue;

                int? assignedRoomId = allocation.Section.RoomId;
                if (assignedRoomId != null && roomSlotMap.ContainsKey(((int)assignedRoomId, timeSlotId)))
                {
                    // Find alternative room that is free
                    assignedRoomId = _context.Rooms
                        .AsNoTracking()
                        .ToList()
                        .FirstOrDefault(r => !roomSlotMap.ContainsKey((r.Id, timeSlotId)))?.Id;

                    if (assignedRoomId == null)
                        return false; // no available room
                }

                // Assign and track
                newSchedules.Add(new Schedule
                {
                    AllocationId = allocation.Id,
                    TimeSlotId = timeSlotId,
                    RoomId = assignedRoomId,
                    Ssid = departmentToSSID[allocation.Section.DepartmentId]
                });
                roomSlotMap[((int) assignedRoomId, timeSlotId)] = true;


                instructorSlotMap[(instructorId, timeSlotId)] = true;
                sectionSlotMap[(sectionId, timeSlotId)] = true;
                instructorHoursMap[instructorId] = currentIHours + creditHour;
                sectionDayHoursMap[(sectionId, day)] = currentSDHours + creditHour;
                sectionScheduledDays[sectionId].Add(day);

                if (!instructorDaySlots.ContainsKey((instructorId, day)))
                    instructorDaySlots[(instructorId, day)] = new();
                instructorDaySlots[(instructorId, day)].Add(ts);

                return true;  // Successfully assigned
            }

            // If no slot was found, return false
            return false;
        }


        private async Task<List<Allocation>> GetAllocationsAsync()
        {
            return await _context.Allocations
                .Include(a => a.Course)
                .Include(a => a.Instructor)
                  .Include(a => a.Section)
            .ThenInclude(sec => sec.Batch)
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
        public IActionResult ViewByTimeSlot(int scheduleSettingId)
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
        [HttpGet]
        public async Task<IActionResult> Generated()
        {
            var schedules = await _context.Schedules
                .Include(s => s.Room)
                .Include(s => s.Allocation).ThenInclude(a => a.Course)
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation).ThenInclude(a => a.Section)
                .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                .Include(s => s.Allocation).ThenInclude(a => a.Section).ThenInclude(sec => sec.Room)
                .ToListAsync();

            return View(schedules); 
        }

        [HttpPost]
public async Task<IActionResult> ClearAllSchedules()
{
    var schedules = await _context.Schedules.ToListAsync();
    _context.Schedules.RemoveRange(schedules);
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "All schedules cleared!";
    return RedirectToAction("Generated");
}

[HttpPost]
public async Task<IActionResult> Regenerate()
{
    // Clear first
    var schedules = await _context.Schedules.ToListAsync();
    _context.Schedules.RemoveRange(schedules);
    await _context.SaveChangesAsync();

    // Then generate
    return await Generate();
}

        public IActionResult SelectBatch()
        {
            var batches = _context.Batchs.ToList(); 
            return View(batches);
        }
        public IActionResult ViewSectionByBatch(int id)
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Course)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                        .ThenInclude(sec => sec.Batch)
                .Include(s => s.TimeSlot)
                    .ThenInclude(ts => ts.DaysOfWeek)
                .AsNoTracking()
                .Where(s => s.Allocation.Section.Batch.Id == id) // filter by batch ID
                .ToList();

            var groupedSchedules = schedules
                .GroupBy(s => s.Allocation.Section.Batch.Name)
                .OrderBy(g => g.Key)
                .Select(batchGroup => new
                {
                    BatchName = batchGroup.Key,
                    Sections = batchGroup
                        .GroupBy(s => s.Allocation.Section.Id)
                        .OrderBy(g => g.First().Allocation.Section.Name)
                        .Select(sectionGroup => new
                        {
                            Section = sectionGroup.First().Allocation.Section,
                            Schedules = sectionGroup
                                .OrderBy(s => s.TimeSlot.DaysOfWeek.Id)
                                .ThenBy(s => s.TimeSlot.From)
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return View("ViewBySection", groupedSchedules);
        }



        // Views for filtering by batch
        public IActionResult ViewByBatch(int scheduleSettingId)
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section).ThenInclude(s => s.Batch)
                     .Include(s => s.Allocation)
        .ThenInclude(a => a.Course)
          .Include(s => s.Allocation)
        .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot).ThenInclude(t=>t.DaysOfWeek)
                .ToList();

            return View(schedules);
        }
        public IActionResult ViewAllSchedulesByInstructor()
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Course)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                .Include(s => s.TimeSlot)
                    .ThenInclude(ts => ts.DaysOfWeek)
                .AsNoTracking()
                .ToList();

            var groupedSchedules = schedules
                .GroupBy(s => s.Allocation.Instructor.Id)
                .Select(g => new
                {
                    Instructor = g.First().Allocation.Instructor,
                    Schedules = g
                        .OrderBy(s => s.TimeSlot.DaysOfWeek.Id)
                        .ThenBy(s => s.TimeSlot.From)
                        .ToList()
                })
                .ToList();

            return View(groupedSchedules);
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedulesBySection(int scheduleId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Allocation)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
            {
                return NotFound();
            }

            var sectionId = schedule.Allocation.SectionId;

            var schedules = await _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .Where(s => s.Allocation.SectionId == sectionId && s.Id != scheduleId)
                .Select(s => new
                {
                    id = s.Id,
                    displayText = $"{s.Allocation.Instructor.FullName} - {s.TimeSlot.DaysOfWeek.Name} {s.TimeSlot.From:hh\\:mm} - {s.TimeSlot.To:hh\\:mm}"
                })
                .ToListAsync();

            return Json(schedules);
        }

     
        [HttpPost]
        public async Task<IActionResult> SwapSchedules(int scheduleId1, int scheduleId2)
        {
            var schedule1 = await _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .FirstOrDefaultAsync(s => s.Id == scheduleId1);

            var schedule2 = await _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .FirstOrDefaultAsync(s => s.Id == scheduleId2);

            if (schedule1 == null || schedule2 == null)
            {
                return NotFound("One or both schedules not found.");
            }

            var instructor1Id = schedule1.Allocation.InstructorId;
            var instructor2Id = schedule2.Allocation.InstructorId;

            var timeSlot1 = schedule1.TimeSlot;
            var timeSlot2 = schedule2.TimeSlot;

            var conflict1 = await _context.Schedules
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .Include(s => s.Allocation)
                .Where(s => s.Allocation.InstructorId == instructor1Id && s.Id != schedule1.Id)
                .FirstOrDefaultAsync(s =>
                    s.TimeSlot.DaysOfWeekId == timeSlot2.DaysOfWeekId &&
                    s.TimeSlot.From < timeSlot2.To &&
                    s.TimeSlot.To > timeSlot2.From);

            var conflict2 = await _context.Schedules
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .Include(s => s.Allocation)
                .Where(s => s.Allocation.InstructorId == instructor2Id && s.Id != schedule2.Id)
                .FirstOrDefaultAsync(s =>
                    s.TimeSlot.DaysOfWeekId == timeSlot1.DaysOfWeekId &&
                    s.TimeSlot.From < timeSlot1.To &&
                    s.TimeSlot.To > timeSlot1.From);

            if (conflict1 != null || conflict2 != null)
            {
                var schedules = await _context.Schedules
                    .Include(s => s.Allocation)
                        .ThenInclude(a => a.Instructor)
                    .Include(s => s.TimeSlot)
                        .ThenInclude(t => t.DaysOfWeek)
                    .ToListAsync();

                var errors = new List<string>();

                if (conflict1 != null)
                {
                    errors.Add($"Instructor {schedule1.Allocation.Instructor.FullName} has already class on {conflict1.TimeSlot.DaysOfWeek.Name} from {conflict1.TimeSlot.From:hh\\:mm} to {conflict1.TimeSlot.To:hh\\:mm}.");
                }

                if (conflict2 != null)
                {
                    errors.Add($"Instructor {schedule2.Allocation.Instructor.FullName} has already class on {conflict2.TimeSlot.DaysOfWeek.Name} from {conflict2.TimeSlot.From:hh\\:mm} to {conflict2.TimeSlot.To:hh\\:mm}.");
                }

                return Json(new { success = false, errors });

            }

            // Swap AllocationId
            var tempAllocationId = schedule1.AllocationId;
            schedule1.AllocationId = schedule2.AllocationId;
            schedule2.AllocationId = tempAllocationId;

            await _context.SaveChangesAsync();

            return RedirectToAction("ViewAllSchedulesByInstructor");
        }

        public IActionResult ViewBySection()
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Course)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                        .ThenInclude(sec => sec.Batch)
                .Include(s => s.TimeSlot)
                    .ThenInclude(ts => ts.DaysOfWeek)
                .AsNoTracking()
                .ToList();

            var groupedSchedules = schedules
                .GroupBy(s => s.Allocation.Section.Batch.Name)
                .OrderBy(g => g.Key)
                .Select(batchGroup => new
                {
                    BatchName = batchGroup.Key,
                    Sections = batchGroup
                        .GroupBy(s => s.Allocation.Section.Id)
                        .OrderBy(g => g.First().Allocation.Section.Name)
                        .Select(sectionGroup => new
                        {
                            Section = sectionGroup.First().Allocation.Section,
                            Schedules = sectionGroup
                                .OrderBy(s => s.TimeSlot.DaysOfWeek.Id)
                                .ThenBy(s => s.TimeSlot.From)
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return View("ViewBySection", groupedSchedules);
        }

        [HttpPost]
        public IActionResult GenerateSectionRoomAssignments()
        {
            var sections = _context.Sections
                .Where(s => s.RoomId == null) // only assign if not already assigned
                .OrderBy(s => s.Name)
                .ToList();

            var rooms = _context.Rooms
                .OrderBy(r => r.RoomNumber)
                .ToList();

            if (!rooms.Any())
            {
                TempData["Error"] = "No rooms available. Please create rooms first.";
                return RedirectToAction("Index");
            }

            int roomIndex = 0;
            foreach (var section in sections)
            {
                section.RoomId = rooms[roomIndex].Id;

                // Round-robin room assignment (so multiple sections can share rooms)
                roomIndex = (roomIndex + 1) % rooms.Count;
            }

            _context.SaveChanges();
            TempData["Success"] = "Room assignments generated successfully.";
            return RedirectToAction("ViewSectionRoomAssignments");

        }
        public IActionResult ViewSectionRoomAssignments()
        {
            var sectionRooms = _context.Sections
                .Include(s => s.Room)
                    .ThenInclude(r => r.Block)
                .OrderBy(s => s.Name)
                .ToList();

            return View(sectionRooms);
        }


        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> SwapSchedules()
        {
            var schedules = await _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot)
                    .ThenInclude(t => t.DaysOfWeek)
                .ToListAsync();

            return View(schedules);
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
