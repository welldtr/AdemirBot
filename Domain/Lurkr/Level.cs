namespace DiscordBot.Domain.Lurkr
{
    public class MemberLevel
    {
        public string Avatar { get; set; }
        public long MessageCount { get; set; }
        public string Tag { get; set; }
        public string UserId { get; set; }
        public long XP { get; set; }
        public int Level { get; set; }
        public string AccentColour { get; set; }
    }

}
