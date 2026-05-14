using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Options;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class ConfigFileProviderFixture : CoreTest<ConfigFileProvider>
    {
        private const string CONFIG_XML = "<Config><Port>8989</Port><SslPort>9898</SslPort><EnableSsl>False</EnableSsl><LaunchBrowser>True</LaunchBrowser><ApiKey>testApiKey123456789012345678</ApiKey><AuthenticationMethod>None</AuthenticationMethod><Branch>main</Branch><LogLevel>debug</LogLevel><UrlBase></UrlBase></Config>";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(s => s.AppDataFolder)
                  .Returns(Path.Combine(Path.GetTempPath(), "sonarr_test"));

            Mocker.SetConstant<IOptions<PostgresOptions>>(Options.Create(new PostgresOptions()));
            Mocker.SetConstant<IOptions<AuthOptions>>(Options.Create(new AuthOptions()));
            Mocker.SetConstant<IOptions<AppOptions>>(Options.Create(new AppOptions()));
            Mocker.SetConstant<IOptions<ServerOptions>>(Options.Create(new ServerOptions()));
            Mocker.SetConstant<IOptions<UpdateOptions>>(Options.Create(new UpdateOptions()));
            Mocker.SetConstant<IOptions<LogOptions>>(Options.Create(new LogOptions()));

            Mocker.SetConstant<ICacheManager>(new CacheManager());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.ReadAllText(It.IsAny<string>()))
                  .Returns(CONFIG_XML);
        }

        [Test]
        public void should_return_default_port_when_not_set_in_options()
        {
            Subject.Port.Should().Be(8989);
        }

        [Test]
        public void should_return_default_ssl_port()
        {
            Subject.SslPort.Should().Be(9898);
        }

        [Test]
        public void should_return_enable_ssl_as_false_by_default()
        {
            Subject.EnableSsl.Should().BeFalse();
        }

        [Test]
        public void should_return_default_bind_address()
        {
            Subject.BindAddress.Should().Be("*");
        }

        [Test]
        public void should_return_api_key_from_config()
        {
            Subject.ApiKey.Should().Be("testApiKey123456789012345678");
        }

        [Test]
        public void should_return_branch_from_config()
        {
            Subject.Branch.Should().Be("main");
        }

        [Test]
        public void should_return_log_level_from_config()
        {
            Subject.LogLevel.Should().Be("debug");
        }

        [Test]
        public void should_return_default_launch_browser()
        {
            Subject.LaunchBrowser.Should().BeTrue();
        }

        [Test]
        public void get_config_dictionary_should_return_all_properties()
        {
            var dict = Subject.GetConfigDictionary();

            dict.Should().NotBeEmpty();
            dict.Keys.Should().Contain("Port");
            dict.Keys.Should().Contain("ApiKey");
            dict.Keys.Should().Contain("Branch");
        }

        [Test]
        public void save_config_dictionary_should_skip_api_key()
        {
            var dict = new Dictionary<string, object>
            {
                { "ApiKey", "newApiKey" },
                { "Port", "9999" }
            };

            Subject.SaveConfigDictionary(dict);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.WriteAllText(It.IsAny<string>(), It.Is<string>(s => s.Contains("9999"))), Times.Once());
        }

        [Test]
        public void save_config_dictionary_should_publish_event_when_values_change()
        {
            var dict = new Dictionary<string, object>
            {
                { "Port", "7777" }
            };

            Subject.SaveConfigDictionary(dict);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<ConfigFileSavedEvent>()), Times.Once());
        }

        [Test]
        public void save_config_dictionary_should_not_publish_event_when_values_unchanged()
        {
            var dict = new Dictionary<string, object>
            {
                { "Port", "8989" }
            };

            Subject.SaveConfigDictionary(dict);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<ConfigFileSavedEvent>()), Times.Never());
        }

        [Test]
        public void url_base_should_return_empty_when_not_set()
        {
            Subject.UrlBase.Should().BeEmpty();
        }

        [Test]
        public void url_base_should_trim_slashes_and_prepend()
        {
            var configXml = "<Config><UrlBase>mybase</UrlBase></Config>";

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.ReadAllText(It.IsAny<string>()))
                  .Returns(configXml);

            // Need to create a new subject to clear cache
            var subject = Mocker.Resolve<ConfigFileProvider>();
            subject.UrlBase.Should().Be("/mybase");
        }

        [Test]
        public void should_create_new_config_when_file_does_not_exist()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);

            var subject = Mocker.Resolve<ConfigFileProvider>();
            subject.Port.Should().Be(8989);
        }

        [Test]
        public void should_return_default_authentication_method()
        {
            Subject.AuthenticationMethod.Should().Be(AuthenticationType.None);
        }

        [Test]
        public void should_return_analytics_enabled_by_default()
        {
            Subject.AnalyticsEnabled.Should().BeTrue();
        }
    }
}
