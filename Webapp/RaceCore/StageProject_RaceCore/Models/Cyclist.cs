namespace StageProject_RaceCore.Models
{
    public class Cyclist
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? TeamId { get; set; }
        public Team Team { get; set; }
        public bool IsActive { get; set; }
    }
}
