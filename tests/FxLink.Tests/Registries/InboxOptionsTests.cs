using FxLink.Exceptions;
using FxLink.Registries;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class InboxOptionsTests
{
    [Fact]
    public void Validate_does_not_throw_for_the_default_values()
    {
        var options = new InboxOptions();

        Should.NotThrow(options.Validate);
    }

    [Theory]
    [InlineData(nameof(InboxOptions.ClaimDuration))]
    [InlineData(nameof(InboxOptions.ClaimRenewInterval))]
    [InlineData(nameof(InboxOptions.RetentionPeriod))]
    [InlineData(nameof(InboxOptions.CleanupInterval))]
    public void Validate_throws_when_a_TimeSpan_property_is_zero_or_negative(string propertyName)
    {
        var options = new InboxOptions();
        typeof(InboxOptions).GetProperty(propertyName)!.SetValue(options, TimeSpan.Zero);

        Should.Throw<FxLinkException.InboxOptionsMustBePositive>(options.Validate);
    }

    [Fact]
    public void Validate_throws_when_ClaimRenewInterval_equals_ClaimDuration()
    {
        var options = new InboxOptions { ClaimDuration = TimeSpan.FromMinutes(1), ClaimRenewInterval = TimeSpan.FromMinutes(1) };

        Should.Throw<FxLinkException.InboxClaimRenewIntervalTooLong>(options.Validate);
    }

    [Fact]
    public void Validate_throws_when_ClaimRenewInterval_exceeds_ClaimDuration()
    {
        var options = new InboxOptions { ClaimDuration = TimeSpan.FromMinutes(1), ClaimRenewInterval = TimeSpan.FromMinutes(2) };

        Should.Throw<FxLinkException.InboxClaimRenewIntervalTooLong>(options.Validate);
    }

    [Fact]
    public void Validate_does_not_throw_when_ClaimRenewInterval_is_comfortably_less_than_ClaimDuration()
    {
        var options = new InboxOptions { ClaimDuration = TimeSpan.FromMinutes(5), ClaimRenewInterval = TimeSpan.FromMinutes(2) };

        Should.NotThrow(options.Validate);
    }
}
