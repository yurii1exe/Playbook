namespace Web.Domain.Entities
{
    public class TeamBase : BaseEntity
    {
        public string Name { get; set; }

        public TeamBase GetInstance()
        {
            return new TeamBase { Id = this.Id, Name = this.Name };
        }
    }
}
