using Octokit;
using System;
using System.Threading.Tasks;

namespace Azi.Cloud.DokanNet.Gui
{
    public class UpdateChecker
    {
        public enum UpdateType
        {
            None,
            Patch,
            Major,
            Minor
        }

        public class UpdateInfo
        {
            public UpdateType UpdateType { get; }

            public string Description { get; }
        }

        private long repositoryId;
        private string githubApiAccessId;

        public UpdateChecker(long repositoryId, string githubApiAccessId)
        {
            this.repositoryId = repositoryId;
            this.githubApiAccessId = githubApiAccessId;
        }

        public UpdateChecker(long repositoryId)
            : this(repositoryId, "ForRepositoryId" + repositoryId)
        {
        }

        public async Task<UpdateInfo> CheckUpdate()
        {
            var github = new GitHubClient(new ProductHeaderValue(githubApiAccessId));

            var releases = await github.Repository.Release.GetAll(repositoryId);

            throw new NotImplementedException();
        }
    }
}