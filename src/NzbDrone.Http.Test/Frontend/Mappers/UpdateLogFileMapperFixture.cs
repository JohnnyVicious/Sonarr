using System.IO; // NOSONAR
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class UpdateLogFileMapperFixture : TestBase<UpdateLogFileMapper>
    {
        private static readonly char S = Path.DirectorySeparatorChar;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(c => c.AppDataFolder)
                  .Returns($"{S}app{S}data");
        }

        [Test]
        public void should_handle_updatelogfile_url_with_txt_extension()
        {
            Subject.CanHandle("/updatelogfile/2024.01.01-12.00.txt").Should().BeTrue();
        }

        [Test]
        public void should_handle_updatelogfile_url_with_simple_name()
        {
            Subject.CanHandle("/updatelogfile/update.txt").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_updatelogfile_root()
        {
            Subject.CanHandle("/updatelogfile/").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_updatelogfile_with_non_txt_extension()
        {
            Subject.CanHandle("/updatelogfile/test.log").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_url_without_updatelogfile_prefix()
        {
            Subject.CanHandle("/logfile/sonarr.txt").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_api_url()
        {
            Subject.CanHandle("/api/v3/update").Should().BeFalse();
        }

        [Test]
        public void should_map_to_update_log_folder_with_filename()
        {
            var result = Subject.Map("/updatelogfile/2024.01.01-12.00.txt");

            // GetUpdateLogFolder() returns AppDataFolder + "UpdateLogs" + DirectorySeparatorChar
            // Path.Combine handles the trailing separator
            result.Should().Contain("UpdateLogs");
            result.Should().EndWith("2024.01.01-12.00.txt");
        }

        [Test]
        public void should_extract_filename_only_from_path()
        {
            var result = Subject.Map("/updatelogfile/update.txt");

            result.Should().EndWith($"{S}update.txt");
        }

        [Test]
        public void should_strip_traversal_to_filename_only()
        {
            // Path.GetFileName() strips directory traversal sequences,
            // so ../../etc/passwd becomes just "passwd"
            var result = Subject.Map("/updatelogfile/../../etc/passwd");

            result.Should().EndWith($"{S}passwd");

            // The result should stay within the update log folder
            result.Should().Contain("UpdateLogs");
        }

        [Test]
        public void should_strip_deep_traversal_to_filename_only()
        {
            var result = Subject.Map("/updatelogfile/../../../secret.txt");

            // Path.GetFileName extracts only "secret.txt"
            result.Should().EndWith($"{S}secret.txt");
            result.Should().Contain("UpdateLogs");
        }
    }
}
