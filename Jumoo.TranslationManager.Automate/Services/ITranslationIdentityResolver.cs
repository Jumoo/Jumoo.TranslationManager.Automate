using Umbraco.Cms.Core.Models.Membership;

namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>
/// Resolves the workspace service account key to an <see cref="IUser"/>.
/// <c>TranslationJobService.CreateJobAsync</c> takes an <see cref="IUser"/>, not a key, so
/// every translation run needs this round-trip.
/// </summary>
public interface ITranslationIdentityResolver
{
    Task<IUser?> ResolveAsync(Guid serviceAccountKey, CancellationToken cancellationToken);
}
