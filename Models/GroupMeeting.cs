public class GroupMeeting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; }
    public string Location { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ICollection<GroupMeeting_User> GroupMeeting_Users { get; set; } = new List<GroupMeeting_User>();
}
