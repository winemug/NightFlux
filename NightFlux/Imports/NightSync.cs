using System.Threading.Tasks;
using NightFlux.Data;

namespace NightFlux.Imports
{
    public static class NightSync
    {
        public static async Task Run(Configuration configuration)
        {
            var ns = await NightSql.GetInstance(configuration);
            await NightscoutImporter.Run(configuration);
            await TsvImporter.Run(configuration);
            await ns.Flush();
        }
    }
}
