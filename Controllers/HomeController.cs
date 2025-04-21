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
        int endHours, int endMinutes, string title, string location, bool isReminder, bool isGroupMeeting)
    {
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

        // If neither checkbox is selected, create a regular appointment
        if (!isReminder && !isGroupMeeting)
        {
            var appointment = new Appointment
            {
                UserId = user.Id,
                Title = title,
                StartTime = startTime,
                EndTime = endTime,
                Location = location
            };
            _context.Appointment.Add(appointment);
        }

        // Save all changes at once
        await _context.SaveChangesAsync();
        
        // Redirect to MyAppointment instead of Index to show the new entries
        return RedirectToAction(nameof(MyAppointment));
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
