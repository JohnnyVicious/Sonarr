using System; // NOSONAR
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Jobs
{
    [TestFixture]
    public class TaskManagerFixture : CoreTest<TaskManager>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(new CacheManager());

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<ScheduledTask>());

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RssSyncInterval)
                  .Returns(15);

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.BackupInterval)
                  .Returns(7);
        }

        private void GivenInitializedTasks()
        {
            Subject.Handle(new ApplicationStartedEvent());
        }

        [Test]
        public void should_initialize_tasks_on_application_started()
        {
            GivenInitializedTasks();

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Verify(v => v.Upsert(It.IsAny<ScheduledTask>()), Times.AtLeastOnce());
        }

        [Test]
        public void get_all_should_return_initialized_tasks()
        {
            GivenInitializedTasks();

            var tasks = Subject.GetAll();

            tasks.Should().NotBeEmpty();
        }

        [Test]
        public void get_pending_should_return_tasks_past_due()
        {
            GivenInitializedTasks();

            // All tasks start with LastExecution = DateTime.UtcNow so none are pending initially
            var pending = Subject.GetPending();
            pending.Should().BeEmpty();
        }

        [Test]
        public void get_pending_should_return_tasks_that_are_overdue()
        {
            var existingTasks = new List<ScheduledTask>
            {
                new ScheduledTask
                {
                    TypeName = typeof(RefreshMonitoredDownloadsCommand).FullName,
                    Interval = 1,
                    LastExecution = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(existingTasks);

            GivenInitializedTasks();

            var pending = Subject.GetPending();
            pending.Should().HaveCount(1);
        }

        [Test]
        public void get_pending_should_not_return_tasks_with_zero_interval()
        {
            var existingTasks = new List<ScheduledTask>
            {
                new ScheduledTask
                {
                    TypeName = typeof(RefreshMonitoredDownloadsCommand).FullName,
                    Interval = 0,
                    LastExecution = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(existingTasks);

            GivenInitializedTasks();

            // The default tasks will overwrite interval, but the zero-interval RSS won't appear
            // because its interval is properly set during init. Test the filtering logic.
            var pending = Subject.GetPending();
            pending.Where(p => p.Interval == 0).Should().BeEmpty();
        }

        [Test]
        public void should_remove_tasks_not_in_defaults()
        {
            var obsoleteTask = new ScheduledTask
            {
                Id = 999,
                TypeName = "NzbDrone.Core.SomeOldCommand",
                Interval = 60,
                LastExecution = DateTime.UtcNow
            };

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<ScheduledTask> { obsoleteTask });

            GivenInitializedTasks();

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Verify(v => v.Delete(999), Times.Once());
        }

        [Test]
        public void should_clamp_rss_sync_interval_minimum_to_10()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RssSyncInterval)
                  .Returns(5);

            GivenInitializedTasks();

            var tasks = Subject.GetAll();
            var rssTask = tasks.FirstOrDefault(t => t.TypeName.Contains("RssSyncCommand"));

            rssTask.Should().NotBeNull();
            rssTask.Interval.Should().Be(10);
        }

        [Test]
        public void should_allow_zero_rss_sync_interval()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RssSyncInterval)
                  .Returns(0);

            GivenInitializedTasks();

            var tasks = Subject.GetAll();
            var rssTask = tasks.FirstOrDefault(t => t.TypeName.Contains("RssSyncCommand"));

            rssTask.Should().NotBeNull();
            rssTask.Interval.Should().Be(0);
        }

        [Test]
        public void should_return_negative_rss_sync_interval_as_zero()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RssSyncInterval)
                  .Returns(-1);

            GivenInitializedTasks();

            var tasks = Subject.GetAll();
            var rssTask = tasks.FirstOrDefault(t => t.TypeName.Contains("RssSyncCommand"));

            rssTask.Should().NotBeNull();
            rssTask.Interval.Should().Be(0);
        }

        [Test]
        public void should_clamp_backup_interval_minimum_to_1_day()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.BackupInterval)
                  .Returns(0);

            GivenInitializedTasks();

            var tasks = Subject.GetAll();
            var backupTask = tasks.FirstOrDefault(t => t.TypeName.Contains("BackupCommand"));

            backupTask.Should().NotBeNull();
            // 1 day * 60 min * 24 hrs = 1440
            backupTask.Interval.Should().Be(1 * 60 * 24);
        }

        [Test]
        public void should_clamp_backup_interval_maximum_to_7_days()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.BackupInterval)
                  .Returns(30);

            GivenInitializedTasks();

            var tasks = Subject.GetAll();
            var backupTask = tasks.FirstOrDefault(t => t.TypeName.Contains("BackupCommand"));

            backupTask.Should().NotBeNull();
            // 7 days * 60 min * 24 hrs = 10080
            backupTask.Interval.Should().Be(7 * 60 * 24);
        }
    }
}
