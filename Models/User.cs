namespace tran_nguyen_vi_khang_api.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string Role { get; set; } = "Parent";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<EmailOtp> EmailOtps { get; set; } = new List<EmailOtp>();
}
