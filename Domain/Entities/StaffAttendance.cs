using Domain.Enums;

namespace Domain.Entities;

public class StaffAttendance : BaseDBModel
{
    public Guid ShopId { get; set; }
    public Guid StaffUserId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public User? StaffUser { get; set; }
}
