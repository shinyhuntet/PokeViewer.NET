using Newtonsoft.Json;
using PokeViewer.NET.Properties;
using System.Text;
using PKHeX.Core;
using PokeViewer.NET.SubForms;
using static PokeViewer.NET.ViewerUtil;
using static RaidCrawler.Core.Structures.Utils;
using System.Globalization;

namespace PokeViewer.NET.Util;

public static class WebHookUtil
{
    private static HttpClient? _client;
    private static HttpClient Client
    {
        get
        {
            _client ??= new HttpClient();
            return _client;
        }
    }

    private static string[]? DiscordWebhooks;

    public static async void SendNotifications(string results, string thumbnail, bool pinguser)
    {
        if (string.IsNullOrEmpty(results) || string.IsNullOrEmpty(Settings.Default.WebHook))
            return;
        DiscordWebhooks = Settings.Default.WebHook.Split(',');
        if (DiscordWebhooks == null)
            return;
        var webhook = GenerateWebhook(results, thumbnail, pinguser);
        var content = new StringContent(JsonConvert.SerializeObject(webhook), Encoding.UTF8, "application/json");
        foreach (var url in DiscordWebhooks)
            await Client.PostAsync(url, content).ConfigureAwait(false);
    }

    private static object GenerateWebhook(string results, string thumbnail, bool pinguser)
    {
        string userContent = pinguser ? $"<@{Settings.Default.UserDiscordID}>\n{Settings.Default.PingMessage}" : "";
        string title = pinguser ? $"Match Found!" : "Unwanted match..";
        var WebHook = new
        {
            username = $"{(!string.IsNullOrEmpty(Settings.Default.DiscordUserName) ? Settings.Default.DiscordUserName : "PokeViewer.NET")}",
            content = userContent,
            embeds = new List<object>
                {
                    new
                    { 
                        title,
                        thumbnail = new
                        {
                            url = thumbnail
                        },
                        fields = new List<object>
                        {
                            new { name = "Description               ", value = results, inline = true, },
                        },
                    }
                }
        };
        return WebHook;
    }

    public static async void SendDetailNotifications(PKM pk, string thumbnail, bool pinguser, SimpleTrainerInfo trainerInfo, bool isStaic = false, bool isGift = false, int Encounter = 0, double Rate = 0)
    {
        if (pk == null || !pk.Valid || pk.Species <= 0 || pk.Species > (ushort)Species.MAX_COUNT || string.IsNullOrEmpty(Settings.Default.WebHook))
            return;
        DiscordWebhooks = Settings.Default.WebHook.Split(',');
        if (DiscordWebhooks == null)
            return;
        try
        {
            var webhook = GenerateWebHookDetail(pk, thumbnail, pinguser, trainerInfo, isStaic, isGift, Encounter, Rate);
            var content = new StringContent(JsonConvert.SerializeObject(webhook), Encoding.UTF8, "application/json");
            foreach (var url in DiscordWebhooks)
                await Client.PostAsync(url, content).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            // Log the exception or handle it as needed
            MessageBox.Show($"Error sending webhook: {ex.Message}");
        }
    }
    
