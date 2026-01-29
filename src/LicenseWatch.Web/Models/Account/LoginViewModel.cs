using System.ComponentModel.DataAnnotations;

namespace LicenseWatch.Web.Models.Account;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? AlertMessage { get; set; }
}
