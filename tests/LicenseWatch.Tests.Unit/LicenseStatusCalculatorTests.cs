using LicenseWatch.Core.Services;
using Xunit;

namespace LicenseWatch.Tests.Unit;

public class LicenseStatusCalculatorTests
{
    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData(-1, "Expired")]
    [InlineData(0, "Expired")]
    [InlineData(10, "Critical")]
    [InlineData(45, "Warning")]
    [InlineData(120, "Good")]
    public void ComputeStatus_ReturnsExpectedStatus(int? daysFromNow, string expected)
    {
        DateTime? expiresOn = daysFromNow.HasValue
            ? DateTime.UtcNow.Date.AddDays(daysFromNow.Value)
            : null;

        var actual = LicenseStatusCalculator.ComputeStatus(expiresOn, DateTime.UtcNow);

        Assert.Equal(expected, actual);
    }
}
