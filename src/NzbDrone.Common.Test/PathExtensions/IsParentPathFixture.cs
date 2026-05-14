using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Test.Common;

namespace NzbDrone.Common.Test.PathExtensions
{
    [TestFixture]
    public class IsParentPathFixture : TestBase
    {
        [TestCase(@"C:\parent", @"C:\parent\child", true)]
        [TestCase(@"C:\parent", @"C:\parent\child\grandchild", true)]
        [TestCase(@"C:\parent", @"C:\other\child", false)]
        [TestCase(@"C:\parent", @"C:\parent", false)]
        [TestCase(@"C:\parent\", @"C:\parent\child", true)]
        [TestCase(@"C:\parent", @"C:\parent\child\", true)]
        [TestCase(@"C:\parent\", @"C:\parent\child\", true)]
        [TestCase(@"C:\", @"C:\child", true)]
        [TestCase(@"C:\parent", @"C:\parentmore\child", false)]
        [TestCase(@"C:\Test", @"C:\Test\TV\Show", true)]
        public void should_detect_parent_path_windows(string parentPath, string childPath, bool expected)
        {
            // nosemgrep: codacy.csharp.security.null-dereference
            parentPath.AsOsAgnostic()
                .IsParentPath(childPath.AsOsAgnostic()) // nosemgrep: codacy.csharp.security.null-dereference
                .Should().Be(expected);
        }

        [TestCase("/parent", "/parent/child", true)]
        [TestCase("/parent", "/parent/child/grandchild", true)]
        [TestCase("/parent", "/other/child", false)]
        [TestCase("/parent", "/parent", false)]
        [TestCase("/parent/", "/parent/child", true)]
        [TestCase("/parent", "/parent/child/", true)]
        [TestCase("/", "/child", true)]
        [TestCase("/parent", "/parentmore/child", false)]
        [TestCase("/test", "/test/tv/show", true)]
        public void should_detect_parent_path_posix(string parentPath, string childPath, bool expected)
        {
            PosixOnly();

            // nosemgrep: codacy.csharp.security.null-dereference
            parentPath.IsParentPath(childPath).Should().Be(expected);
        }

        [Test]
        public void same_path_should_not_be_parent()
        {
            var path = @"C:\Test".AsOsAgnostic();
            path.IsParentPath(path).Should().BeFalse();
        }

        [Test]
        public void same_path_with_trailing_slash_should_not_be_parent()
        {
            var parent = @"C:\Test\".AsOsAgnostic();
            var child = @"C:\Test".AsOsAgnostic();

            parent.IsParentPath(child).Should().BeFalse();
        }

        [Test]
        public void deeply_nested_child_should_be_detected()
        {
            var parent = @"C:\root".AsOsAgnostic();
            var child = @"C:\root\a\b\c\d\e\f".AsOsAgnostic();

            parent.IsParentPath(child).Should().BeTrue();
        }

        [Test]
        public void partial_name_match_should_not_be_parent()
        {
            var parent = @"C:\Test".AsOsAgnostic();
            var child = @"C:\TestExtra\child".AsOsAgnostic();

            parent.IsParentPath(child).Should().BeFalse();
        }

        [Test]
        public void child_path_should_not_be_parent_of_parent()
        {
            var parent = @"C:\Test\child".AsOsAgnostic();
            var child = @"C:\Test".AsOsAgnostic();

            parent.IsParentPath(child).Should().BeFalse();
        }

        [Test]
        public void windows_case_insensitive_parent_check()
        {
            WindowsOnly();

            @"c:\test".IsParentPath(@"C:\Test\child").Should().BeTrue();
        }

        [Test]
        public void root_should_be_parent_of_any_child()
        {
            var root = @"C:\".AsOsAgnostic();
            var child = @"C:\anything\here".AsOsAgnostic();

            root.IsParentPath(child).Should().BeTrue();
        }
    }
}
