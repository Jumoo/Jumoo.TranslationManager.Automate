using Jumoo.TranslationManager.Automate.Triggers.Jobs;
using Jumoo.TranslationManager.Core.Models;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Tests.Triggers;

public class TranslationJobMapperTests
{
    [Test]
    public void Map_NoNodesLoaded_ProducesEmptyContentKeys()
    {
        var contentService = new Mock<IContentService>();
        var mapper = new TranslationJobMapper(contentService.Object);
        var job = new TranslationJob { Nodes = [] };

        var output = mapper.Map(job, publish: false, userKey: Guid.NewGuid(), maxContentKeys: 100);

        Assert.That(output.ContentKeys, Is.Empty);
        contentService.Verify(c => c.GetById(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void Map_NodesLoaded_ResolvesDistinctContentKeysCappedAtMax()
    {
        var content1 = Mock.Of<IContent>(c => c.Key == Guid.NewGuid());
        var content2 = Mock.Of<IContent>(c => c.Key == Guid.NewGuid());

        var contentService = new Mock<IContentService>();
        contentService.Setup(c => c.GetById(1)).Returns(content1);
        contentService.Setup(c => c.GetById(2)).Returns(content2);

        var mapper = new TranslationJobMapper(contentService.Object);
        var job = new TranslationJob
        {
            Nodes =
            [
                new TranslationNode { MasterNodeId = 1 },
                new TranslationNode { MasterNodeId = 1 }, // duplicate - must be deduped
                new TranslationNode { MasterNodeId = 2 },
            ],
        };

        var output = mapper.Map(job, publish: false, userKey: Guid.NewGuid(), maxContentKeys: 1);

        Assert.That(output.ContentKeys, Has.Length.EqualTo(1));
        Assert.That(output.ContentKeys[0], Is.EqualTo(content1.Key));
    }

    [Test]
    public void Map_SetsSetKeyOnMarkerInterface()
    {
        var mapper = new TranslationJobMapper(Mock.Of<IContentService>());
        var setKey = Guid.NewGuid();
        var job = new TranslationJob { SetKey = setKey, Nodes = [] };

        var output = mapper.Map(job, publish: false, userKey: Guid.NewGuid(), maxContentKeys: 100);

        Jumoo.TranslationManager.Automate.Dispatch.ITranslationSetScopedOutput scoped = output;
        Assert.That(scoped.GetSetKey(), Is.EqualTo(setKey));
    }
}
