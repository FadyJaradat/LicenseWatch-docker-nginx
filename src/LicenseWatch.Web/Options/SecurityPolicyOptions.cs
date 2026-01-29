namespace LicenseWatch.Web.Options;

public class SecurityPolicyOptions
{
    public int LoginPermitLimitPerMinute { get; set; } = 10;
    public int AdminPermitLimitPerMinute { get; set; } = 60;
    public int UploadPermitLimitPerMinute { get; set; } = 6;
}