    private static object GenerateWebHookDetail(PKM pk, string thumbnail, bool pinguser, SimpleTrainerInfo trainerInfo, bool isStaic = false, bool isGift = false, int Encounter = 0, double Rate = 0)
    {
        GameStrings Strings = GameInfo.GetStrings("en");
        string userContent = pinguser ? $"<@{Settings.Default.UserDiscordID}>\n{Settings.Default.PingMessage}" : "";
        string title = pinguser ? $"Match Found!" : isStaic || isGift ? $"{(isStaic ? "Static" : "Mystery Gift")} Pokemon Found!{Environment.NewLine}Encounter #{Encounter}{(Rate <= 0 ? "" : $"{Environment.NewLine}Target Rate: {Rate:0.00}%")}" : "Unwanted match..";
        List<string> EmojiMark = MarkEmoji(pk, out List<string> MarkString);
        bool hasMark = MarkString.Count > 0;
        List<string> EmojiRibbon = RibbonEmoji(pk, out List<string> RibbonString);
        bool hasRibbon = RibbonString.Count > 0;
        Span<int> _ivs = stackalloc int[6];
        pk.GetIVs(_ivs);
        var ivs = IVsStringEmoji(ToSpeedLast(_ivs));
        Span<int> _evs = stackalloc int[6];
        pk.GetEVs(_evs);
        var evs = IVsStringEmoji(ToSpeedLast(_evs));
        var movestr = GetMovesString(pk, Strings);
        var teratype = TeraEmoji(pk, Strings);
        var gmax = GetGigantamaxEmoji(pk, Strings);
        var hasItem = pk.HeldItem != 0;
        var trainerID = GetTrainerID32(trainerInfo.TID16, trainerInfo.SID16);
        var timezone = string.IsNullOrEmpty(Settings.Default.DiscordTimeZone) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(Settings.Default.DiscordTimeZone);
        var Date = DateTimeOffset.UtcNow;        
        var DisplayTime = TimeZoneInfo.ConvertTime(Date.DateTime, timezone);
        var WebHook = new
        {
            username = $"{(!string.IsNullOrEmpty(Settings.Default.DiscordUserName) ? Settings.Default.DiscordUserName : "PokeViewer.NET")}",
            content = userContent,
            embeds = new List<object>
                {
                    new
                    {
                        title,
                        description = "**Pokemon Info**",
                        color = SetColor(pk),
                        author = new
                        {
                            name  = (Ball)pk.Ball != Ball.None ? "Pokemon you've obtained" : "Wild Pokemon",
                            icon_url = (Ball)pk.Ball != Ball.None ? GetBallImg(pk) : "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/ribbonmarkalpha.png",
                        },
                        thumbnail = new
                        {
                            url = thumbnail
                        },
                        fields = new List<object>
                        {
                            new { name = "Species               ", value = $"{ShinyEmoji(pk.IsShiny, pk.ShinyXor == 0)}{(Species)pk.Species}{Egg_Viewer.FormOutput(Strings, pk.Species, pk.Form, out _)} {GenderEmoji(pk.Gender)}", inline = true, },
                            new { name = $"{(pk.IsEgg ? $"Egg Hatch Cycle {Emoji["Egg"]}               " : "")}", value = $"{(pk.IsEgg ? $"{pk.CurrentFriendship}" : "")}", inline = pk.IsEgg, },
                            new { name = $"{(!string.IsNullOrEmpty(teratype) ? "TeraType               " : "")}", value = $"{(!string.IsNullOrEmpty(teratype) ? $"{teratype}" : "")}", inline = !string.IsNullOrEmpty(teratype), },
                            new { name = $"{(!string.IsNullOrEmpty(gmax) ? "Gigantamax               " : "")}", value = $"{(!string.IsNullOrEmpty(gmax) ? $"{gmax}\n** **" : "")}", inline = !string.IsNullOrEmpty(gmax), },
                            new { name = "PID               ", value = $"{pk.PID:X}", inline = true, },
                            new { name = "EncryptionConstatnt               ", value = $"{pk.EncryptionConstant:X}", inline = true, },
                            new { name = "Nature               ", value = $"{pk.Nature}\n** **", inline = true, },
                            new { name = "Ability               ", value = $"{(Ability)pk.Ability}", inline = true, },
                            new { name = "Mark               ", value = $"{(hasMark ? $"{string.Join(Environment.NewLine, ConcatStringList(MarkString, EmojiMark))}" : "None")}", inline = true, },
                            new { name = $"{(hasRibbon ? "Ribbon               " : "")}", value = $"{(hasRibbon ? $"{string.Join(Environment.NewLine, ConcatStringList(RibbonString, EmojiRibbon))}\n** **" : "\n** **")}", inline = hasRibbon, },
                            new { name = "Scale               ", value = ScaleString(pk) , inline = true, },
                            new { name = "Level               ", value = $"{pk.CurrentLevel}\n** **", inline = true, },
                            new { name = "Moves               ", value = movestr, inline = true, },
                            new { name = "IVs               ", value = ivs + "\n** **", inline = true, },
                            new { name = "EVs               ", value = evs, inline = true, },

                        },
                    },
                    new
                    {
                        description = "**Miscellaneous Info**",
                        color = SetColor(pk),
                        author = new
                        {
                            name  = hasItem ? $"Held Item: {Strings.itemlist[pk.HeldItem]}" : "No Held Item",
                            icon_url = hasItem ? $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Artwork%20Items/aitem_{pk.HeldItem}.png" : "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/helditem.png",
                        },
                        timestamp = DisplayTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        footer = new
                        {
                            text = $"Trainer Info{Environment.NewLine}{(!string.IsNullOrEmpty(pk.OriginalTrainerName) ? $"OT:{pk.OriginalTrainerName} | TID:{pk.DisplayTID} | SID:{pk.DisplaySID}\nLanguage: {(LanguageID)pk.Language}\nTimeZone: {timezone.StandardName}" : $"OT:{trainerInfo.OT} | TID:{(pk.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? trainerID % 1000000 : trainerInfo.TID16)} | SID:{(pk.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? trainerID / 1000000 : trainerInfo.SID16)}\nLanguage: {(LanguageID)trainerInfo.Language}\nTimeZone: {timezone.StandardName}")}",
                            icon_url = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/ball/_ball16.png"
                        },
                    }
                }
        };
        return WebHook;

    }
    private static uint GetTrainerID32(ushort tid16, ushort sid16)
    {
        return (uint)((sid16 << 16) | tid16);
    }

