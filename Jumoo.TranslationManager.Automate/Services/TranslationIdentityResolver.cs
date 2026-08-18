using System.Collections.Concurrent;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Services;

/// <inheritdoc cref="ITranslationIdentityResolver" />
internal sealed class TranslationIdentityResolver : ITranslationIdentityResolver
{
    private readonly IUserService _userService;
    private readonly ConcurrentDictionary<Guid, IUser> _cache = new();

    public TranslationIdentityResolver(IUserService userService) => _userService = userService;

    public async Task<IUser?> ResolveAsync(Guid serviceAccountKey, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(serviceAccountKey, out var cached))
            return cached;

        var user = await _userService.GetAsync(serviceAccountKey);
        if (user is not null)
            _cache[serviceAccountKey] = user;

        return user;
    }
}
