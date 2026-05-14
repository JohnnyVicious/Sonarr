using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Exclusions;
using NzbDrone.Core.ImportLists.ImportListItems;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ImportLists
{
    [TestFixture]
    public class ImportListSyncServiceFixture : CoreTest<ImportListSyncService>
    {
        private ImportListFetchResult _importListFetchResult;

        [SetUp]
        public void Setup()
        {
            _importListFetchResult = new ImportListFetchResult
            {
                Series = new List<ImportListItemInfo>()
            };

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(false))
                  .Returns(new List<IImportList>());

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList>());

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.ListSyncLevel)
                  .Returns(ListSyncLevelType.Disabled);
        }

        [Test]
        public void should_not_sync_if_no_lists_with_automatic_add_enabled()
        {
            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Verify(v => v.Fetch(), Times.Never());
        }

        [Test]
        public void should_fetch_all_lists_when_no_definition_id()
        {
            var mockList = new Mock<IImportList>();
            mockList.SetupGet(s => s.Definition)
                    .Returns(new ImportListDefinition { Id = 1, EnableAutomaticAdd = true });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList> { mockList.Object });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.Fetch())
                  .Returns(_importListFetchResult);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Verify(v => v.Fetch(), Times.Once());
        }

        [Test]
        public void should_fetch_single_list_when_definition_id_provided()
        {
            var definition = new ImportListDefinition { Id = 5, Name = "TestList" };

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.Get(5))
                  .Returns(definition);

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.FetchSingleList(definition))
                  .Returns(_importListFetchResult);

            Subject.Execute(new ImportListSyncCommand { DefinitionId = 5 });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Verify(v => v.FetchSingleList(definition), Times.Once());
        }

        [Test]
        public void should_not_add_series_that_already_exists()
        {
            var listItems = new List<ImportListItemInfo>
            {
                new ImportListItemInfo
                {
                    TvdbId = 100,
                    Title = "Existing Series",
                    ImportListId = 1
                }
            };

            _importListFetchResult.Series = listItems;

            var mockList = new Mock<IImportList>();
            mockList.SetupGet(s => s.Definition)
                    .Returns(new ImportListDefinition { Id = 1, EnableAutomaticAdd = true });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList> { mockList.Object });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>
                  {
                      new ImportListDefinition { Id = 1, EnableAutomaticAdd = true }
                  });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.Fetch())
                  .Returns(_importListFetchResult);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int> { 100 });

            Mocker.GetMock<IImportListExclusionService>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListExclusion>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(v => v.AddSeries(It.Is<List<Series>>(s => s.Count == 0), true), Times.Once());
        }

        [Test]
        public void should_not_add_series_on_exclusion_list()
        {
            var listItems = new List<ImportListItemInfo>
            {
                new ImportListItemInfo
                {
                    TvdbId = 200,
                    Title = "Excluded Series",
                    ImportListId = 1
                }
            };

            _importListFetchResult.Series = listItems;

            var mockList = new Mock<IImportList>();
            mockList.SetupGet(s => s.Definition)
                    .Returns(new ImportListDefinition { Id = 1, EnableAutomaticAdd = true });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList> { mockList.Object });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>
                  {
                      new ImportListDefinition { Id = 1, EnableAutomaticAdd = true }
                  });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.Fetch())
                  .Returns(_importListFetchResult);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<IImportListExclusionService>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListExclusion>
                  {
                      new ImportListExclusion { TvdbId = 200 }
                  });

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(v => v.AddSeries(It.Is<List<Series>>(s => s.Count == 0), true), Times.Once());
        }

        [Test]
        public void should_add_new_series_from_list()
        {
            var listItems = new List<ImportListItemInfo>
            {
                new ImportListItemInfo
                {
                    TvdbId = 300,
                    Title = "New Series",
                    ImportListId = 1
                }
            };

            _importListFetchResult.Series = listItems;

            var mockList = new Mock<IImportList>();
            mockList.SetupGet(s => s.Definition)
                    .Returns(new ImportListDefinition { Id = 1, EnableAutomaticAdd = true });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList> { mockList.Object });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>
                  {
                      new ImportListDefinition
                      {
                          Id = 1,
                          EnableAutomaticAdd = true,
                          ShouldMonitor = MonitorTypes.All,
                          RootFolderPath = "/tv",
                          QualityProfileId = 1,
                          SeriesType = SeriesTypes.Standard,
                          SeasonFolder = true,
                          Tags = new HashSet<int>()
                      }
                  });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.Fetch())
                  .Returns(_importListFetchResult);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<IImportListExclusionService>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListExclusion>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(v => v.AddSeries(It.Is<List<Series>>(s => s.Count == 1 && s.First().TvdbId == 300), true), Times.Once());
        }

        [Test]
        public void should_skip_items_with_no_tvdb_id()
        {
            var listItems = new List<ImportListItemInfo>
            {
                new ImportListItemInfo
                {
                    TvdbId = 0,
                    Title = "Unknown Series",
                    ImportListId = 1
                }
            };

            _importListFetchResult.Series = listItems;

            var mockList = new Mock<IImportList>();
            mockList.SetupGet(s => s.Definition)
                    .Returns(new ImportListDefinition { Id = 1, EnableAutomaticAdd = true });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.AutomaticAddEnabled(true))
                  .Returns(new List<IImportList> { mockList.Object });

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>
                  {
                      new ImportListDefinition { Id = 1, EnableAutomaticAdd = true }
                  });

            Mocker.GetMock<IFetchAndParseImportList>()
                  .Setup(s => s.Fetch())
                  .Returns(_importListFetchResult);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(new List<int>());

            Mocker.GetMock<IImportListExclusionService>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListExclusion>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddSeriesService>()
                  .Verify(v => v.AddSeries(It.Is<List<Series>>(s => s.Count == 0), true), Times.Once());
        }
    }
}
