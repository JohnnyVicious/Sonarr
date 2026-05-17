#!/usr/bin/env bash
set -euo pipefail

fail() {
  echo "::error::$*"
  exit 1
}

gitlinks="$(git ls-tree -r --full-tree HEAD | awk '$1 == "160000" { print $4 }')"
if [[ -n "$gitlinks" ]]; then
  fail "Unexpected gitlink/submodule entries found: ${gitlinks//$'\n'/, }"
fi

tracked_local_artifacts="$(git ls-tree -r --name-only HEAD | grep -E '^(\.claude/|Sonarr-port-[^/]+($|/))' || true)"
if [[ -n "$tracked_local_artifacts" ]]; then
  fail "Local worktree artifacts are tracked: ${tracked_local_artifacts//$'\n'/, }"
fi

grep -qxF ".claude/" .gitignore || fail ".gitignore must ignore .claude/"
grep -qxF "Sonarr-port-*/" .gitignore || fail ".gitignore must ignore Sonarr-port-*/"

http_test_projects="$(grep -cF 'Sonarr.Http.Test\Sonarr.Http.Test.csproj' src/Sonarr.sln || true)"
if [[ "$http_test_projects" -ne 1 ]]; then
  fail "src/Sonarr.sln must contain exactly one Sonarr.Http.Test project entry; found $http_test_projects"
fi

if grep -qF "NzbDrone.Http.Test" src/Sonarr.sln; then
  fail "src/Sonarr.sln still references removed NzbDrone.Http.Test project"
fi

if [[ -e src/NzbDrone.Http.Test ]]; then
  fail "Removed src/NzbDrone.Http.Test directory is present again"
fi

invalid_path_regex_fields="$(grep -cF "private static readonly Regex InvalidPathRegex" src/Sonarr.Http/Frontend/StaticResourceController.cs || true)"
if [[ "$invalid_path_regex_fields" -ne 1 ]]; then
  fail "StaticResourceController must declare InvalidPathRegex exactly once; found $invalid_path_regex_fields"
fi

grep -qF '[SuppressMessage("Sonar", "S3329"' src/NzbDrone.Core/Notifications/Pushover/PushoverProxy.cs ||
  fail "Pushover S3329 suppression must use the Sonar category"

grep -qF '[SuppressMessage("Sonar", "SCS0013"' src/NzbDrone.Core/Notifications/Pushover/PushoverProxy.cs ||
  fail "Pushover SCS0013 suppression must use the Sonar category"

grep -qF "#pragma warning disable S3329" src/NzbDrone.Core/Notifications/Pushover/PushoverProxy.cs ||
  fail "Pushover S3329 warning must be disabled around the vendor-mandated CBC implementation"

grep -qF "#pragma warning restore S3329" src/NzbDrone.Core/Notifications/Pushover/PushoverProxy.cs ||
  fail "Pushover S3329 warning must be restored after the vendor-mandated CBC implementation"

grep -qF '[SuppressMessage("SonarAnalyzer", "S3994"' src/Sonarr.Http/Frontend/Mappers/StaticResourceMapperBase.cs ||
  fail "StaticResourceMapperBase S3994 suppression must use the SonarAnalyzer category"

grep -qF "private sealed class TestMapper" src/Sonarr.Http.Test/Frontend/Mappers/StaticResourceMapperBaseFixture.cs ||
  fail "StaticResourceMapperBaseFixture test mock must remain sealed"

grep -qF "#pragma warning disable S1172" src/Sonarr.Http.Test/Frontend/Mappers/StaticResourceMapperBaseFixture.cs ||
  fail "StaticResourceMapperBaseFixture must suppress S1172 for signature-required parameters"

grep -qF "#pragma warning restore S1172" src/Sonarr.Http.Test/Frontend/Mappers/StaticResourceMapperBaseFixture.cs ||
  fail "StaticResourceMapperBaseFixture must restore S1172 after signature-required parameters"

grep -qF "protected override string MapPath(string resourceUrl) => MapPathResult;" src/Sonarr.Http.Test/Frontend/Mappers/StaticResourceMapperBaseFixture.cs ||
  fail "StaticResourceMapperBaseFixture MapPath test override must keep a StyleCop-compliant parameter name"

grep -qF "public override bool CanHandle(string resourceUrl) => true;" src/Sonarr.Http.Test/Frontend/Mappers/StaticResourceMapperBaseFixture.cs ||
  fail "StaticResourceMapperBaseFixture CanHandle test override must keep a StyleCop-compliant parameter name"
