using Jumoo.TranslationManager.Automate.Mapping;
using NUnit.Framework;
using Umbraco.Automate.Core.Triggers;

namespace Jumoo.TranslationManager.Automate.Tests.Mapping;

public class TranslationOriginBehaviourTests
{
    [TestCase("Run", AutomationOriginatedEventBehavior.Run)]
    [TestCase("SkipOnCycle", AutomationOriginatedEventBehavior.SkipOnCycle)]
    [TestCase("SkipAlways", AutomationOriginatedEventBehavior.SkipAlways)]
    [TestCase("skiponcycle", AutomationOriginatedEventBehavior.SkipOnCycle)]
    public void Parse_RoundTripsKnownValues(string value, AutomationOriginatedEventBehavior expected)
        => Assert.That(TranslationOriginBehaviour.Parse(value, AutomationOriginatedEventBehavior.Run), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("garbage")]
    public void Parse_FallsBackOnInvalidInput(string? value)
        => Assert.That(
            TranslationOriginBehaviour.Parse(value, AutomationOriginatedEventBehavior.SkipOnCycle),
            Is.EqualTo(AutomationOriginatedEventBehavior.SkipOnCycle));
}
