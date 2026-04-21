namespace StageProject_RaceCore.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public List<Cyclist> Cyclists { get; set; }
    }
}