    private static int SetColor(PKM pkm)
    {
        Color color = pkm.IsShiny && pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.LightGray : Color.Teal;
        return int.Parse($"{color.R:X2}{color.G:X2}{color.B:X2}", NumberStyles.HexNumber);
    }

    private static string GetGigantamaxEmoji(PKM pk, GameStrings Strings)
    {
        string gmax = "";
        if (pk is PK8 pk8)
        {
            if (pk8.CanGigantamax)
            {
                gmax = Emoji["Gigantamax"];
            }
            else
            {
                gmax = "Can't Gigantamax";
            }
        }
        return gmax;
    }

    private static string TeraEmoji(PKM pk, GameStrings Strings)
    {
        string tera = "";
        if (pk is PK9 pk9) 
        {
            var type = Strings.Types[(int)pk9.TeraType];
            tera = Emoji.ContainsKey(type) ? "- " + Emoji[type] + $" {pk9.TeraType}\n" : "";
        }

        return tera + "** **";
    }
    
    private static string GetMovesString(PKM pk, GameStrings Strings)
    {
        List<(ushort, int, int, byte)> moves =
        [
            (pk.Move1, pk.Move1_PP, pk.GetMovePP(pk.Move1, pk.Move1_PPUps), MoveInfo.GetType(pk.Move1, pk.Context)),
            (pk.Move2, pk.Move2_PP, pk.GetMovePP(pk.Move2, pk.Move2_PPUps), MoveInfo.GetType(pk.Move2, pk.Context)),
            (pk.Move3, pk.Move3_PP, pk.GetMovePP(pk.Move3, pk.Move3_PPUps), MoveInfo.GetType(pk.Move3, pk.Context)),
            (pk.Move4, pk.Move4_PP, pk.GetMovePP(pk.Move4, pk.Move4_PPUps), MoveInfo.GetType(pk.Move4, pk.Context)),
        ];
        string movestr = string.Concat(moves.Where(z => z.Item1 != 0).Select(z => $"{(Emoji.ContainsKey(Strings.Types[z.Item4]) ? "- " + Emoji[$"{Strings.Types[z.Item4]}"] + " " : "")}{Strings.Move[z.Item1]} ({z.Item2}/{z.Item3}){Environment.NewLine}"));

        return movestr + "** **";
    }

    private static string ScaleString(PKM pk)
    {
        string scaleString = "";
        if (pk is PK9 pk9)
        {
            scaleString = $"{PokeSizeDetailedUtil.GetSizeRating(pk9.Scale)}({pk9.Scale})";
        }
        else
        {
            scaleString = $"{PokeSizeDetailedUtil.GetSizeRating(((IScaledSize)pk).HeightScalar)}({((IScaledSize)pk).HeightScalar})";
        }
            return scaleString;
    }

