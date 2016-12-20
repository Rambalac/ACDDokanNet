namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Octokit;

    public class UpdateChecker
    {
        private static readonly Regex VersionExp = new Regex("^\\D*(\\d.*$)");
        private readonly long repositoryId;

        private readonly string githubApiAccessId;

        public UpdateChecker(long repositoryId, string githubApiAccessId)
        {
            this.repositoryId = repositoryId;
            this.githubApiAccessId = githubApiAccessId;
        }

        public UpdateChecker(long repositoryId)
            : this(repositoryId, "ForRepositoryId" + repositoryId)
        {
        }

        public enum UpdateType
        {
            Build,
            Patch,
            Major,
            Minor
        }

        public async Task<UpdateInfo> CheckUpdate(UpdateType type = UpdateType.Patch, Version currentVersion = null)
        {
            var github = new GitHubClient(new ProductHeaderValue(githubApiAccessId));

            var releases = await github.Repository.Release.GetAll(repositoryId);

            if (currentVersion == null)
            {
                currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            }

            var curver = new Semver.SemVersion(currentVersion.Major, currentVersion.Minor, currentVersion.Build, string.Empty, currentVersion.Revision.ToString());
            var result = releases.FirstOrDefault(r =>
              {
                  var tagMatch = VersionExp.Match(r.TagName);
                  if (!tagMatch.Success)
                  {
                      return false;
                  }

                  Semver.SemVersion relver;
                  var parsed = Semver.SemVersion.TryParse(tagMatch.Groups[1].Value, out relver);
                  if (!parsed)
                  {
                      return false;
                  }

                  switch (type)
                  {
                      case UpdateType.Major:
                          relver = new Semver.SemVersion(relver.Major);
                          break;
                      case UpdateType.Minor:
                          relver = new Semver.SemVersion(relver.Major, relver.Minor);
                          break;
                      case UpdateType.Patch:
                          relver = new Semver.SemVersion(relver.Major, relver.Minor, relver.Patch);
                          break;
                  }

                  return relver > curver;
              });
            if (result == null)
            {
                return null;
            }

            return new UpdateInfo
            {
                Description = result.Body,
                Name = result.Name,
                Version = result.TagName,
                Assets = result.Assets.Select(a => new UpdateAssetsInfo
                {
                    Name = a.Name,
                    Url = a.Url,
                    BrowserUrl = a.BrowserDownloadUrl
                }).ToList()
            };
        }

        public class UpdateAssetsInfo
        {
            public string BrowserUrl { get; internal set; }

            public string Name { get; internal set; }

            public string Url { get; internal set; }
        }

        public class UpdateInfo
        {
            public List<UpdateAssetsInfo> Assets { get; internal set; }

            public string Description { get; internal set; }

            public string Name { get; internal set; }

            public string Version { get; internal set; }
        }
    }
}