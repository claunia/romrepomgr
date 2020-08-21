using Microsoft.EntityFrameworkCore.Design;

namespace RomRepoMgr.Database
{
    public class ContextFactory : IDesignTimeDbContextFactory<Context>
    {
        public Context CreateDbContext(string[] args) => Context.Create("romrepo.db");
    }
}