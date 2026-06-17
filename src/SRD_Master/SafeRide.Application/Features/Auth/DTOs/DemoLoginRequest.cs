using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafeRide.Application.Features.Auth.DTOs;

public class DemoLoginRequest
{
    public string Provider { get; set; } = "Phone";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}
