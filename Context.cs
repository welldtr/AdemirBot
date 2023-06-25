using DiscordBot.Entities;
using DiscordBot.Repository;
using DiscordBot.Utils;
using MongoDB.Driver;

namespace DiscordBot
{
    public class Context
    {
        private readonly IMongoDatabase db;
        public Context(IMongoDatabase db)
        {
            this.db = db;
        }

        public IRepository<Membership> memberships { get => db.GetRepository<Membership>("memberships"); }
        public IRepository<BumpConfig> bumpCfg { get => db.GetRepository<BumpConfig>("bump_config"); }
        public IRepository<AdemirConfig> ademirCfg { get => db.GetRepository<AdemirConfig>("ademir_cfg"); }
        public IRepository<DenunciaConfig> denunciaCfg { get => db.GetRepository<DenunciaConfig>("denuncia_config"); }
        public IRepository<Message> messagelog { get => db.GetRepository<Message>("messages"); }
        public IRepository<Denuncia> denuncias { get => db.GetRepository<Denuncia>("denuncias"); }
        public IRepository<Bump> bumps { get => db.GetRepository<Bump>("bumps"); }
        public IRepository<Macro> macros { get => db.GetRepository<Macro>("macros"); }
        public IRepository<Track> tracks { get => db.GetRepository<Track>("tracks"); }
    }
}
