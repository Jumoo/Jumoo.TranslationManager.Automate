using Jumoo.TranslationManager.Automate.Triggers.Jobs;
using NUnit.Framework;

namespace Jumoo.TranslationManager.Automate.Tests.Triggers;

public class TranslationJobTriggerFilterTests
{
    private static TranslationJobTriggerOutput MakeOutput(
        Guid? setKey = null, Guid? providerKey = null, string targetCulture = "fr-FR", bool isError = false)
        => new()
        {
            SetKey = setKey ?? Guid.NewGuid(),
            ProviderKey = providerKey ?? Guid.NewGuid(),
            TargetCulture = targetCulture,
            IsError = isError,
        };

    [Test]
    public void Matches_NullSettings_ReturnsTrue()
        => Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(), null), Is.True);

    [Test]
    public void Matches_IgnoreErrorsTrue_ErrorJob_ReturnsFalse()
    {
        var settings = new TranslationJobTriggerSettings { IgnoreErrors = true };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(isError: true), settings), Is.False);
    }

    [Test]
    public void Matches_SetKeyFilter_NonMatchingSet_ReturnsFalse()
    {
        var settings = new TranslationJobTriggerSettings { SetKeys = Guid.NewGuid().ToString() };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(), settings), Is.False);
    }

    [Test]
    public void Matches_SetKeyFilter_MatchingSet_ReturnsTrue()
    {
        var setKey = Guid.NewGuid();
        var settings = new TranslationJobTriggerSettings { SetKeys = setKey.ToString() };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(setKey: setKey), settings), Is.True);
    }

    [Test]
    public void Matches_CultureFilter_NonMatchingCulture_ReturnsFalse()
    {
        var settings = new TranslationJobTriggerSettings { Cultures = "de-DE, es-ES" };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(targetCulture: "fr-FR"), settings), Is.False);
    }

    [Test]
    public void Matches_CultureFilter_MatchingCulture_ReturnsTrue()
    {
        var settings = new TranslationJobTriggerSettings { Cultures = "de-DE, fr-FR" };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(targetCulture: "fr-FR"), settings), Is.True);
    }

    [Test]
    public void Matches_ProviderKeyFilter_NonMatching_ReturnsFalse()
    {
        var settings = new TranslationJobTriggerSettings { ProviderKeys = Guid.NewGuid().ToString() };
        Assert.That(TranslationJobTriggerFilter.Matches(MakeOutput(), settings), Is.False);
    }
}
