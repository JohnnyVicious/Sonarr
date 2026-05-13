using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.OrganizerTests
{
    [TestFixture]
    public class FileNameValidationFixture : CoreTest
    {
        private TestStandardValidator _standardValidator;
        private TestDailyValidator _dailyValidator;
        private TestAnimeValidator _animeValidator;
        private TestSeriesFolderValidator _seriesFolderValidator;
        private TestSeasonFolderValidator _seasonFolderValidator;
        private TestColonValidator _colonValidator;

        [SetUp]
        public void Setup()
        {
            _standardValidator = new TestStandardValidator();
            _dailyValidator = new TestDailyValidator();
            _animeValidator = new TestAnimeValidator();
            _seriesFolderValidator = new TestSeriesFolderValidator();
            _seasonFolderValidator = new TestSeasonFolderValidator();
            _colonValidator = new TestColonValidator();
        }

        // Standard episode format tests

        [Test]
        public void valid_standard_format_with_season_episode_pattern()
        {
            var model = new TestModel { Value = "{Series Title} - S{season:00}E{episode:00} - {Episode Title}" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_standard_format_with_separate_season_and_episode()
        {
            var model = new TestModel { Value = "{Series Title} - {season:00}x{episode:00}" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_standard_format_with_original_title()
        {
            var model = new TestModel { Value = "{Original Title}" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_standard_format_with_original_filename()
        {
            var model = new TestModel { Value = "{Original Filename}" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void invalid_standard_format_without_episode_info()
        {
            var model = new TestModel { Value = "{Series Title} - {Episode Title}" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeFalse();
        }

        [Test]
        public void invalid_standard_format_with_empty_value()
        {
            var model = new TestModel { Value = "" };
            var result = _standardValidator.Validate(model);
            result.IsValid.Should().BeFalse();
        }

        // Daily episode format tests

        [Test]
        public void valid_daily_format_with_air_date()
        {
            var model = new TestModel { Value = "{Series Title} - {Air Date}" };
            var result = _dailyValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_daily_format_with_season_episode()
        {
            var model = new TestModel { Value = "{Series Title} - S{season:00}E{episode:00}" };
            var result = _dailyValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void invalid_daily_format_without_date_or_episode()
        {
            var model = new TestModel { Value = "{Series Title} - {Episode Title}" };
            var result = _dailyValidator.Validate(model);
            result.IsValid.Should().BeFalse();
        }

        // Anime episode format tests

        [Test]
        public void valid_anime_format_with_absolute_episode()
        {
            var model = new TestModel { Value = "{Series Title} - {absolute:000}" };
            var result = _animeValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_anime_format_with_season_episode()
        {
            var model = new TestModel { Value = "{Series Title} - S{season:00}E{episode:00}" };
            var result = _animeValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void invalid_anime_format_without_episode_info()
        {
            var model = new TestModel { Value = "{Series Title} - {Episode Title}" };
            var result = _animeValidator.Validate(model);
            result.IsValid.Should().BeFalse();
        }

        // Series folder format tests

        [Test]
        public void valid_series_folder_with_series_title()
        {
            var model = new TestModel { Value = "{Series Title}" };
            var result = _seriesFolderValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_series_folder_with_clean_series_title()
        {
            var model = new TestModel { Value = "{Series CleanTitle}" };
            var result = _seriesFolderValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        // Season folder format tests

        [Test]
        public void valid_season_folder_with_season_number()
        {
            var model = new TestModel { Value = "Season {season}" };
            var result = _seasonFolderValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void valid_season_folder_with_padded_season()
        {
            var model = new TestModel { Value = "Season {season:00}" };
            var result = _seasonFolderValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        // Colon replacement validator tests

        [Test]
        public void colon_validator_should_pass_for_valid_input()
        {
            var model = new TestModel { Value = "dash replacement" };
            var result = _colonValidator.Validate(model);
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void colon_validator_should_fail_for_colon()
        {
            var model = new TestModel { Value = "test:replacement" };
            var result = _colonValidator.Validate(model);
            result.IsValid.Should().BeFalse();
        }

        private class TestModel
        {
            public string Value { get; set; }
        }

        private class TestStandardValidator : AbstractValidator<TestModel>
        {
            public TestStandardValidator()
            {
                RuleFor(m => m.Value).ValidEpisodeFormat();
            }
        }

        private class TestDailyValidator : AbstractValidator<TestModel>
        {
            public TestDailyValidator()
            {
                RuleFor(m => m.Value).ValidDailyEpisodeFormat();
            }
        }

        private class TestAnimeValidator : AbstractValidator<TestModel>
        {
            public TestAnimeValidator()
            {
                RuleFor(m => m.Value).ValidAnimeEpisodeFormat();
            }
        }

        private class TestSeriesFolderValidator : AbstractValidator<TestModel>
        {
            public TestSeriesFolderValidator()
            {
                RuleFor(m => m.Value).ValidSeriesFolderFormat();
            }
        }

        private class TestSeasonFolderValidator : AbstractValidator<TestModel>
        {
            public TestSeasonFolderValidator()
            {
                RuleFor(m => m.Value).ValidSeasonFolderFormat();
            }
        }

        private class TestColonValidator : AbstractValidator<TestModel>
        {
            public TestColonValidator()
            {
                RuleFor(m => m.Value).ValidCustomColonReplacement();
            }
        }
    }
}
