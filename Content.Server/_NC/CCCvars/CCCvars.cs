using Robust.Shared.Configuration;

namespace Content.Server._NC.CCCvars;

[CVarDefs]
public static class CCCVars
{
    /*
     *  Discord OAuth2
     */

    public static readonly CVarDef<string> DiscordApiUrl =
        CVarDef.Create("jerry.discord_api_url", "", CVar.CONFIDENTIAL | CVar.SERVERONLY);
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("jerry.discord_auth_enabled", false, CVar.CONFIDENTIAL | CVar.SERVERONLY);

    /*
     * Sponsors
     */

    public static readonly CVarDef<string> DiscordGuildID =
        CVarDef.Create("jerry.discord_guildId", "1471565853534322715", CVar.CONFIDENTIAL | CVar.SERVERONLY);

    public static readonly CVarDef<string> ApiKey =
        CVarDef.Create("jerry.discord_apikey", "", CVar.CONFIDENTIAL | CVar.SERVERONLY);
}
