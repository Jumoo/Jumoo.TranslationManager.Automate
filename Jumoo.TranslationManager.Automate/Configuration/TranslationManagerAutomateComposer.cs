using Jumoo.TranslationManager.Core.Boot;
using Jumoo.TranslationManager.Automate.Dispatch;
using Jumoo.TranslationManager.Automate.Services;
using Jumoo.TranslationManager.Automate.Triggers.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Jumoo.TranslationManager.Automate.Configuration;

/// <summary>
/// Actions and triggers are discovered by Umbraco's <c>TypeLoader</c> attribute scan
/// (Automate's own <c>[Action]</c>/<c>[Trigger]</c> scan) - no explicit registration needed
/// for those. This composer only wires up the shared services they take as constructor
/// dependencies, plus options binding and the dispatch authorizer.
///
/// <c>[ComposeAfter(typeof(TranslationComposer))]</c> because <see cref="TranslationManagerFacade"/>
/// resolves Translation Manager's own services from DI, and those are registered by TM's
/// composer.
///
/// No <c>[ComposeAfter]</c> on any Umbraco.Automate composer here - this project references
/// only <c>Umbraco.Automate.Core</c> (Automate's stable extensibility surface); the startup
/// composer type lives in <c>Umbraco.Automate.Startup</c>, which is deliberately not
/// referenced. Nothing this composer registers depends on Automate's own composition order.
/// </summary>
[ComposeAfter(typeof(TranslationComposer))]
public class TranslationManagerAutomateComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AddTranslationManagerAutomate();
}

public static class BuilderTranslationManagerAutomateExtensions
{
    public static IUmbracoBuilder AddTranslationManagerAutomate(this IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton<ITranslationManagerFacade, TranslationManagerFacade>();

        // TryAddSingleton so a future companion package contends for the same gate as a
        // translation run started here instead of creating a second, independent lock.
        builder.Services.TryAddSingleton<ITranslationOperationGate, TranslationOperationGate>();
        builder.Services.TryAddSingleton<ITranslationIdentityResolver, TranslationIdentityResolver>();
        builder.Services.AddSingleton<TranslationRunner>();
        builder.Services.AddSingleton<TranslationJobMapper>();

        builder.AutomateTriggerDispatchAuthorizers().Add<TranslationScopedDispatchAuthorizer>();

        builder.Services.AddOptions<TranslationManagerAutomateOptions>()
            .Bind(builder.Config.GetSection(TranslationManagerAutomateOptions.Section));

        return builder;
    }
}
