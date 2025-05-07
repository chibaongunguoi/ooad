using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ooad.Models;

namespace ooad.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MainContext _context;

    public HomeController(ILogger<HomeController> logger, MainContext context)
    {
        _logger = logger;
        _context = context;
    }

    private async Task<Appointment> CheckForConflict(int userId, DateTime startTime, DateTime endTime)
    {
        return await _context.Appointment
            .FirstOrDefaultAsync(a => 
                a.UserId == userId && 
                ((startTime >= a.StartTime && startTime < a.EndTime) ||  // New appointment starts during existing one
                (endTime > a.StartTime && endTime <= a.EndTime) ||      // New appointment ends during existing one
                (startTime <= a.StartTime && endTime >= a.EndTime)));   // New appointment completely overlaps existing one
    }

    private async Task<GroupMeeting> CheckForMatchingGroupMeeting(int userId, string title, DateTime startTime, DateTime endTime)
    {
        return await _context.GroupMeeting
            .Include(g => g.GroupMeeting_Users)
            .FirstOrDefaultAsync(g => 
                g.Title.ToLower() == title.ToLower() && 
                g.StartTime == startTime && 
                g.EndTime == endTime &&
                !g.GroupMeeting_Users.Any(gu => gu.UserId == userId));
    }

    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("Username") == null)
        {
            return RedirectToAction("Login", "Account");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment(DateTime appointmentDate, int hours, int minutes, 
        int endHours, int endMinutes, string title, string location, bool isReminder, bool isGroupMeeting, bool forceCreate = false, bool joinGroup = false)
    {
        _logger.LogInformation($"isReminder value: {isReminder}");
        Console.WriteLine($"isReminder value: {isReminder}");

        if (HttpContext.Session.GetString("Username") == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Server-side validation
        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError("title", "Tiêu đề không được để trống");
            return View("Index");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            ModelState.AddModelError("location", "Địa điểm không được để trống");
            return View("Index");
        }

        var startTime = appointmentDate.Date.AddHours(hours).AddMinutes(minutes);
        var endTime = appointmentDate.Date.AddHours(endHours).AddMinutes(endMinutes);

        if (endTime <= startTime)
        {
            ModelState.AddModelError("", "Thời gian kết thúc phải sau thời gian bắt đầu");
            return View("Index");
        }

        var username = HttpContext.Session.GetString("Username");
        var user = await _context.User.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Check for matching group meeting if not forcing creation
        if (!forceCreate && !joinGroup)
        {
            var matchingGroupMeeting = await CheckForMatchingGroupMeeting(user.Id, title, startTime, endTime);
            if (matchingGroupMeeting != null)
            {
                TempData["MatchingGroupMeeting"] = true;
                TempData["MatchingGroupMeetingData"] = System.Text.Json.JsonSerializer.Serialize(new {
                    Id = matchingGroupMeeting.Id,
                    Title = matchingGroupMeeting.Title,
                    Location = matchingGroupMeeting.Location,
                    StartTime = matchingGroupMeeting.StartTime,
                    EndTime = matchingGroupMeeting.EndTime
                });
                TempData["FormData"] = System.Text.Json.JsonSerializer.Serialize(new {
                    Title = title,
                    Location = location,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsReminder = isReminder,
                    IsGroupMeeting = isGroupMeeting
                });
                return RedirectToAction(nameof(Index));
            }
        }

        // Check for conflicts if not forcing creation
        if (!forceCreate)
        {
            var conflict = await CheckForConflict(user.Id, startTime, endTime);
            if (conflict != null)
            {
                TempData["Conflict"] = true;
                TempData["ConflictingAppointment"] = System.Text.Json.JsonSerializer.Serialize(new {
                    Title = conflict.Title,
                    StartTime = conflict.StartTime,
                    EndTime = conflict.EndTime,
                    Location = conflict.Location
                });
                TempData["FormData"] = System.Text.Json.JsonSerializer.Serialize(new {
                    Title = title,
                    Location = location,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsReminder = isReminder,
                    IsGroupMeeting = isGroupMeeting
                });
                return RedirectToAction(nameof(Index));
            }
        }

        if (joinGroup)
        {
            // Get the group meeting ID from TempData
            var groupMeetingData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(TempData["MatchingGroupMeetingData"].ToString());
            var groupMeetingId = groupMeetingData.GetProperty("Id").GetInt32();

            var groupMeetingUser = new GroupMeeting_User
            {
                UserId = user.Id,
                GroupMeetingId = groupMeetingId
            };
            _context.GroupMeeting_User.Add(groupMeetingUser);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyAppointment));
        }

        // Always create an Appointment record
        var appointment = new Appointment
        {
            UserId = user.Id,
            Title = title,
            StartTime = startTime,
            EndTime = endTime,
            Location = location
        };
        _context.Appointment.Add(appointment);

        // Handle Reminder if selected
        if (isReminder)
        {
            var reminder = new Reminder
            {
                UserId = user.Id,
                Title = title,
                StartTime = startTime,
                EndTime = endTime,
                Location = location
            };
            _context.Reminder.Add(reminder);
        }

        // Handle GroupMeeting if selected
        if (isGroupMeeting)
        {
            var groupMeeting = new GroupMeeting
            {
                UserId = user.Id,
                Title = title,
                StartTime = startTime,
                EndTime = endTime,
                Location = location
            };
            _context.GroupMeeting.Add(groupMeeting);
            
            // Save changes to get the GroupMeeting ID
            await _context.SaveChangesAsync();

            var groupMeetingUser = new GroupMeeting_User
            {
                UserId = user.Id,
                GroupMeetingId = groupMeeting.Id
            };
            _context.GroupMeeting_User.Add(groupMeetingUser);
        }

        // Save all changes
        await _context.SaveChangesAsync();
        
        // Redirect to MyAppointment to show the new entries
        return RedirectToAction(nameof(MyAppointment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAppointment([FromBody] DeleteAppointmentModel model)
    {
        try
        {
            _logger.LogInformation($"Attempting to delete {model.Type} with ID {model.Id}");
            
            if (HttpContext.Session.GetString("Username") == null)
            {
                _logger.LogWarning("Unauthorized: No username in session");
                return Unauthorized();
            }

            var username = HttpContext.Session.GetString("Username");
            var user = await _context.User.FirstOrDefaultAsync(u => u.Username == username);
            
            if (user == null)
            {
                _logger.LogWarning($"User not found: {username}");
                return Unauthorized();
            }

            _logger.LogInformation($"User {username} attempting to delete {model.Type}");

            switch (model.Type.ToLower())
            {
                case "appointment":
                    var appointment = await _context.Appointment
                        .FirstOrDefaultAsync(a => a.Id == model.Id && a.UserId == user.Id);
                    if (appointment != null)
                    {
                        _logger.LogInformation($"Deleting appointment {appointment.Id}: {appointment.Title}");
                        _context.Appointment.Remove(appointment);
                    }
                    else
                    {
                        _logger.LogWarning($"Appointment {model.Id} not found or doesn't belong to user {user.Id}");
                        return NotFound();
                    }
                    break;

                case "reminder":
                    var reminder = await _context.Reminder
                        .FirstOrDefaultAsync(r => r.Id == model.Id && r.UserId == user.Id);
                    if (reminder != null)
                    {
                        _logger.LogInformation($"Deleting reminder {reminder.Id}: {reminder.Title}");
                        _context.Reminder.Remove(reminder);
                    }
                    else
                    {
                        _logger.LogWarning($"Reminder {model.Id} not found or doesn't belong to user {user.Id}");
                        return NotFound();
                    }
                    break;

                case "groupmeeting":
                    var groupMeeting = await _context.GroupMeeting
                        .Include(g => g.GroupMeeting_Users)
                        .FirstOrDefaultAsync(g => g.Id == model.Id && g.UserId == user.Id);
                    if (groupMeeting != null)
                    {
                        _logger.LogInformation($"Deleting group meeting {groupMeeting.Id}: {groupMeeting.Title}");
                        _context.GroupMeeting_User.RemoveRange(groupMeeting.GroupMeeting_Users);
                        _context.GroupMeeting.Remove(groupMeeting);
                    }
                    else
                    {
                        _logger.LogWarning($"Group meeting {model.Id} not found or doesn't belong to user {user.Id}");
                        return NotFound();
                    }
                    break;

                default:
                    _logger.LogWarning($"Invalid type specified: {model.Type}");
                    return BadRequest($"Invalid type: {model.Type}");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully deleted {model.Type} with ID {model.Id}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting {model.Type} with ID {model.Id}");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetGroupMeetingParticipants(int id)
    {
        if (HttpContext.Session.GetString("Username") == null)
        {
            return Unauthorized();
        }

        var username = HttpContext.Session.GetString("Username");
        var currentUser = await _context.User.FirstOrDefaultAsync(u => u.Username == username);
        
        if (currentUser == null)
        {
            return Unauthorized();
        }

        // Check if the current user is a participant of this meeting
        var isParticipant = await _context.GroupMeeting_User
            .AnyAsync(gu => gu.GroupMeetingId == id && gu.UserId == currentUser.Id);

        if (!isParticipant)
        {
            return Unauthorized();
        }

        var participants = await _context.GroupMeeting_User
            .Include(gu => gu.User)
            .Where(gu => gu.GroupMeetingId == id)
            .Select(gu => new { 
                Username = gu.User.Username,
                IsCreator = gu.GroupMeeting.UserId == gu.UserId
            })
            .ToListAsync();

        return Json(participants);
    }

    public async Task<IActionResult> MyAppointment()
    {
        if (HttpContext.Session.GetString("Username") == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var username = HttpContext.Session.GetString("Username");
        var user = await _context.User.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var viewModel = new AppointmentListViewModel
        {
            Appointments = await _context.Appointment.Where(a => a.UserId == user.Id).ToListAsync(),
            Reminders = await _context.Reminder.Where(r => r.UserId == user.Id).ToListAsync(),
            GroupMeetings = await _context.GroupMeeting
                .Include(g => g.GroupMeeting_Users)
                .Where(g => g.GroupMeeting_Users.Any(gu => gu.UserId == user.Id))
                .ToListAsync()
        };

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
