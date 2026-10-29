using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Location;

public class CreateLocationRefreshRequestDto
{
    [Required]
    public int ChildId { get; set; }

    public int? DeviceId { get; set; }
}