    private static List<string> ConcatStringList(List<string> list1, List<string> list2)
    {
        List<string> result = [];
        if(list1.Count == 0 || list2.Count == 0)
            return result;
        result = list1.Select((z, Index) => Index >= list2.Count ? z + list2.Last() : z + list2[Index]).ToList();
        return result;
    }

    private static List<string> MarkEmoji(PKM pk, out List<string> MarkString)
    {
        List<string> emoji = [];
        MarkString = [];
        if (pk is PK8 pk8)
        {
            bool hasMark = HasOnlyMark(pk8, out List<RibbonIndex> mark);
            MarkString = hasMark ? mark.Select(z => z.ToString().Replace("Mark", "")).ToList() : [];
            foreach(var m in MarkString)
            {
                if (Emoji.ContainsKey(m))
                    emoji.Add($"{Emoji[m]}");
            }
        }
        else if(pk is PK9 pk9)
        {
            bool hasMark = HasOnlyMark(pk9, out List<RibbonIndex> mark);
            MarkString = hasMark ? mark.Select(z => z.ToString().Replace("Mark", "")).ToList() : [];
            foreach (var m in MarkString)
            {
                if (Emoji.ContainsKey(m))
                    emoji.Add($"{Emoji[m]}");
            }
        }
        return emoji;
    }

    private static List<string> RibbonEmoji(PKM pk, out List<string> RibbonString)
    {
        List<string> emoji = [];
        RibbonString = [];
        if (pk is PK8 pk8)
        {
            bool hasRibbon = HasOnlyRibbon(pk8, out List<RibbonIndex> mark);
            RibbonString = hasRibbon ? mark.Select(z => z == RibbonIndex.Partner ? z.ToString() + "Ribbon" : z.ToString().Replace("Mark", "")).ToList() : [];
            foreach(var ribbon in RibbonString)
            {                
                if (Emoji.ContainsKey(ribbon))
                    emoji.Add($"{Emoji[ribbon]}");
            }
        }
        else if (pk is PK9 pk9)
        {
            bool hasRibbon = HasOnlyRibbon(pk9, out List<RibbonIndex> mark);
            RibbonString = hasRibbon ? mark.Select(z => z == RibbonIndex.Partner ? z.ToString() + "Ribbon" : z.ToString().Replace("Mark", "")).ToList() : [];
            foreach (var ribbon in RibbonString)
            {
                if (Emoji.ContainsKey(ribbon))
                    emoji.Add($"{Emoji[ribbon]}");
            }
        }
        return emoji;
    }

    private static string ShinyEmoji(bool shiny, bool square)
    {
        if (!shiny)
            return "";
;
        if (square)
            return $"{Emoji["Square Shiny"]} ";
        return $"{Emoji["Shiny"]} ";
    }

    private static string GenderEmoji(int genderInt) => genderInt switch
    {
        (int)Gender.Male => Emoji["Male"],
        (int)Gender.Female => Emoji["Female"],
        _ => Emoji["Genderless"],
    };

