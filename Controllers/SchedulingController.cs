using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    public class SchedulingController : Controller
    {
        private readonly SchedulingContext _context;
        private const int MaxTeachingLoad = 30;
        private const int MaxLearningHoursPerDay = 30;
        public SchedulingController(SchedulingContext context)
        { 
            _context = context;
        }
        public async Task<IActionResult> Generate()
        {
            int? departmentIdNullable = HttpContext.Session.GetInt32("DepartmentId");
            if (departmentIdNullable == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any department.";
                return RedirectToAction("Index");
            }
            int departmentId = departmentIdNullable.Value;

            try
            {
                var allocations = await GetAllocationsAsync(departmentId);
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
                var admissionTypeDays = await _context.AdmissionTypeDays.AsNoTracking().ToListAsync();
                var admissionTypeCategories = await _context.AdmissionTypeCategories.AsNoTracking().ToListAsync();
                var admissionTypeToDayIds = admissionTypeDays
                    .GroupBy(x => x.AdmissionTypeId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.DayOfWeekId).ToHashSet());
                var admissionTypeToCategoryIds = admissionTypeCategories
                    .GroupBy(x => x.AdmissionTypeId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToHashSet());
                var allRooms = await _context.Rooms.AsNoTracking().ToListAsync();


                foreach (var group in groupedAllocations)
                {
                    foreach (var allocation in group)
                    {
                        int admissionTypeId = allocation.Section.Batch.AdmissionTypesId;

                        var allowedDays = admissionTypeToDayIds.GetValueOrDefault(admissionTypeId, new HashSet<int>());
                        var allowedCategories = admissionTypeToCategoryIds.GetValueOrDefault(admissionTypeId, new HashSet<int>());

                        var filteredSlots = orderedSlots
                            .Where(ts => allowedDays.Contains(ts.DaysOfWeekId) &&
                                         allowedCategories.Contains(ts.CategoryId))
                            .ToList();

                        var assigned = TryAssignSchedule(
                            allocation, filteredSlots, newSchedules,
                            instructorSlotMap, sectionSlotMap,
                            instructorHoursMap, sectionDayHoursMap,
                            sectionScheduledDays, instructorDaySlots,
                            group.ToList(), roomSlotMap, departmentToSSID,
    allRooms
                        );
                    }
                }



                await ReplaceOldSchedulesAsync(newSchedules, departmentId);


                var finalSchedules = await _context.Schedules
                    .Include(s => s.Room)
                    .Include(s => s.Allocation).ThenInclude(a => a.Course)
                    .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                    .Include(s => s.Allocation).ThenInclude(a => a.Section)
                    .Include(s => s.Allocation).ThenInclude(a => a.Section).ThenInclude(sec => sec.Department).Where(a => a.Allocation.Section.DepartmentId == departmentId)

                    .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                    .Include(s => s.Allocation).ThenInclude(a => a.Section).ThenInclude(sec => sec.Room)
                    .ToListAsync();

                TempData["SuccessMessage"] = "Schedule generated successfully and saved!";
                return RedirectToAction("Generated");
            }
            catch (Exception ex)
            {
                var trackerLog = string.Join("\n", _context.ChangeTracker.Entries()
                    .Select(entry => $"Entity: {entry.Entity.GetType().Name}, State: {entry.State}"));
                ViewBag.CurrentUserDepartmentId = GetCurrentUserDepartmentId();
                ViewBag.TrackerLog = trackerLog;
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                return View("Generated", new List<Schedule>());
            }
        }
        private int? GetCurrentUserDepartmentId()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return null;

            return _context.DepartmentAdmins
                .Where(d => d.UserId == userId)
                .Select(d => (int?)d.DepartmentId)
                .FirstOrDefault();
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
            Dictionary<int, int> departmentToSSID,
            List<Room> allRooms
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
                if (TryPlaceSchedule(ts, allocation, orderedSlots, newSchedules, instructorSlotMap, sectionSlotMap,
                    instructorHoursMap, sectionDayHoursMap, sectionScheduledDays, instructorDaySlots,
                    roomSlotMap, departmentToSSID, allRooms))
                    return true;
            }


            foreach (var ts in orderedSlots)
            {
                if (TryPlaceSchedule(ts, allocation, orderedSlots, newSchedules, instructorSlotMap, sectionSlotMap,
                    instructorHoursMap, sectionDayHoursMap, sectionScheduledDays, instructorDaySlots,
                    roomSlotMap, departmentToSSID, allRooms))
                    return true;
            }

            return false;
        }
        private bool TryPlaceSchedule(
            TimeSlot ts,
            Allocation allocation,
            List<TimeSlot> orderedSlots,
            List<Schedule> newSchedules,
            Dictionary<(int, int), bool> instructorSlotMap,
            Dictionary<(int, int), bool> sectionSlotMap,
            Dictionary<int, int> instructorHoursMap,
            Dictionary<(int, string), int> sectionDayHoursMap,
            Dictionary<int, HashSet<string>> sectionScheduledDays,
            Dictionary<(int, string), List<TimeSlot>> instructorDaySlots,
            Dictionary<(int roomId, int timeSlotId), bool> roomSlotMap,
            Dictionary<int, int> departmentToSSID ,
              List<Room> allRooms)
        {
            var instructorId = allocation.InstructorId;
            var sectionId = allocation.SectionId;
            var creditHour = allocation.Course.CreditHour;
            var timeSlotId = ts.Id;
            var day = ts.DaysOfWeek.Name;

            if (instructorSlotMap.ContainsKey((instructorId, timeSlotId)) ||
                sectionSlotMap.ContainsKey((sectionId, timeSlotId)))
                return false;

            var currentIHours = instructorHoursMap.TryGetValue(instructorId, out var iHrs) ? iHrs : 0;
            var currentSDHours = sectionDayHoursMap.TryGetValue((sectionId, day), out var sHrs) ? sHrs : 0;

            if (currentIHours + creditHour > MaxTeachingLoad ||
                currentSDHours + creditHour > MaxLearningHoursPerDay)
                return false;

           
           

            int? assignedRoomId = allocation.Section.RoomId;
            if (assignedRoomId != null && roomSlotMap.ContainsKey(((int)assignedRoomId, timeSlotId)))
            {
                assignedRoomId = allRooms.FirstOrDefault(r => !roomSlotMap.ContainsKey((r.Id, timeSlotId)))?.Id;

                if (assignedRoomId == null)
                    return false;
            }

            if (!departmentToSSID.TryGetValue(allocation.Section.DepartmentId, out var ssid))
                return false;

            newSchedules.Add(new Schedule
            {
                AllocationId = allocation.Id,
                TimeSlotId = timeSlotId,
                RoomId = assignedRoomId,
                Ssid = ssid
            });

            roomSlotMap[((int)assignedRoomId, timeSlotId)] = true;
            instructorSlotMap[(instructorId, timeSlotId)] = true;
            sectionSlotMap[(sectionId, timeSlotId)] = true;
            instructorHoursMap[instructorId] = currentIHours + creditHour;
            sectionDayHoursMap[(sectionId, day)] = currentSDHours + creditHour;
            sectionScheduledDays[sectionId].Add(day);

            if (!instructorDaySlots.ContainsKey((instructorId, day)))
                instructorDaySlots[(instructorId, day)] = new();
            instructorDaySlots[(instructorId, day)].Add(ts);

            return true;
        }
        private async Task<List<Allocation>> GetAllocationsAsync(int departmentId)
        {
            return await _context.Allocations
                .Include(a => a.Course)
                .Include(a => a.Instructor)
                .Include(a => a.Section).ThenInclude(sec => sec.Batch)
                .Include(a => a.Section).ThenInclude(sec => sec.Department)
                .AsNoTracking()
                .Where(a => a.Section.DepartmentId == departmentId)
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
        private async Task ReplaceOldSchedulesAsync(List<Schedule> newSchedules, int departmentId)
        {

            var oldSchedules = await _context.Schedules
                .Include(s => s.Allocation).ThenInclude(a => a.Section)
                .Where(s => s.Allocation.Section.DepartmentId == departmentId)
                .ToListAsync();

            _context.Schedules.RemoveRange(oldSchedules);
            await _context.SaveChangesAsync();

            await _context.Schedules.AddRangeAsync(newSchedules);
            await _context.SaveChangesAsync();
        }











        [HttpGet]
        public async Task<IActionResult> Generated()
        {
            int? departmentId = GetCurrentUserDepartmentId();
            if (departmentId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any department.";
                return RedirectToAction("Index");
            }

            var schedules = await _context.Schedules
                .Include(s => s.Room)
                .Include(s => s.Allocation).ThenInclude(a => a.Course)
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation).ThenInclude(a => a.Section)
                .Include(s => s.Allocation).ThenInclude(a => a.Section).ThenInclude(sec => sec.Room)
                .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                .Where(s => s.Allocation.Section.DepartmentId == departmentId)
                .AsNoTracking()
                .ToListAsync();

            return View(schedules);
        }

        public IActionResult SelectBatch()
        {
            var batches = _context.Batchs.ToList();
            return View(batches);
        }
        public IActionResult ViewSectionByBatch(int batchId, int departmentId)
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Course)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section)
                        .ThenInclude(sec => sec.Batch)
                .Include(s => s.Allocation.Section.Department)
                .Include(s => s.TimeSlot)
                    .ThenInclude(ts => ts.DaysOfWeek)
                .AsNoTracking()
                .Where(s => s.Allocation.Section.BatchId == batchId &&
                            s.Allocation.Section.DepartmentId == departmentId)
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
            ViewBag.CurrentUserDepartmentId = GetCurrentUserDepartmentId();

            return View("ViewBySection", groupedSchedules);
        }
        [HttpGet]
        public IActionResult GetDepartmentsByBatch(int batchId)
        {
            int? departmentId = GetCurrentUserDepartmentId();

            List<object> departments;

            if (departmentId != null)
            {
                
                departments = _context.Sections
                    .Where(s => s.BatchId == batchId && s.DepartmentId == departmentId)
                    .Select(s => s.Department)
                    .Distinct()
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToList<object>();
            }
            else
            {
                
                departments = _context.Sections
                    .Where(s => s.BatchId == batchId)
                    .Select(s => s.Department)
                    .Distinct()
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToList<object>();
            }

            return Json(departments);
        }

        public async Task<IActionResult> MyInstructorSchedule()
        {
            var instructorId = HttpContext.Session.GetInt32("InstructorId");
            if (instructorId == null) return Unauthorized();

            var schedules = await _context.Schedules
                .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                .Include(s => s.Allocation).ThenInclude(a => a.Course)
                .Include(s => s.Allocation).ThenInclude(a => a.Section)
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Where(s => s.Allocation.InstructorId == instructorId)
                .ToListAsync();

            var grouped = new[]
            {
        new
        {
            Instructor = schedules.FirstOrDefault()?.Allocation?.Instructor,
            Schedules = schedules
        }
    };

            return View("ViewAllSchedulesByInstructor", grouped);
        }
        public async Task<IActionResult> MyStudentSchedule()
        {
            var sectionId = HttpContext.Session.GetInt32("SectionId");
            if (sectionId == null) return Unauthorized();

            var schedules = await _context.Schedules
                .Include(s => s.TimeSlot).ThenInclude(ts => ts.DaysOfWeek)
                .Include(s => s.Allocation).ThenInclude(a => a.Course)
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation).ThenInclude(a => a.Section).ThenInclude(sec => sec.Batch)
                .Where(s => s.Allocation.SectionId == sectionId)
                .ToListAsync();

            if (!schedules.Any()) return View("ViewBySection", new List<dynamic>());

            var section = schedules.First().Allocation.Section;
            var batch = section.Batch;

            var result = new[]
            {
        new
        {
            Id = batch.Id,
            Name = batch.Name,
            Sections = new[]
            {
                new
                {
                    Section = section,
                    Schedules = schedules
                }
            }
        }
            };

            return View("ViewBySection", result);
        }
        public IActionResult ViewBySection()
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Instructor)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Course)
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section).ThenInclude(sec => sec.Batch)
                 .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section).ThenInclude(sec => sec.Room)
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
            ViewBag.CurrentUserDepartmentId = GetCurrentUserDepartmentId();

            return View("ViewBySection", groupedSchedules);
        }
        public IActionResult ViewByBatch(int scheduleSettingId)
        {
            var schedules = _context.Schedules
                .Include(s => s.Allocation)
                    .ThenInclude(a => a.Section).ThenInclude(s => s.Batch)
                     .Include(s => s.Allocation)
        .ThenInclude(a => a.Course)
          .Include(s => s.Allocation)
        .ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot).ThenInclude(t => t.DaysOfWeek)
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
        [HttpPost]
        public async Task<IActionResult> Regenerate()
        {

            var schedules = await _context.Schedules.ToListAsync();
            _context.Schedules.RemoveRange(schedules);
            await _context.SaveChangesAsync();


            return await Generate();
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
        public async Task<IActionResult> SeedData()
        {
            try
            {
                await DataSeeder.SeedFullDataAsync(HttpContext.RequestServices);
                TempData["Success"] = " Seeding complete!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $" Seeding failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SwapSchedules(int scheduleId1, int scheduleId2)
        {
            var currentDepartmentId = GetCurrentUserDepartmentId();
            if (currentDepartmentId == null)
            {
                return Unauthorized("You are not authorized to perform this action.");
            }

            var schedule1 = await _context.Schedules
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot).ThenInclude(t => t.DaysOfWeek)
                .FirstOrDefaultAsync(s => s.Id == scheduleId1);

            var schedule2 = await _context.Schedules
                .Include(s => s.Allocation).ThenInclude(a => a.Instructor)
                .Include(s => s.TimeSlot).ThenInclude(t => t.DaysOfWeek)
                .FirstOrDefaultAsync(s => s.Id == scheduleId2);

            if (schedule1 == null || schedule2 == null)
            {
                return NotFound("One or both schedules not found.");
            }

          
            var schedule1DeptId = await _context.Sections
                .Where(sec => sec.Id == schedule1.Allocation.SectionId)
                .Select(sec => sec.DepartmentId)
                .FirstOrDefaultAsync();

            var schedule2DeptId = await _context.Sections
                .Where(sec => sec.Id == schedule2.Allocation.SectionId)
                .Select(sec => sec.DepartmentId)
                .FirstOrDefaultAsync();

            if (schedule1DeptId != currentDepartmentId || schedule2DeptId != currentDepartmentId)
            {
                return Forbid("You can only swap schedules within your own department.");
            }

            var instructor1Id = schedule1.Allocation.InstructorId;
            var instructor2Id = schedule2.Allocation.InstructorId;

            var timeSlot1 = schedule1.TimeSlot;
            var timeSlot2 = schedule2.TimeSlot;

            var conflict1 = await _context.Schedules
                .Include(s => s.TimeSlot).ThenInclude(t => t.DaysOfWeek)
                .Include(s => s.Allocation)
                .Where(s => s.Allocation.InstructorId == instructor1Id && s.Id != schedule1.Id)
                .FirstOrDefaultAsync(s =>
                    s.TimeSlot.DaysOfWeekId == timeSlot2.DaysOfWeekId &&
                    s.TimeSlot.From < timeSlot2.To &&
                    s.TimeSlot.To > timeSlot2.From);

            var conflict2 = await _context.Schedules
                .Include(s => s.TimeSlot).ThenInclude(t => t.DaysOfWeek)
                .Include(s => s.Allocation)
                .Where(s => s.Allocation.InstructorId == instructor2Id && s.Id != schedule2.Id)
                .FirstOrDefaultAsync(s =>
                    s.TimeSlot.DaysOfWeekId == timeSlot1.DaysOfWeekId &&
                    s.TimeSlot.From < timeSlot1.To &&
                    s.TimeSlot.To > timeSlot1.From);

            if (conflict1 != null || conflict2 != null)
            {
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

            var tempAllocationId = schedule1.AllocationId;
            schedule1.AllocationId = schedule2.AllocationId;
            schedule2.AllocationId = tempAllocationId;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GenerateSectionRoomAssignments()
        {
            var departmentId = GetCurrentUserDepartmentId();
            if (departmentId == null)
            {
                TempData["Error"] = "You are not assigned to any department.";
                return RedirectToAction("Index");
            }
            var unassignedSections = await _context.Sections
                .Where(s => s.DepartmentId == departmentId && s.RoomId == null)
                .OrderBy(s => s.Name)
                .ToListAsync();

            if (!unassignedSections.Any())
            {
                TempData["Info"] = "All sections in your department are already assigned to rooms.";
                return RedirectToAction("ViewSectionRoomAssignments");
            }
            var usedBlockIds = await _context.Sections
                .Where(s => s.DepartmentId == departmentId && s.RoomId != null)
                .Select(s => s.Room!.BlockId)
                .Distinct()
                .ToListAsync();
            var preferredRooms = await _context.Rooms
                .Where(r => usedBlockIds.Contains(r.BlockId))
                .OrderBy(r => r.Block.Name)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();

            var otherRooms = await _context.Rooms
                .Where(r => !usedBlockIds.Contains(r.BlockId))
                .OrderBy(r => r.Block.Name)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();

            var allRooms = preferredRooms.Concat(otherRooms).ToList();

            if (!allRooms.Any())
            {
                TempData["Error"] = "No rooms available. Please create rooms first.";
                return RedirectToAction("Index");
            }

            int roomIndex = 0;
            foreach (var section in unassignedSections)
            {
                section.RoomId = allRooms[roomIndex].Id;
                roomIndex = (roomIndex + 1) % allRooms.Count;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Room assignments generated for {unassignedSections.Count} section(s) in your department.";
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
        private async Task<bool> CheckInstructorConflicts(int instructorId)
        {
            var instructorSchedules = await _context.Schedules
                .Include(s => s.TimeSlot)
                .Where(s => s.Allocation.InstructorId == instructorId)
                .ToListAsync();

            for (int i = 0; i < instructorSchedules.Count; i++)
            {
                for (int j = i + 1; j < instructorSchedules.Count; j++)
                {
                    var s1 = instructorSchedules[i];
                    var s2 = instructorSchedules[j];

                    if (s1.TimeSlot.DaysOfWeekId == s2.TimeSlot.DaysOfWeekId)
                    {
                        if (TimeOverlaps(s1.TimeSlot.From, s1.TimeSlot.To, s2.TimeSlot.From, s2.TimeSlot.To))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private bool TimeOverlaps(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            return (start1 < end2) && (start2 < end1);
        }

        [HttpPost]
        public async Task<IActionResult> ReassignInstructor(int sectionId, int courseId, int newInstructorId)
        {
            var departmentId = GetCurrentUserDepartmentId();
            if (departmentId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any department.";
                return RedirectToAction("Generated");
            }
            var existingAllocation = await _context.Allocations
                .Include(a => a.Section)
                .FirstOrDefaultAsync(a =>
                    a.SectionId == sectionId &&
                    a.CourseId == courseId &&
                    a.Section.DepartmentId == departmentId);

            if (existingAllocation == null)
            {
                TempData["ErrorMessage"] = "Allocation not found or you do not have permission.";
                return RedirectToAction("Generated");
            }

            bool instructorValid = await _context.Allocations
       .AnyAsync(a =>
           a.InstructorId == newInstructorId &&
           a.Section.DepartmentId == departmentId);


            if (!instructorValid)
            {
                TempData["ErrorMessage"] = "Instructor is not valid for your department.";
                return RedirectToAction("Generated");
            }

            existingAllocation.InstructorId = newInstructorId;
            _context.Allocations.Update(existingAllocation);
            await _context.SaveChangesAsync();

            bool hasConflict = await CheckInstructorConflicts(newInstructorId);
            if (hasConflict)
            {
                TempData["WarningMessage"] = "Instructor reassigned, but there may be schedule conflicts.";
            }
            else
            {
                TempData["SuccessMessage"] = "Instructor reassigned successfully.";
            }

            return RedirectToAction("ViewAllocations");
        }

        [HttpGet]
        public async Task<IActionResult> ReassignInstructor(int sectionId, int courseId)
        {
            var departmentId = GetCurrentUserDepartmentId();
            if (departmentId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any department.";
                return RedirectToAction("Generated");
            }
            var allocation = await _context.Allocations
                .Include(a => a.Course)
                .Include(a => a.Section)
                .FirstOrDefaultAsync(a => a.SectionId == sectionId && a.CourseId == courseId && a.Section.DepartmentId == departmentId);

            if (allocation == null)
            {
                TempData["ErrorMessage"] = "Allocation not found or access denied.";
                return RedirectToAction("Generated");
            }

            var instructors = await _context.Allocations
          .Where(a => a.Section.DepartmentId == departmentId)
          .Select(a => a.Instructor)
          .Distinct()
          .ToListAsync();


            ViewBag.SectionId = sectionId;
            ViewBag.CourseId = courseId;
            ViewBag.CourseName = allocation.Course.Name;
            ViewBag.SectionName = allocation.Section.Name;
            ViewBag.Instructors = instructors;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ViewAllocations(string searchInstructor)
        {
            var departmentId = GetCurrentUserDepartmentId();
            if (departmentId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any department.";
                return RedirectToAction("Index");
            }

            var allocationsQuery = _context.Allocations
                .Include(a => a.Course)
                .Include(a => a.Section).ThenInclude(s => s.Department)
                .Include(a => a.Instructor)
                .Where(a => a.Section.DepartmentId == departmentId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchInstructor))
            {
                allocationsQuery = allocationsQuery
                    .Where(a => a.Instructor.FullName.Contains(searchInstructor));
            }

            var allocations = await allocationsQuery.ToListAsync();

            ViewBag.SearchInstructor = searchInstructor;
            ViewBag.Instructors = await _context.Allocations
                                      .Where(a => a.Section.DepartmentId == departmentId)
                                      .Select(a => a.Instructor)
                                      .Distinct()
                                      .ToListAsync();


            return View(allocations);
        }
    }
}
