using System.Threading.Tasks;

namespace OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
{
    public class AzureTableMigration
    {
        public string Name { get; private set; }

        public AzureTableMigration(string name)
        {
            Name = name;
        }

        public virtual Task Execute(AzureTableContext context) => Task.CompletedTask;
    }
}