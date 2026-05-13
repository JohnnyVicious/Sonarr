using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Profiles.Quality;

namespace NzbDrone.Api.Test.V5
{
    [TestFixture]
    public class QualityProfileControllerFixture : TestBase<QualityProfileController>
    {
        private QualityProfile _profile;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile
            {
                Id = 1,
                Name = "HD - 720p/1080p",
                Cutoff = NzbDrone.Core.Qualities.Quality.HDTV720p.Id,
                UpgradeAllowed = true,
                MinUpgradeFormatScore = 1,
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem
                    {
                        Quality = NzbDrone.Core.Qualities.Quality.HDTV720p,
                        Allowed = true
                    }
                },
                FormatItems = new List<ProfileFormatItem>()
            };

            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat>());

            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("/api/v5/qualityprofile/1");

            Subject.Url = urlHelper.Object;
            Subject.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Test]
        public void should_get_all_profiles()
        {
            var profiles = new List<QualityProfile> { _profile };

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.All())
                .Returns(profiles);

            var result = Subject.GetAll();

            result.Value.Should().HaveCount(1);
            result.Value[0].Name.Should().Be("HD - 720p/1080p");
        }

        [Test]
        public void should_get_profile_by_id()
        {
            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.Get(1))
                .Returns(_profile);

            var result = Subject.GetResourceByIdWithErrorHandler(1);

            var okResult = result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<QualityProfileResource>>().Subject;
            okResult.Value.Id.Should().Be(1);
            okResult.Value.Name.Should().Be("HD - 720p/1080p");
            okResult.Value.UpgradeAllowed.Should().BeTrue();
        }

        [Test]
        public void should_delete_profile()
        {
            Subject.DeleteProfile(1);

            Mocker.GetMock<IQualityProfileService>()
                .Verify(s => s.Delete(1), Times.Once());
        }

        [Test]
        public void should_add_profile()
        {
            var resource = new QualityProfileResource
            {
                Name = "New Profile",
                Cutoff = NzbDrone.Core.Qualities.Quality.HDTV720p.Id,
                MinUpgradeFormatScore = 1,
                Items = new List<QualityProfileQualityItemResource>
                {
                    new QualityProfileQualityItemResource
                    {
                        Quality = NzbDrone.Core.Qualities.Quality.HDTV720p,
                        Allowed = true
                    }
                },
                FormatItems = new List<ProfileFormatItemResource>()
            };

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.Add(It.IsAny<QualityProfile>()))
                .Returns(new QualityProfile
                {
                    Id = 5,
                    Name = "New Profile",
                    Cutoff = NzbDrone.Core.Qualities.Quality.HDTV720p.Id,
                    Items = new List<QualityProfileQualityItem>(),
                    FormatItems = new List<ProfileFormatItem>()
                });

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.Get(5))
                .Returns(new QualityProfile
                {
                    Id = 5,
                    Name = "New Profile",
                    Cutoff = NzbDrone.Core.Qualities.Quality.HDTV720p.Id,
                    Items = new List<QualityProfileQualityItem>(),
                    FormatItems = new List<ProfileFormatItem>()
                });

            Subject.Create(resource);

            Mocker.GetMock<IQualityProfileService>()
                .Verify(s => s.Add(It.Is<QualityProfile>(p => p.Name == "New Profile")), Times.Once());
        }

        [Test]
        public void should_update_profile()
        {
            var resource = new QualityProfileResource
            {
                Id = 1,
                Name = "Updated Profile",
                Cutoff = NzbDrone.Core.Qualities.Quality.HDTV1080p.Id,
                MinUpgradeFormatScore = 1,
                Items = new List<QualityProfileQualityItemResource>(),
                FormatItems = new List<ProfileFormatItemResource>()
            };

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.Get(1))
                .Returns(new QualityProfile
                {
                    Id = 1,
                    Name = "Updated Profile",
                    Cutoff = NzbDrone.Core.Qualities.Quality.HDTV1080p.Id,
                    Items = new List<QualityProfileQualityItem>(),
                    FormatItems = new List<ProfileFormatItem>()
                });

            Subject.Update(resource);

            Mocker.GetMock<IQualityProfileService>()
                .Verify(s => s.Update(It.Is<QualityProfile>(p => p.Name == "Updated Profile")), Times.Once());
        }

        [Test]
        public void should_return_empty_list_when_no_profiles()
        {
            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.All())
                .Returns(new List<QualityProfile>());

            var result = Subject.GetAll();

            result.Value.Should().BeEmpty();
        }

        [Test]
        public void should_map_upgrade_allowed_flag()
        {
            var profile = new QualityProfile
            {
                Id = 2,
                Name = "No Upgrade",
                UpgradeAllowed = false,
                Cutoff = NzbDrone.Core.Qualities.Quality.SDTV.Id,
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem>()
            };

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.All())
                .Returns(new List<QualityProfile> { profile });

            var result = Subject.GetAll();

            result.Value[0].UpgradeAllowed.Should().BeFalse();
        }
    }
}
