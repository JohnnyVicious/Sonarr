using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Sonarr.Http.Frontend.Mappers;

namespace NzbDrone.Http.Test.Frontend.Mappers
{
    [TestFixture]
    public class LogFileMapperFixture : TestBase<LogFileMapper>
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
        public void should_handle_logfile_url_with_txt_extension()
        {
            Subject.CanHandle("/logfile/sonarr.txt").Should().BeTrue();
        }

        [Test]
        public void should_handle_logfile_url_with_rollover_txt()
        {
            Subject.CanHandle("/logfile/sonarr.0.txt").Should().BeTrue();
        }

        [Test]
        public void should_not_handle_logfile_root()
        {
            Subject.CanHandle("/logfile/").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_logfile_with_non_txt_extension()
        {
            Subject.CanHandle("/logfile/test.log").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_logfile_without_trailing_slash_prefix()
        {
            Subject.CanHandle("/logfiles/sonarr.txt").Should().BeFalse();
        }

        [Test]
        public void should_not_handle_api_url()
        {
            Subject.CanHandle("/api/v3/log").Should().BeFalse();
        }

        [Test]
        public void should_map_to_log_folder_with_filename()
        {
            var result = Subject.Map("/logfile/sonarr.txt");

            // GetLogFolder() returns AppDataFolder + "logs"
            result.Should().Be(Path.Combine($"{S}app{S}data", "logs", "sonarr.txt"));
        }

        [Test]
        public void should_extract_filename_only_from_path()
        {
            var result = Subject.Map("/logfile/sonarr.0.txt");

            result.Should().Be(Path.Combine($"{S}app{S}data", "logs", "sonarr.0.txt"));
        }

        [Test]
        public void should_strip_traversal_to_filename_only()
        {
            // Path.GetFileName() strips directory components, so traversal
            // sequences like ../../etc/passwd resolve to just "passwd"
            var result = Subject.Map("/logfile/../../etc/passwd");

            result.Should().Be(Path.Combine($"{S}app{S}data", "logs", "passwd"));
        }

        [Test]
        public void should_strip_deep_traversal_to_filename_only()
        {
            var result = Subject.Map("/logfile/../../../secret.txt");

            // Path.GetFileName extracts only "secret.txt"
            result.Should().Be(Path.Combine($"{S}app{S}data", "logs", "secret.txt"));
        }
    }
}
