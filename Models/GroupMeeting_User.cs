using ooad.Models;

public class GroupMeeting_User
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GroupMeetingId { get; set; }

    // Navigation properties
    public User User { get; set; }
    public GroupMeeting GroupMeeting { get; set; }
}
