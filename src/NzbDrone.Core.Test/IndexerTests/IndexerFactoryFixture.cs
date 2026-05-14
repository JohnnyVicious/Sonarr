using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests
{
    [TestFixture]
    public class IndexerFactoryFixture : CoreTest<IndexerFactory>
    {
        private List<IndexerDefinition> _indexers;

        [SetUp]
        public void Setup()
        {
            _indexers = Builder<IndexerDefinition>.CreateListOfSize(3)
                .Build()
                .ToList();

            Mocker.GetMock<IIndexerRepository>()
                  .Setup(s => s.All())
                  .Returns(_indexers);
        }

        [Test]
        public void resolve_indexer_should_return_by_id()
        {
            var result = Subject.ResolveIndexer(_indexers[0].Id, null);

            result.Should().NotBeNull();
            result.Should().Be(_indexers[0]);
        }

        [Test]
        public void resolve_indexer_should_return_by_name()
        {
            var result = Subject.ResolveIndexer(null, _indexers[1].Name);

            result.Should().NotBeNull();
            result.Should().Be(_indexers[1]);
        }

        [Test]
        public void resolve_indexer_should_return_when_id_and_name_match()
        {
            var result = Subject.ResolveIndexer(_indexers[2].Id, _indexers[2].Name);

            result.Should().NotBeNull();
            result.Should().Be(_indexers[2]);
        }

        [Test]
        public void resolve_indexer_should_throw_when_id_not_found()
        {
            Assert.Throws<ResolveIndexerException>(() => Subject.ResolveIndexer(999, null));
        }

        [Test]
        public void resolve_indexer_should_throw_when_name_not_found()
        {
            Assert.Throws<ResolveIndexerException>(() => Subject.ResolveIndexer(null, "NonExistent"));
        }

        [Test]
        public void resolve_indexer_should_throw_when_id_and_name_mismatch()
        {
            Assert.Throws<ResolveIndexerException>(() => Subject.ResolveIndexer(_indexers[0].Id, _indexers[1].Name));
        }

        [Test]
        public void resolve_indexer_should_return_null_when_both_null()
        {
            var result = Subject.ResolveIndexer(null, null);

            result.Should().BeNull();
        }

        [Test]
        public void resolve_indexer_should_return_null_when_both_empty()
        {
            var result = Subject.ResolveIndexer(0, "");

            result.Should().BeNull();
        }

        [Test]
        public void find_by_name_should_delegate_to_repository()
        {
            var definition = Builder<IndexerDefinition>.CreateNew().Build();

            Mocker.GetMock<IIndexerRepository>()
                  .Setup(s => s.FindByName("TestIndexer"))
                  .Returns(definition);

            var result = Subject.FindByName("TestIndexer");

            result.Should().Be(definition);
            Mocker.GetMock<IIndexerRepository>()
                  .Verify(v => v.FindByName("TestIndexer"), Times.Once());
        }

        [Test]
        public void find_by_name_should_return_null_when_not_found()
        {
            Mocker.GetMock<IIndexerRepository>()
                  .Setup(s => s.FindByName("Missing"))
                  .Returns((IndexerDefinition)null);

            var result = Subject.FindByName("Missing");

            result.Should().BeNull();
        }

        [Test]
        public void set_provider_characteristics_should_set_protocol()
        {
            var mockIndexer = new Mock<IIndexer>();
            mockIndexer.SetupGet(s => s.Protocol).Returns(DownloadProtocol.Usenet);
            mockIndexer.SetupGet(s => s.SupportsRss).Returns(true);
            mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            var definition = new IndexerDefinition();

            Subject.SetProviderCharacteristics(mockIndexer.Object, definition);

            definition.Protocol.Should().Be(DownloadProtocol.Usenet);
            definition.SupportsRss.Should().BeTrue();
            definition.SupportsSearch.Should().BeTrue();
        }

        [Test]
        public void set_provider_characteristics_should_set_torrent_protocol()
        {
            var mockIndexer = new Mock<IIndexer>();
            mockIndexer.SetupGet(s => s.Protocol).Returns(DownloadProtocol.Torrent);
            mockIndexer.SetupGet(s => s.SupportsRss).Returns(false);
            mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            var definition = new IndexerDefinition();

            Subject.SetProviderCharacteristics(mockIndexer.Object, definition);

            definition.Protocol.Should().Be(DownloadProtocol.Torrent);
            definition.SupportsRss.Should().BeFalse();
            definition.SupportsSearch.Should().BeTrue();
        }

        [Test]
        public void test_should_record_success_for_existing_definition_with_valid_result()
        {
            var mockIndexer = new Mock<IIndexer>();
            var definition = Builder<IndexerDefinition>.CreateNew()
                .With(d => d.Id = 5)
                .With(d => d.Implementation = mockIndexer.Object.GetType().Name)
                .Build();

            mockIndexer.SetupProperty(s => s.Definition);
            mockIndexer.Setup(s => s.Test()).Returns(new FluentValidation.Results.ValidationResult());
            mockIndexer.SetupGet(s => s.Protocol).Returns(DownloadProtocol.Usenet);
            mockIndexer.SetupGet(s => s.SupportsRss).Returns(true);
            mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            Mocker.SetConstant<IEnumerable<IIndexer>>(new[] { mockIndexer.Object });
            Mocker.GetMock<IServiceProvider>()
                  .Setup(s => s.GetService(mockIndexer.Object.GetType()))
                  .Returns(mockIndexer.Object);

            var result = Subject.Test(definition);

            result.IsValid.Should().BeTrue();
            mockIndexer.Verify(v => v.Test(), Times.Once());
            Mocker.GetMock<IIndexerStatusService>()
                  .Verify(v => v.RecordSuccess(5), Times.Once());
        }
    }
}