    private static string IVsStringEmoji(ReadOnlySpan<int> ivs)
    {
        string s = string.Empty;
        var stats = new[] { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };
        string[] iv0 =
        [
            Emoji["Health 0"],
            Emoji["Attack 0"],
            Emoji["Defense 0"],
            Emoji["SpAttack 0"],
            Emoji["SpDefense 0"],
            Emoji["Speed 0"],
        ];
        string[] iv31 =
        [
            Emoji["Health 31"],
            Emoji["Attack 31"],
            Emoji["Defense 31"],
            Emoji["SpAttack 31"],
            Emoji["SpDefense 31"],
            Emoji["Speed 31"],
        ];
        for (int i = 0; i < ivs.Length; i++)
        {
            s += ivs[i] switch
            {
                00 => $"- {iv0[i]}{" " + stats[i]}",
                31 => $"- {iv31[i]}{" " + stats[i]}",
                _ => $"- `{ivs[i]}`{" " + stats[i]}",
            };

            if (i < 5)
                s += "\n";            
        }
        return s;
    }
    private static string GetBallImg(PKM pkm)
    {
        return $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)pkm.Ball}ball".ToLower() + ".png";
    }

    public static Dictionary<string, string> Emoji { get; set; } = new()
    {
        { "Classic", "<:ribbonclassic:1403723076268458005>" },
        { "Birthday", "<:ribbonbirthday:1403724402322968689>" },
        { "ChampionBattle", "<:ribbonchampionbattle:1403724865734840431>" },
        { "Event", "<:ribbonevent:1403724207258337331>" },
        { "Wishing", "<:ribbonwishing:1403723820585455646>" },
        { "PartnerRibbon" , "<:partner:1391327584620511366>" },
        { "Partner", "<:MarkPartner:1266688105277292544>" },
        { "Lunchtime", "<:lunchtime:1391338148071604246>" },    
        { "SleepyTime" , "<:sleepytime:1391326666055225364>" },    
        { "Dusk" , "<:dusk:1391329433864110090>" },    
        { "Dawn" , "<:dawn:1391329102291533844>" },    
        { "Cloudy" , "<:cloudy:1391328800217894932>" },    
        { "Rainy" , "<:rainy:1391351267959377931>" },    
        { "Stormy" , "<:stormy:1391326882519060570>" },    
        { "Snowy" , "<:snowy:1391326832023834676>" },    
        { "Blizzard" , "<:blizzard:1391328425259696139>" },    
        { "Dry" , "<:dry:1391329431456583740>" },    
        { "Sandstorm" , "<:sandstorm:1391326462539206716>" },    
        { "Misty" , "<:misty:1391339363195031655>" },    
        { "Destiny" , "<:destiny:1391338163397726218>" },    
        { "Fishing" , "<:fishing:1391329439094411384>" },    
        { "Curry" , "<:curry:1391328966555598989>" },    
        { "Uncommon" , "<:uncommon:1391327059535593532>" },    
        { "Rare" , "<:rare:1391338169605034056>" },    
        { "Rowdy" , "<:rowdy:1391329429615284287>" },    
        { "AbsentMinded" , "<:absentminded:1391328116877557790>" },    
        { "Jittery" , "<:jittery:1391350869135458425>" },    
        { "Excited" , "<:excited:1391329435810267216>" },    
        { "Charismatic" , "<:charismatic:1391328681816887367>" },
        { "Calmness" , "<:calmness:1391328523809194145>" },
        { "Intense" , "<:intense:1391329448913272883>" },
        { "ZonedOut" , "<:zonedout:1391327311088844830>" },
        { "Joyful" , "<:joyful:1391329454827110430>" },
        { "Angry" , "<:angry:1391328328774058004>" },
        { "Smiley" , "<:smiley:1391326789871075448>" },
        { "Teary" , "<:teary:1391326926777483264>" },
        { "Upbeat" , "<:upbeat:1391327161356521552>" },
        { "Peeved" , "<:peeved:1391339476667863040>" },
        { "Intellectual" , "<:intellectual:1391329447168311420>" },
        { "Ferocious" , "<:ferocious:1391329437500309565>" },
        { "Crafty" , "<:crafty:1391328899761307689>" },
        { "Scowling" , "<:scowling:1391326612460666950>" },
        { "Kindly" , "<:kindly:1391338151112347729>" },
        { "Flustered" , "<:flustered:1391329441304543313>" },
        { "PumpedUp" , "<:pumpedup:1391340374383005696>" },
        { "ZeroEnergy" , "<:zeroenergy:1391327259289194546>" },
        { "Prideful" , "<:prideful:1391340163300593704>" },
        { "Unsure" , "<:unsure:1391327116833853450>" },
        { "Humble" , "<:humble:1391329444999860304>" },
        { "Thorny" , "<:thorny:1391326965520400454>" },
        { "Vigor" , "<:vigor:1391327204797055079>" },
        { "Slump" , "<:slump:1391326751791120384>" },
        { "Hisui" , "<:hisui:1391351503192588298>" },
        { "TwinklingStar" , "<:twinklingstar:1391351557722996797>" },
        { "ChampionPaldea" , "<:championpaldea:1391351759175290940>" },
        { "Jumbo" , "<:jumbo:1391329458484412476>" },
        { "Mini" , "<:mini:1391339282039570432>" },
        { "Itemfinder" , "<:itemfinder:1391329450662166528>" },
        { "Gourmand" , "<:gourmand:1391329443015823500>" },
        { "OnceInALifetime" , "<:onceinalifetime:1391351895209017384>" },
        { "Alpha" , "<:alpha:1391328184477155338>" },
        { "Mightiest" , "<:mightiest:1391338172298039316>" },
        { "Titan" , "<:titan:1391327007140352072>" },
        { "Bug", "<:bug:1064546304048496812>" },
        { "Dark", "<:dark:1064557656079085588>" },
        { "Dragon", "<:dragon:1064557631890538566>" },
        { "Electric", "<:electric:1064557559563943956>" },
        { "Fairy", "<:fairy:1064557682566123701>" },
        { "Fighting", "<:fighting:1064546289406189648>" },
        { "Fire", "<:fire:1064557482468446230>" },
        { "Flying", "<:flying:1064546291239104623>" },
        { "Ghost", "<:ghost:1064546307848536115>" },
        { "Grass", "<:grass:1064557534096130099>" },
        { "Ground", "<:ground:1064546296725241988>" },
        { "Ice", "<:ice:1064557609857863770>" },
        { "Normal", "<:normal:1064546286247886938>" },
        { "Poison", "<:poison:1064546294854586400>" },
        { "Psychic", "<:psychic:1064557585124049006>" },
        { "Rock", "<:rock:1064546299992625242>" },
        { "Steel", "<:steel:1064557443742453790>" },
        { "Water", "<:water:1064557509404270642>" },
        { "Male", "<:male:1064844611341795398>" },
        { "Female", "<:female:1064844510636552212>" },
        { "Genderless", "<:gen_less:1254797542861045891>" },
        { "Gigantamax", "<:raid:944323014919618590>" },
        { "Shiny", "<:shiny:1064845915036323840>" },
        { "Square Shiny", ":white_square_button:" },
        { "Event Star", "<:bluestar:1064538604409471016>" },
        { "7 Star", "<:pinkstar:1064538642934140978>" },
        { "Star", "<:yellowstar:1064538672113922109>" },
        { "Egg", "<:65535chan:1032255574966009966>" },
        { "Health 0", "<:h0:1064842950573572126>" },
        { "Health 31", "<:h31:1064726680628895784>" },
        { "Attack 0", "<:a0:1064842895712075796>" },
        { "Attack 31", "<:a31:1064726668419289138>" },
        { "Defense 0", "<:b0:1064842811196833832>" },
        { "Defense 31", "<:b31:1064726671703429220>" },
        { "SpAttack 0", "<:c0:1064842749272133752>" },
        { "SpAttack 31", "<:c31:1064726673649582121>" },
        { "SpDefense 0", "<:d0:1064842668624068608>" },
        { "SpDefense 31", "<:d31:1064726677176987832>" },
        { "Speed 0", "<:s0:1064842545953243176>" },
        { "Speed 31", "<:s31:1064726682721865818>" },
        { "Sweet Herba", "<:sweetherba:1064541764163227759>" },
        { "Sour Herba", "<:sourherba:1064541770148483073>" },
        { "Salty Herba", "<:saltyherba:1064541768147796038>" },
        { "Bitter Herba", "<:bitterherba:1064541773763977256>" },
        { "Spicy Herba", "<:spicyherba:1064541776699994132>" },
        { "Bottle Cap", "<:bottlecap:1064537470370320495>" },
        { "Ability Capsule", "<:abilitycapsule:1064541406921752737>" },
        { "Ability Patch", "<:abilitypatch:1064538087763476522>" },
    };
}
