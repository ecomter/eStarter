namespace eStarter.Models
{
    public class AppEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IconPath { get; set; }
        public string Background { get; set; } = "#FF2D9CDB";
        public int BadgeCount { get; set; }
    }
}
