using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ooad.Models;

public class MainContext : DbContext
{
    public required DbSet<Appointment> Appointment { get; set; } = null!;
    public required DbSet<Reminder> Reminder { get; set; } = null!;
    public required DbSet<GroupMeeting> GroupMeeting { get; set; } = null!;
     public required DbSet<GroupMeeting_User> GroupMeeting_User { get; set; } = null!;
    public required DbSet<User> User { get; set; } = null!;

    public MainContext() { }

    public MainContext(DbContextOptions<MainContext> options)
        : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string conn_string = new SqlConnectionStringBuilder
        {
            DataSource = @"DESKTOP-F65EUHO\SQLEXPRESS",
            InitialCatalog = "DBOOAD",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 60,
        }.ConnectionString;
        optionsBuilder.UseSqlServer(conn_string);
    }
}

/* EOF */
