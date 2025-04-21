namespace ooad.Models;

public class AppointmentListViewModel
{
    public IEnumerable<Appointment> Appointments { get; set; } = new List<Appointment>();
    public IEnumerable<Reminder> Reminders { get; set; } = new List<Reminder>();
    public IEnumerable<GroupMeeting> GroupMeetings { get; set; } = new List<GroupMeeting>();
}