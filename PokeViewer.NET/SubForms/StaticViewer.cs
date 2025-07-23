using PKHeX.Core;
using static PokeViewer.NET.RoutineExecutor;
using static PokeViewer.NET.ViewerUtil;
using static System.Buffers.Binary.BinaryPrimitives;
using static SysBot.Base.SwitchButton;
using static PokeViewer.NET.SubForms.Egg_Viewer;
using SysBot.Base;
using System.Globalization;
using static PokeViewer.NET.SubForms.MiscViewer;
using System.Text;
using System.Security.Principal;
using System.Windows.Forms;
using System.DirectoryServices.ActiveDirectory;
using System.Threading.Tasks.Dataflow;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using System.ComponentModel;
using PokeViewer.NET.Misc;
using System.Net.Sockets;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using Newtonsoft.Json;
using PokeViewer.NET.Properties;
using PokeViewer.NET.Util;

namespace PokeViewer.NET.SubForms
{
    public partial class StaticViewer : Form
    {
        private readonly ViewerState Executor;
        protected ViewerOffsets Offsets { get; } = new();
        private static ulong BaseBlockKeyPointer = 0;
        public ulong PlayerOnMountOffset = 0;
        public ulong PlayerCanMoveOffset = 0;
        private ulong OverworldOffset = 0;
        private readonly Egg_Viewer eggviewer;
        private CancellationTokenSource? cts = null;
        private GameStrings Strings = GameInfo.GetStrings("en");
        private (Color, Color) setcolors;
        private FilterMode filtermode;
        private List<EncounterFilter> encounterFilters = new();
        private int[] IVFiltersMax = Array.Empty<int>();
        private int[] IVFiltersMin = Array.Empty<int>();
        private List<byte[]?> coordList = new();
        private (Color, Color) colors;
        private bool canceled = false;
        private Image? ShinySquare = null!;
        private Image? ShinyStar = null!;
        private List<string> SpeciesList = null!;
        private string[] FormsList = null!;
        private string[] TypesList = null!;
        private readonly string[] GenderList = null!;
        private ulong PreviousSaved = 0;
        private ushort TargetMon = 0;
        private byte TargetForm = 0;
        private readonly SimpleTrainerInfo TrainerInfo;
        public StaticViewer(int Gametype, ref ViewerState executor, (Color, Color) color, SimpleTrainerInfo trainerInfo)
        {
            InitializeComponent();
            Executor = executor;
            filtermode = FilterMode.Static;
            var filterpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{filtermode}filters.json");
            if (File.Exists(filterpath))
                encounterFilters = System.Text.Json.JsonSerializer.Deserialize<List<EncounterFilter>>(File.ReadAllText(filterpath)) ?? new List<EncounterFilter>();
            eggviewer = new Egg_Viewer(Gametype, ref executor, color, trainerInfo);
            TrainerInfo = trainerInfo;
            Language.DataSource = Enum.GetValues(typeof(LanguageID)).Cast<LanguageID>().Where(z => z != LanguageID.None && z != LanguageID.UNUSED_6).ToArray();
            Language.SelectedIndex = 1;
            Strings = GameInfo.GetStrings(Language.SelectedIndex);
            SpeciesList = Strings.specieslist.ToList();
            PokemonCombo.DataSource = Strings.specieslist.Where(z => PersonalTable.SV.IsSpeciesInGame((ushort)SpeciesList.IndexOf(z))).ToArray();
            FormsList = Strings.forms;
            TypesList = Strings.types;
            GenderList = [.. GameInfo.GenderSymbolUnicode];
            SetColors(color);
            colors = color;

        }

        private void SetColors((Color, Color) color)
        {
            BackColor = color.Item1;
            ForeColor = color.Item2;
            button1.BackColor = color.Item1;
            button1.ForeColor = color.Item2;
            HardStopButton.BackColor = color.Item1;
            HardStopButton.ForeColor = color.Item2;
            StopConditions.BackColor = color.Item1;
            StopConditions.ForeColor = color.Item2;
            textBox1.BackColor = color.Item1;
            textBox1.ForeColor = color.Item2;
            pictureBox1.BackColor = color.Item1;
            pictureBox2.BackColor = color.Item1;
            XCoord.BackColor = color.Item1;
            YCoord.BackColor = color.Item1;
            ZCoord.BackColor = color.Item1;
            setcolors = color;
        }
        private bool ValidateEncounter(PK9 pk, EncounterFilter filter)

        {
            if (filter.Species is not null && filter.Species != 0 && pk.Species != filter.Species)
                return false;

            if (filter.Form is not null && filter.Form != pk.Form)
                return false;

            var ivs = RaidCrawler.Core.Structures.Utils.ToSpeedLast(pk.IVs);
            if (!filter.ignoreIVs)
            {
                (IVFiltersMax, IVFiltersMin) = GrabIvFilters(filter);
                for (int i = 0; i < ivs.Length; i++)
                {
                    if (ivs[i] < IVFiltersMin[i] || ivs[i] > IVFiltersMax[i])
                        return false;
                }
            }
            if (filter.Nature is not null && filter.Nature.Count > 0 && !filter.Nature.Contains((Nature)pk.Nature))
                return false;

            if (filter.AbilityList is not null && filter.AbilityList.Count > 0 && !filter.AbilityList.Contains((Ability)pk.Ability))
                return false;

            var hasmark = HasMark(pk, out RibbonIndex mark);
            if (filter.MarkOnly && !hasmark)
                return false;

            if (filter.UnwantedMark is not null && filter.UnwantedMark.Count > 0 && hasmark && filter.UnwantedMark.Contains(mark))
                return false;
            
            if (filter.Mark is not null && filter.Mark.Count > 0 &&(!hasmark || !filter.Mark.Contains(mark)))
                return false;

            if (pk.Gender != filter.Gender && filter.Gender != 3)
                return false; // gender != gender filter when gender is not Any

            if (filter.Scale && pk.Scale > 0 && pk.Scale < 255) // Mini/Jumbo Only
                return false;

            if (filter.ItemList is not null && filter.ItemList.Count > 0 && !filter.ItemList.Contains(pk.HeldItem))
                return false;

            if (!pk.IsShiny && filter.Shiny != 0 && filter.Shiny != 1)
                return false;

            if (!pk.IsShiny && filter.Shiny == 0 || !pk.IsShiny && filter.Shiny == 1)
                return true;

            if (pk.IsShiny && filter.Shiny is not 0 or 1)
            {
                if (filter.Shiny is 4 && pk.ShinyXor != 0) // SquareOnly
                    return false;

                if (filter.Shiny is 3 && pk.ShinyXor > 0 && pk.ShinyXor > 16) // StarOnly
                    return false;

                if ((Species)pk.Species is Species.Dunsparce or Species.Tandemaus && pk.EncryptionConstant % 100 != 0 && filter.ThreeSegment)
                    return false;

                if ((Species)pk.Species is Species.Dunsparce or Species.Tandemaus && pk.EncryptionConstant % 100 == 0 && filter.ThreeSegment)
                    return true;
            }
            return true;
        }
        private static (int[], int[]) GrabIvFilters(EncounterFilter filter)
        {
            int[] ivsequencemax = Array.Empty<int>();
            int[] ivsequencemin = Array.Empty<int>();
            int filtersmax = filter.IVMaxindex == null ? 0 : (int)filter.IVMaxindex;
            switch (filtersmax)
            {
                case 0: ivsequencemax = filter.MaxIVs == null ? new[] { 31, 31, 31, 31, 31, 31 } : filter.MaxIVs; break;
                case 1: ivsequencemax = new[] { 31, 31, 31, 31, 31, 31 }; break;
                case 2: ivsequencemax = new[] { 31, 0, 31, 31, 31, 0 }; break;
                case 3: ivsequencemax = new[] { 31, 0, 31, 31, 31, 31 }; break;
                case 4: ivsequencemax = new[] { 31, 31, 31, 31, 31, 0 }; break;
            }
            int filtersmin = filter.IVMinindex == null ? 0 : (int)filter.IVMinindex;
            switch (filtersmin)
            {
                case 0: ivsequencemin = filter.MinIVs == null ? new[] { 0, 0, 0, 0, 0, 0 } : filter.MinIVs; break;
                case 1: ivsequencemin = new[] { 31, 31, 31, 31, 31, 31 }; break;
                case 2: ivsequencemin = new[] { 31, 0, 31, 31, 31, 0 }; break;
                case 3: ivsequencemin = new[] { 31, 0, 31, 31, 31, 31 }; break;
                case 4: ivsequencemin = new[] { 31, 31, 31, 31, 31, 0 }; break;
            }
            return (ivsequencemax, ivsequencemin);
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var success = true;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                if(PokemonCombo.SelectedIndex < 0)
                {
                    MessageBox.Show("Target Species is empty!");
                    return;
                }
                if(FormCombo.Visible && FormCombo.SelectedIndex < 0)
                {
                    MessageBox.Show("Target Form is empty!");
                    return;
                }
                if (encounterFilters.Count == 0 || encounterFilters.All(x => !x.Enabled))
                {
                    MessageBox.Show("No Filter Active!");
                    return;
                }
                if (button1.Enabled)
                    DisableOptions();
                canceled = false;
                try
                {
                    await AnticipateResponse(token).ConfigureAwait(false);
                    await InitilizeSessionOffsets(token).ConfigureAwait(false);
                    await ScanOverworld(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    MessageBox.Show(this, "Process has been canceled!", "Process cencel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    EnableOptions();
                }
                catch (Exception)
                {
                    success = false;
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                        }
                    }
                    try
                    {
                        await Task.Delay(300_000, token).ConfigureAwait(false);
                        Executor.SwitchConnection.Reset();
                        if (!Executor.SwitchConnection.Connected)
                            throw new Exception("SwitchConnection can't reconnect!");
                        await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
                        await CloseGame(token).ConfigureAwait(false);
                        await StartBackupGame(token).ConfigureAwait(false);
                    }
                    catch (SocketException err)
                    {
                        MessageBox.Show(this, err.ToString(), "ReConnect Sokcket Exception!");
                        EnableOptions();
                        return;
                    }
                    catch (Exception RecEx)
                    {
                        MessageBox.Show(this, RecEx.ToString(), "ReConnect Exception!");
                        EnableOptions();
                        return;
                    }
                    button1_Click(sender, e);
                    return;
                }
            }

            if (Executor.SwitchConnection.Connected)
                await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
            if (!cts.IsCancellationRequested && cts != null && success)
                MessageBox.Show(this, "Process has been finished!", "Process Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private async Task SVSaveGameOverworldStatic(CancellationToken token)
        {
            await Click(X, !CoordCheck.Checked ? 3_000 : 1_500, token).ConfigureAwait(false);
            for(int i = 0; i < 3; i++)
                await Click(R, 0_500, token).ConfigureAwait(false);
            await Click(A, 3_500, token).ConfigureAwait(false);
        }
        private async Task<(ulong, ulong, DateTime)> GetLastSaveTime(ulong init, CancellationToken token)
        {
            var data = await ReadBlock(init, BlocksOverworld.LastSaved, token).ConfigureAwait(false);
            var (LastSavedTime, Curdate) = GetDateTime(data.Item2);
            return (data.Item1, LastSavedTime, Curdate);
        }
        private async Task<bool> PlayerNotOnMount(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            while (PlayerOnMountOffset == 0)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            }
            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(PlayerOnMountOffset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }

        private async Task CollideRead(CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            var coords = await Executor.SwitchConnection.PointerPeek(12, Offsets.CollisionPointer, token).ConfigureAwait(false);
            XCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[..4])}";
            YCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[4..8])}";
            ZCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[8..12])}";
        }
        private async Task<(Single, Single, Single)> PlayerCoordRead(CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            var coords = await Executor.SwitchConnection.PointerPeek(12, Offsets.CollisionPointer, token).ConfigureAwait(false);
            Single x = BitConverter.ToSingle(coords.AsSpan()[..4]);
            Single y = BitConverter.ToSingle(coords.AsSpan()[4..8]);
            Single z = BitConverter.ToSingle(coords.AsSpan()[8..12]);
            return (x, y, z);
        }

        private async Task Collide(CancellationToken token)
        {
            await ToOverworldScreen(token).ConfigureAwait(false);
            var checkcount = 0;
            if (await PlayerNotOnMount(token).ConfigureAwait(false))
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
            while (await PlayerNotOnMount(token).ConfigureAwait(false))
            {
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
                checkcount++;
                if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                if (checkcount < 2)
                    continue;
                await Click(B, 0_500, token).ConfigureAwait(false);
                if (checkcount % 4 == 0)
                {
                    await SetStick(SwitchStick.LEFT, 30_000, -30_000, 0_400, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.LEFT, 0, 0, 0_050, token).ConfigureAwait(false);
                }
                if(checkcount % 4 == 1)
                {
                    await SetStick(SwitchStick.LEFT, 30_000, 30_000, 0_400, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.LEFT, 0, 0, 0_050, token).ConfigureAwait(false);
                }
                if(checkcount % 4 == 2)
                {
                    await SetStick(SwitchStick.LEFT, -30_000, 30_000, 0_400, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.LEFT, 0, 0, 0_050, token).ConfigureAwait(false);
                }
                if(checkcount % 4 == 3)
                {
                    await SetStick(SwitchStick.LEFT, -30_000, -30_000, 0_400, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.LEFT, 0, 0, 0_050, token).ConfigureAwait(false);
                }
            }

        }
        private async Task DisCollide(CancellationToken token)
        {
            var checkcount = 0;
            if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
            while (!await PlayerNotOnMount(token).ConfigureAwait(false))
            {
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
                if (await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                checkcount++;
                if (checkcount >= 2)
                    break;
            }
            await SetStick(SwitchStick.LEFT, 0, 10000, 0_050, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
        }
        public async Task CollideToCave(bool outbreakmode, CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            float coordx = Single.Parse(XCoord.Text, NumberStyles.Float);
            byte[] X1 = BitConverter.GetBytes(coordx);
            float coordy = Single.Parse(YCoord.Text, NumberStyles.Float);
            byte[] Y1 = BitConverter.GetBytes(coordy);
            float coordz = Single.Parse(ZCoord.Text, NumberStyles.Float);
            byte[] Z1 = BitConverter.GetBytes(coordz);

            X1 = X1.Concat(Y1).Concat(Z1).ToArray();
            float y = BitConverter.ToSingle(X1, 4);
            y += 40;
            if (outbreakmode)
                y += 80;
            WriteSingleLittleEndian(X1.AsSpan()[4..], y);

            for (int i = 0; i < 15; i++)
                await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
            if (outbreakmode)
            {
                await Task.Delay(3_000).ConfigureAwait(false);
                await Click(B, 19_000, token).ConfigureAwait(false);
            }

            await Task.Delay(6_000, token).ConfigureAwait(false);
            await DisCollide(token).ConfigureAwait(false);
        }
        private async Task TeleportToMatch(byte[]? cp, CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            float Y = BitConverter.ToSingle(cp!, 4);
            Y += 40;
            WriteSingleLittleEndian(cp.AsSpan()[4..], Y);

            for (int i = 0; i < 15; i++)
                await Executor.SwitchConnection.PointerPoke(cp!, Offsets.CollisionPointer, token).ConfigureAwait(false);

            await Task.Delay(6_000, token).ConfigureAwait(false);
            await DisCollide(token).ConfigureAwait(false);
        }

        private async Task CollideToSpot(string x, string Y, string z, CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(Y) && !string.IsNullOrEmpty(z))
            {
                float coordx = Single.Parse(x, NumberStyles.Float);
                byte[] X1 = BitConverter.GetBytes(coordx);
                float coordy = Single.Parse(Y, NumberStyles.Float);
                byte[] Y1 = BitConverter.GetBytes(coordy);
                float coordz = Single.Parse(z, NumberStyles.Float);
                byte[] Z1 = BitConverter.GetBytes(coordz);
                
                X1 = X1.Concat(Y1).Concat(Z1).ToArray();
                if (CoordNum.Value > 0)
                {
                    float y = BitConverter.ToSingle(X1, 4);
                    y += (float)CoordNum.Value;
                    coordy += (float)CoordNum.Value;
                    WriteSingleLittleEndian(X1.AsSpan()[4..], y);
                }

                var TeleportCoords = (coordx, coordy, coordz).ToTuple();
                for (int i = 0; i < 15; i++)
                    await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
                var PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
                while(Math.Abs(TeleportCoords.Item1 - PlayerCoords.Item1) > 5 || Math.Abs(TeleportCoords.Item2 - PlayerCoords.Item2) > 5 || Math.Abs(TeleportCoords.Item3 - PlayerCoords.Item3) > 5)
                {
                    await Task.Delay(0_500, token).ConfigureAwait(false);
                    for (int i = 0; i < 15; i++)
                        await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
                    PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
                }
                await Task.Delay(CoordNum.Value <= 10 ? 7_000 + (int)DelayNum.Value : 6_000 + (int)DelayNum.Value, token).ConfigureAwait(false);

            }
            else
            {
                if (string.IsNullOrEmpty(XCoord.Text) || string.IsNullOrEmpty(YCoord.Text) || string.IsNullOrEmpty(ZCoord.Text))
                {
                    MessageBox.Show("Scan location coord is empty. read Scan location coord!");
                    await CollideRead(token).ConfigureAwait(false);
                }
            }
        }
        private new async Task Click(SwitchButton b, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Click(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
        private async Task GotoAcurateCoord(CancellationToken token)
        {
            float coordx = Single.Parse(XCoord.Text, NumberStyles.Float);
            float coordy = Single.Parse(YCoord.Text, NumberStyles.Float);
            float coordz = Single.Parse(ZCoord.Text, NumberStyles.Float);
            var TeleportCoords = (coordx, coordy, coordz).ToTuple();
                        
            await DefeatPokemon(token).ConfigureAwait(false);
            var PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
            if (Math.Abs(TeleportCoords.Item1 - PlayerCoords.Item1) > 5 || Math.Abs(TeleportCoords.Item2 - PlayerCoords.Item2) > 5 || Math.Abs(TeleportCoords.Item3 - PlayerCoords.Item3) > 5)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                await CollideToSpot(XCoord.Text, YCoord.Text, ZCoord.Text, token).ConfigureAwait(false);
                await GotoAcurateCoord(token).ConfigureAwait(false);
            }

        }
        private async Task AnticipateResponse(CancellationToken token)
        {
            using HttpClient client = new();
            string shinyicon = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.WinForms/Resources/img/Markings/";
            var square = await client.GetStreamAsync(shinyicon + "rare_icon_2.png", token).ConfigureAwait(false);
            ShinySquare = Image.FromStream(square);

            var star = await client.GetStreamAsync(shinyicon + "rare_icon.png", token).ConfigureAwait(false);
            ShinyStar = Image.FromStream(star);
        }
        private async Task ScanOverworld(CancellationToken token)
        {
            using HttpClient client = new();
            coordList = new();
            string sprite;
            string species = string.Empty;
            string caption = string.Empty;
            int encountercount = 0;
            int resetcount = 0;
            ulong init = 0;
            ulong LastSaveInit = 0;
            RateBox.Text = string.Empty;
            TargetMon =(ushort)SpeciesList.IndexOf((string)PokemonCombo.Items[PokemonCombo.SelectedIndex]!);
            if (FormCombo.Visible)
            {
                var formslist = FormConverter.GetFormList(TargetMon, TypesList, FormsList, GenderList, EntityContext.Gen9).ToList();
                TargetForm = (byte)formslist.IndexOf((string)FormCombo.Items[FormCombo.SelectedIndex]!);
            }
            else
                TargetForm = 0;
            while (!token.IsCancellationRequested)
            {
                bool staticfound = false;
                button1.Text = "Scan!";
                bool match = false;
                pictureBox1.Image = null;
                pictureBox2.Image = null;
                pictureBox3.Image = null;
                textBox1.Text = string.Empty;
                textBox1.BackColor = colors.Item1;
                textBox1.ForeColor = colors.Item2;
                
                if (!NonSave.Checked)
                {
                    (LastSaveInit, PreviousSaved, DateTime PreviousDate) = await GetLastSaveTime(LastSaveInit, token).ConfigureAwait(false);
                    PreSaveBox.Text = $"{PreviousDate}";
                    var TempAd = LastSaveInit;
                    if (!CoordCheck.Checked)
                    {
                        await CollideToSpot(XCoord.Text, YCoord.Text, ZCoord.Text, token).ConfigureAwait(false);                        
                    }
                    else
                    {
                        await SetStick(SwitchStick.LEFT, 0, 30000, (int)DelayNum.Value, token).ConfigureAwait(false);
                        await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
                    }
ReSave:
                    if(!CoordCheck.Checked)
                        await GotoAcurateCoord(token).ConfigureAwait(false);
                    else
                     {
                        await DefeatPokemon(token).ConfigureAwait(false);
                        await ToGameOverworld(token).ConfigureAwait(false);
                     }
                    await SVSaveGameOverworldStatic(token).ConfigureAwait(false);
                    (LastSaveInit, var LastSaved, DateTime CurDate) = await GetLastSaveTime(LastSaveInit, token).ConfigureAwait(false);
                    LastSavedBox.Text = $"{CurDate}";
                    if (LastSaved == PreviousSaved)
                    {
                        await ToGameOverworld(token).ConfigureAwait(false);
                        goto ReSave;
                    }
                }

                button1.Text = "Scanning...";
                (init, var test) = await ReadBlock(init, BlocksOverworld.Overworld, token).ConfigureAwait(false);
                resetcount++;
                for (int i = 0; i < 20; i++)
                {
                    var data = test.AsSpan(0 + (i * 0x1D4), 0x158).ToArray();
                    var coord = test.AsSpan(0 + (i * 0x1D4) + 0x158, 0xC).ToArray();
                    var pk = new PK9(data);
                    if (pk.Species != TargetMon || (FormCombo.Visible && pk.Form != TargetForm) || pk.FlawlessIVCount < 3)
                        continue;

                    bool isValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                    if (!isValid || pk == null || pk.Species < 0 || pk.Species > (int)Species.MAX_COUNT)                    
                        break;                    
                    staticfound = true;
                    encountercount++;
                    string outst = GetRealPokemonString(encountercount, pk);
                    string output = $"Reset #{resetcount} (Rate: {100.00 * encountercount / resetcount : 0.00}%){Environment.NewLine}" + outst;
                    textBox1.Text = output;
                    var Rate = SubForms.StopConditions.CalcRate(encounterFilters);
                    var DisplayRate = (1.00 - Math.Pow(1.00 - Rate, encountercount)) * 100.00;
                    RateBox.Text = $"Encounter #{encountercount}{Environment.NewLine}Target Rate: {DisplayRate:0.00}%";
                    sprite = PokeImg(pk, false);
                    var response = await client.GetStreamAsync(sprite, token).ConfigureAwait(false);
                    Image img = Image.FromStream(response);
                    var img2 = (Image)new Bitmap(img, new Size(img.Width, img.Height));
                    img2 = ApplyTeraColor((byte)pk.TeraType, img2, SpriteBackgroundType.BottomStripe);
                    pictureBox2.Image = img2;
                    pictureBox3.Image = GetGemImage((int)pk.TeraType);

                    if (pk.IsShiny)
                    {
                        Image? shiny = pk.ShinyXor == 0 ? ShinySquare : ShinyStar;
                        pictureBox1.Image = shiny;
                    }
                    for (int j = 0; j < encounterFilters.Count; j++)
                    {
                        if (!encounterFilters[j].Enabled)
                            continue;

                        if (ValidateEncounter(pk, encounterFilters[j]))
                        {
                            species = $"Match Found {Strings.Species[pk.Species]}{FormOutput(Strings, pk.Species, pk.Form, out _)}!";
                            caption = $"Filter {encounterFilters[j].Name} is satisfied!";
                            match = true;
                            WebHookUtil.SendDetailNotifications(pk, sprite, true, TrainerInfo);
                            if (pk.IsShiny)
                            {
                                textBox1.BackColor = Color.Gold;
                                textBox1.ForeColor = Color.BlueViolet;
                            }
                            else
                            {
                                textBox1.BackColor = Color.YellowGreen;
                                textBox1.ForeColor = Color.OrangeRed;
                            }
                            break;
                        }
                    }
                    coordList.Add(coord);
                    if (staticfound)
                    {
                        if (!match)
                            WebHookUtil.SendDetailNotifications(pk, sprite, false, TrainerInfo, true, encountercount, DisplayRate);
                        break;
                    }
                }
                if (!staticfound)
                {
                    textBox1.Text = $"Reset #{resetcount} (Rate: {100.00 * encountercount / resetcount: 0.00}%){Environment.NewLine}No Static Pokémon present.";
                    sprite = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/starter.png";
                    pictureBox2.Load(sprite);
                }
                button1.Text = "Done";
                TeleportIndex.Maximum = coordList.Count - 1;
                if (match)
                {
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    TeleportIndex.Value = coordList.Count - 1;
                    MessageBox.Show(this, species, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    EnableOptions();
                    canceled = true;
                    return;
                }
                if (checkBox4.Checked || NonSave.Checked)
                {
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    TeleportIndex.Value = coordList.Count - 1;
                    EnableOptions();
                    if (checkBox4.Checked)
                        MessageBox.Show("HardStop is initialized!");
                    else
                        MessageBox.Show("Complete Non Save Scan!");
                    canceled = true;
                    return;
                }
                if(resetcount >= 50 && 100.00 * encountercount / resetcount < 40.00)
                {
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    MessageBox.Show("Respawn Rate is too low. Change Scan Coordinates!");
                    EnableOptions();
                    canceled = true;
                    return;
                }
                coordList = new();
                await CloseGame(token).ConfigureAwait(false);
                await StartBackupGame(token).ConfigureAwait(false);
                await InitilizeSessionOffsets(token).ConfigureAwait(false);
                init = 0;
                LastSaveInit = 0;
            }
            EnableOptions();
            canceled = true;
            return;
        }
        private Image? GetGemImage(int teratype)
        {
            var baseurl = $"https://raw.githubusercontent.com/LegoFigure11/RaidCrawler/main/RaidCrawler.WinForms/Resources/gem_{teratype:D2}.png";
            PictureBox picture = new();
            picture.Load(baseurl);
            var baseImg = picture.Image;
            if (baseImg is null)
                return null;

            var backlayer = new Bitmap(baseImg.Width + 10, baseImg.Height + 10, baseImg.PixelFormat);
            baseImg = ImageUtil.LayerImage(backlayer, baseImg, 5, 5);
            var pixels = ImageUtil.GetPixelData((Bitmap)baseImg);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0)
                {
                    pixels[i] = 0;
                    pixels[i + 1] = 0;
                    pixels[i + 2] = 0;
                }
            }

            baseImg = ImageUtil.GetBitmap(pixels, baseImg.Width, baseImg.Height, baseImg.PixelFormat);
            return baseImg;
        }
        private Image ApplyTeraColor(byte elementalType, Image img, SpriteBackgroundType type)
        {
            var color = TypeColor.GetTypeSpriteColor(elementalType);
            var thk = SpriteBuilder.ShowTeraThicknessStripe;
            var op = SpriteBuilder.ShowTeraOpacityStripe;
            var bg = SpriteBuilder.ShowTeraOpacityBackground;
            return ApplyColor(img, type, color, thk, op, bg);
        }
        private Image ApplyColor(Image img, SpriteBackgroundType type, Color color, int thick, byte opacStripe, byte opacBack)
        {
            if (type == SpriteBackgroundType.BottomStripe)
            {
                int stripeHeight = thick; // from bottom
                if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                    stripeHeight = img.Height;

                return ImageUtil.BlendTransparentTo(img, color, opacStripe, img.Width * 4 * (img.Height - stripeHeight));
            }
            if (type == SpriteBackgroundType.TopStripe)
            {
                int stripeHeight = thick; // from top
                if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                    stripeHeight = img.Height;

                return ImageUtil.BlendTransparentTo(img, color, opacStripe, 0, (img.Width * 4 * stripeHeight) - 4);
            }
            if (type == SpriteBackgroundType.FullBackground) // full background
                return ImageUtil.BlendTransparentTo(img, color, opacBack);
            return img;
        }
        private async Task InitilizeSessionOffsets(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();
            
            BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            OverworldOffset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            return;
        }

        private string GetRealPokemonString(int encountercount, PK9 pkm)
        {
            string pid = $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 1)}: {pkm.PID:X8}";
            string ec = $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 2)}: {pkm.EncryptionConstant:X8}";
            var form = FormOutput(Strings, pkm.Species, pkm.Form, out _);
            string gender = string.Empty;
            string moveoutput = string.Empty;
            switch (pkm.Gender)
            {
                case 0: gender = $" ({eggviewer.ChangeLanguageString(Language.SelectedIndex, 3)})"; break;
                case 1: gender = $" ({eggviewer.ChangeLanguageString(Language.SelectedIndex, 4)})"; break;
                case 2: break;
            }
            var hasMark = HasMark(pkm, out RibbonIndex mark);
            var markinfo = string.Empty;
            if (hasMark)
            {
                var markstr = Strings.ribbons[53 + (int)mark].Split(new char[] { ' ', '\t' }).ToArray().AsSpan()[1..].ToArray();
                markinfo = string.Join("", markstr);
                markinfo = markinfo.Replace("Mark", "").Replace("-Zeichen", "").Replace("Emblema", "").Replace("Insigne", "");
            }
            string msg = hasMark ? $"{Environment.NewLine}{ChangeLanguageStringWide(Language.SelectedIndex, 0)}: {markinfo}" : "";
            int moveoffset = 0;
            for (int i = 0; i < 4; i++)
            {
                if (pkm.Moves[i] == 0)
                {
                    moveoffset = i;
                    break;
                }
                else if (i == 3)
                    moveoffset = 4;
            }
            for (int i = 0; i < moveoffset; i++)
            {
                if (i == 0)
                    moveoutput += $"{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move1_PP}/{pkm.GetMovePP(pkm.Move1, 0)}";
                else if (i == 1)
                    moveoutput += $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move2_PP}/{pkm.GetMovePP(pkm.Move2, 0)}";
                else if (i == 2)
                    moveoutput += $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move3_PP}/{pkm.GetMovePP(pkm.Move3, 0)}";
                else if (i == 3)
                    moveoutput += $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move4_PP}/{pkm.GetMovePP(pkm.Move4, 0)}";
            }
            string output = $"Encounter #{encountercount}{Environment.NewLine}{(pkm.ShinyXor == 0 ? "■ - " : pkm.ShinyXor <= 16 ? "★ - " : "")}{Strings.Species[pkm.Species]}{form}{gender}{pid}{ec}{Environment.NewLine}{ChangeLanguageStringWide(Language.SelectedIndex, 22)}: {Strings.Types[(int)pkm.TeraType]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 8)}: {Strings.Natures[(byte)pkm.Nature]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 9)}: {(Strings.Ability[pkm.Ability]).Replace(" ", "")}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 41)}: {Strings.Item[pkm.HeldItem]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 10)}: {pkm.IV_HP}/{pkm.IV_ATK}/{pkm.IV_DEF}/{pkm.IV_SPA}/{pkm.IV_SPD}/{pkm.IV_SPE}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 40)}: {pkm.MetLevel}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 11)}: {PokeSizeDetailedUtil.GetSizeRating(pkm.Scale)}{Environment.NewLine}{ChangeLanguageStringWide(Language.SelectedIndex, 21)}: {pkm.Scale}{msg}{Environment.NewLine}" + moveoutput;
            return output;
        }

        public string ChangeLanguageStringWide(int language, int index, double count = 0)
        {
            var str = string.Empty;
            switch (language)
            {
                case 0: str = ((string_ja)index).ToString(); break;
                default: str = ((string_en)index).ToString().Replace("_", " "); break;
            }
            if (language != 0)
            {
                if (index == 9 || index == 11 || index == 15 || index == 16 || index == 19)
                    str += ".";

            }
            else
            {
                if (index == 9)
                    str += "。";
            }
            return str;
        }

        private enum string_en : int
        {
            Mark,
            Scan,
            Scaning,
            Done,
            Not_Active,
            Outbreak_is_not_Active,
            are_in_the_overworld,
            is_in_the_overworld,
            Shiny,
            No_Pokémon_present,
            Stop_Scaning,
            Seleted_shiny_power_type_is_same_as_previous_one,
            change,
            Shiny_power_prompt,
            Scan_location_coord_is_read,
            Go_to_Teleport_location,
            Scan_Location_is_not_empty,
            continue_in_same_location,
            Scan_location_prompt,
            Teleport_Location_is_not_empty,
            Teleport_location_prompt,
            Real_Scale,
            TeraType

        }

        private enum string_ja : int
        {
            証,
            Scan,
            Scaning,
            Done,
            Not_Active,
            Outbreak_is_not_Active,
            are_in_the_overworld,
            is_in_the_overworld,
            Shiny,
            No_Pokémon_present,
            Stop_Scaning,
            Seleted_shiny_power_type_is_same_as_previous_one,
            change,
            Shiny_power_prompt,
            Scan_location_coord_is_read,
            Go_to_Teleport_location,
            Scan_Location_is_not_empty,
            continue_in_same_location,
            Scan_location_prompt,
            Teleport_Location_is_not_empty,
            Teleport_location_prompt,
            大きさ実数値,
            テラスタイプ,

        }

        private void LanguageChanged(object sender, EventArgs e)
        {
            Strings = GameInfo.GetStrings(Language.SelectedIndex < 0 ? 1 : Language.SelectedIndex);
            languageindex = Language.SelectedIndex;
            var specindex = PokemonCombo.SelectedIndex;
            var formindex = FormCombo.Visible ? FormCombo.SelectedIndex : 0;
            SpeciesList = Strings.specieslist.ToList();
            PokemonCombo.DataSource = Strings.specieslist.Where(z => PersonalTable.SV.IsSpeciesInGame((ushort)SpeciesList.IndexOf(z))).ToArray();
            FormsList = Strings.forms;
            TypesList = Strings.types;
            PokemonCombo.SelectedIndex = specindex < 0 ? 0 : specindex;
            if (FormCombo.Visible)
                FormCombo.SelectedIndex = formindex < 0 ? 0 : formindex;
            switch (Language.SelectedItem)
            {
                case "ja":
                    {
                        button1.Text = "スキャン！";
                        HardStopButton.Text = "強制終了";
                        StopConditions.Text = "厳選条件";
                        checkBox4.Text = "中断";
                        checkBox5.Text = "リセットした?";
                        Item7Label.Text = "X座標";
                        Item8Label.Text = "Y座標";
                        Item9Label.Text = "Z座標";
                        break;
                    }
                case "en":
                    {
                        button1.Text = "Scan!";
                        HardStopButton.Text = "HardStop";
                        StopConditions.Text = "StopConditions";
                        checkBox4.Text = "Stop";
                        checkBox5.Text = "Reset?";
                        Item7Label.Text = "Coord X";
                        Item8Label.Text = "Coord Y";
                        Item9Label.Text = "Coord Z";
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        private void SetForm()
        {
            if (PokemonCombo.SelectedIndex < 0)
                return;
            FormCombo.Items.Clear();
            FormCombo.Text = string.Empty;
            ushort SpeciesIndex = (ushort)SpeciesList.IndexOf((string)PokemonCombo.Items[PokemonCombo.SelectedIndex]!);
            byte FormCount = PersonalTable.SV.GetFormEntry(SpeciesIndex, 0).FormCount;
            List<byte> FormList = [];
            for (byte form = 0; form < FormCount; form++)
            {
                if (PersonalTable.SV.IsPresentInGame(SpeciesIndex, form))
                    FormList.Add(form);
            }
            var FormStringList = FormConverter.GetFormList(SpeciesIndex, TypesList, FormsList, GenderList, EntityContext.Gen9);
            var formlist = FormList.Select(x => FormStringList[x]).ToArray();
            if ((Species)SpeciesIndex == Species.Minior)
                formlist = formlist.Skip((formlist.Length + 1) / 2).ToArray();

            if (formlist.Length == 0 || (formlist.Length == 1 && formlist[0].Equals("")))
                FormCombo.Visible = false;
            else
            {
                FormCombo.Items.AddRange(formlist);
                FormCombo.Visible = true;
                FormCombo.Enabled = true;
            }
        }
        private void PokemonCombo_SelectedIndexChanged(object sender, EventArgs e) => SetForm();
        private async Task<(uint, ulong)> ReadEncryptedBlockUint(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (ReadUInt32LittleEndian(header.AsSpan()[1..]), address);
        }

        private async Task<(byte, ulong)> ReadEncryptedBlockByte(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (header[1], address);
        }

        private async Task<(byte[], ulong)> ReadEncryptedBlockHeader(DataBlock block, ulong init, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            if (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);

            return (header, init);
        }

        private async Task<bool> IsInBattle(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(Offsets.IsInBattle, 1, token).ConfigureAwait(false);
            return data[0] != 0x06;
        }
        private async Task DefeatPokemon(CancellationToken token)
        {
            while (await IsInBattle(token).ConfigureAwait(false))
                await Click(A, 0_800, token).ConfigureAwait(false);
        }
        private async Task ToGameOverworld(ulong OverworldOffset, CancellationToken token)
        {
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_500, token).ConfigureAwait(false);

            await Task.Delay(1_000, token).ConfigureAwait(false);
        }
        public async Task CloseGame(CancellationToken token)
        {
            // Close out of the game
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(HOME, 2_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
        }

        public async Task StartBackupGame(CancellationToken token)
        {
            await Click(HOME, 0_700, token).ConfigureAwait(false);
            // Open game.
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);


            // Switch Logo and game load screen
            await Task.Delay(25_000, token).ConfigureAwait(false);
            
            while (!await IsOnTitleScreen(token).ConfigureAwait(false))
                await Task.Delay(2_000).ConfigureAwait(false);

            await MultiPressAndHold(DUP, X, B, 0_850, 2_500, token).ConfigureAwait(false);
            while (await IsInTitle(token).ConfigureAwait(false))
                await MultiPressAndHold(DUP, X, B, 0_850, 2_500, token).ConfigureAwait(false);
            
            bool success = await ToOverworld(token).ConfigureAwait(false);
            if (!success)
                return;
            if (!CoordCheck.Checked)
            {
                await SetStick(SwitchStick.LEFT, 30000, -30000, 0_400, token).ConfigureAwait(false);
                await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
            }
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
        public async Task StartBackupGamePre(CancellationToken token)
        {
            // Open game.
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);


            // Switch Logo and game load screen
            await Task.Delay(21_000, token).ConfigureAwait(false);

            while (!await IsOnTitleScreen(token).ConfigureAwait(false))
                await Task.Delay(2_000).ConfigureAwait(false);

            await MultiPressAndHold(DUP, X, B, 1_500, 2_000, token).ConfigureAwait(false);            
        }
        private async Task ToOverworldScreen(CancellationToken token)
        {
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                await Click(B, 0_800, token).ConfigureAwait(false);
        }
        private async Task<bool> ToOverworld(CancellationToken token)
        {
            for (int i = 0; i < 6; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);

            var timer = 25_000;
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0)
                {
                    var title = await Executor.SwitchConnection.GetTitleID(token).ConfigureAwait(false);
                    if (title != ScarletID && title != VioletID)
                    {
                        await StartBackupGame(token).ConfigureAwait(false);
                        return false;
                    }
                    while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                    {
                        if(!await OverworldMount(token).ConfigureAwait(false) || !await IsInTitle(token).ConfigureAwait(false))
                            await Click(A, 6_000, token).ConfigureAwait(false);
                        if (await IsOnOverworldTitle(token).ConfigureAwait(false))
                            break;
                        if(await OverworldMount(token).ConfigureAwait(false))
                        {
                            var offs = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
                            while (!await IsOnOverworld(offs, token).ConfigureAwait(false))
                            {
                                await Click(B, 0_600, token).ConfigureAwait(false);
                            }
                        }
                    }
                    break;
                }
            }
            await Task.Delay(0_500, token).ConfigureAwait(false);
            return true;
        }
        private async Task<bool> IsOnOverworldTitle(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }
        private async Task<bool> OverworldMount(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return true;
        }
        private async Task<bool> IsOnTitleScreen(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            if (!await IsInTitle(token).ConfigureAwait(false))
                return false;
            return true;
        }
        private async Task<bool> IsInTitle(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(Offsets.PicnicMenu, 1, token).ConfigureAwait(false);
            return data[0] == 0x01; // 1 when in picnic, 2 in sandwich menu, 3 when eating, 2 when done eating
        }
        private async Task ChangeTrainerCoordinates(CancellationToken token)
        {
            await CloseGame(token).ConfigureAwait(false);
            await StartBackupGamePre(token).ConfigureAwait(false);
            await Task.Delay(3_000, token).ConfigureAwait(false);
            var TrainerCoords = await ReadEncryptedBlockArray(BlocksOverworld.KCoordinates, token).ConfigureAwait(false);
            await Task.Delay(1_000, token).ConfigureAwait(false);
            var TrainerCoordsGreen = await ReadEncryptedBlockArray(BlocksOverworld.KPlayerLastGreenPosition, token).ConfigureAwait(false);
            TrainerCoords = TrainerCoords == null ? [] : TrainerCoords;
            TrainerCoordsGreen = TrainerCoordsGreen == null ? [] : TrainerCoordsGreen;
            float coordx = Single.Parse(XCoord.Text, NumberStyles.Float);
            byte[] X1 = BitConverter.GetBytes(coordx);
            float coordy = Single.Parse(YCoord.Text, NumberStyles.Float);
            byte[] Y1 = BitConverter.GetBytes(coordy);
            float coordz = Single.Parse(ZCoord.Text, NumberStyles.Float);
            byte[] Z1 = BitConverter.GetBytes(coordz);
            X1 = X1.Concat(Y1).Concat(Z1).ToArray();
            await WriteEncryptedBlockArray(BlocksOverworld.KCoordinates, TrainerCoords, X1, token).ConfigureAwait(false);
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await WriteEncryptedBlockArray(BlocksOverworld.KPlayerLastGreenPosition, TrainerCoordsGreen, X1, token).ConfigureAwait(false);
            await ToOverworld(token).ConfigureAwait(false);

        }
        public async Task PressAndHold(SwitchButton b, int hold, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.Connection.SendAsync(SwitchCommand.Hold(b, true), token).ConfigureAwait(false);
            await Task.Delay(hold, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        public async Task MultiPressAndHold(SwitchButton b, SwitchButton c, SwitchButton d, int hold, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.Connection.SendAsync(SwitchCommand.Hold(b, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(0_800, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Hold(c, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(0_800, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Hold(d, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(hold, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(b, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(0_800, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(c, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(0_800, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(d, Executor.UseCRLF), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
        private async void CoordChangeButton_Click(object sender, EventArgs e)
        {
            DisableOptions();
            var token = CancellationToken.None;
            await ChangeTrainerCoordinates(token).ConfigureAwait(false);
            MessageBox.Show("Complete!");
            EnableOptions();
        }
        private async void TeleportButton_Click(object sender, EventArgs e)
        {
            bool success = true;
            canceled = false;
            DisableOptions();
            if (coordList.Count <= 0)
            {
                MessageBox.Show("Coord List is Empty!");
                return;
            }
            var coords = coordList[(int)TeleportIndex.Value];
            if (coords == null)
            {
                MessageBox.Show("Target Coord is null!");
                return;
            }
            var x = BitConverter.ToSingle(coords, 0);
            var y = BitConverter.ToSingle(coords, 4);
            var z = BitConverter.ToSingle(coords, 8);
            if (x == 0 || y == 0 || z == 0)
            {
                MessageBox.Show("Target Coord is Invalid!");
                return;
            }
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    if (checkBox5.Checked)
                    {
                        await InitilizeSessionOffsets(token).ConfigureAwait(false);
                        checkBox5.Checked = false;
                    }
                    await TeleportToMatch(coordList[(int)TeleportIndex.Value], token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    MessageBox.Show("The Operation is Canceled!");
                }
                catch (Exception ex)
                {
                    success = false;
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                        }
                    }
                    MessageBox.Show(ex.ToString(), "Exception Occured!");
                }
            }
            EnableOptions();
            canceled = true;
            if (success)
                MessageBox.Show("Completed!");
        }

        private async void TeleportCoordButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(XCoord.Text) || string.IsNullOrEmpty(YCoord.Text) || string.IsNullOrEmpty(ZCoord.Text))
            {
                MessageBox.Show("Teleport Coords is empty!");
                return;
            }
            if (!Single.TryParse(XCoord.Text, out _) || !Single.TryParse(YCoord.Text, out _) || !Single.TryParse(ZCoord.Text, out _))
            {
                MessageBox.Show("Can't covert coords to single!");
                return;
            }
            DisableOptions();
            canceled = false;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    if (checkBox5.Checked)
                    {
                        await InitilizeSessionOffsets(token).ConfigureAwait(false);
                        checkBox5.Checked = false;
                    }
                    await CollideToCave(false, token).ConfigureAwait(false);
                    MessageBox.Show("Completed!");
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("The operation is canceled!");
                }
                catch (Exception ex)
                {
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                        }
                    }
                    MessageBox.Show(ex.ToString());
                }
            }
            EnableOptions();
            canceled = true;
        }
        private async void ReadCoordButton_Click(object sender, EventArgs e)
        {
            DisableOptions();
            canceled = false;
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    if (checkBox5.Checked)
                    {
                        await InitilizeSessionOffsets(token).ConfigureAwait(false);
                        checkBox5.Checked = false;
                    }
                    await CollideRead(token).ConfigureAwait(false);
                    MessageBox.Show("Completed!");
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("The operation is canceled!");
                }
                catch (Exception ex)
                {
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                        }
                    }
                    MessageBox.Show(ex.ToString());
                }
            }
            EnableOptions();
            canceled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("This will restart the application. Do you wish to continue?", "Hard Stop Initiated", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                try
                {
                    Executor.Disconnect();
                }
                catch (SocketException ex)
                {
                    MessageBox.Show(this, ex.ToString(), "Soket Exception!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                System.Windows.Forms.Application.Restart();
                canceled = false;
            }

            else if (dialogResult == DialogResult.No)
            {
                if (cts == null)
                    return;
                else if (cts.IsCancellationRequested || canceled)
                {
                    MessageBox.Show("Process was alreadey canceled!");
                    return;
                }

                cts.Cancel();
                canceled = true;
            }
        }
        
        private async Task<(ulong, byte[])> ReadBlock(ulong init, DataBlock block, CancellationToken token)
        {
            return await ReadEncryptedBlockObject(init, block, token).ConfigureAwait(false);
        }

        private async Task SetStick(SwitchStick stick, short x, short y, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var cmd = SwitchCommand.SetStick(stick, x, y, true);
            await Executor.SwitchConnection.SendAsync(cmd, token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x11;
        }

        private async Task<bool> ReadEncryptedBlockBool(DataBlock block, CancellationToken token)
        {
            var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
            address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data);
            return res[0] == 2;
        }
        private async Task<(long, ulong)> ReadEncryptedBlockInt64(DataBlock block, ulong init, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();

            if (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 1 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);

            return (ReadInt64LittleEndian(data.AsSpan()[1..]), init);
        }
        private async Task<(ulong, byte[])> ReadEncryptedBlockObject(ulong init, DataBlock block, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();
            
            if (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 5 + block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data)[5..];

            return (init, res);
        }
        private async Task<byte[]?> ReadEncryptedBlockArray(DataBlock block, CancellationToken token)
        {
            var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
            address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);

            return data[6..];
        }
        
        public async Task<ulong> SearchSaveKey(uint key, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(BaseBlockKeyPointer + 8, 16, token).ConfigureAwait(false);
            var start = BitConverter.ToUInt64(data.AsSpan()[..8]);
            var end = BitConverter.ToUInt64(data.AsSpan()[8..]);

            while (start < end)
            {
                var block_ct = (end - start) / 48;
                var mid = start + (block_ct >> 1) * 48;

                data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(mid, 4, token).ConfigureAwait(false);
                var found = BitConverter.ToUInt32(data);
                if (found == key)
                    return mid;

                if (found >= key)
                    end = mid;
                else start = mid + 48;
            }
            return start;
        }
        private async Task<bool> WriteEncryptedBlockArray(DataBlock block, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception ex) 
            {
                MessageBox.Show(ex.ToString());
                return false; 
            }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[6..];
            if (!ram.SequenceEqual(arrayToExpect))
            {
                MessageBox.Show("Target Block is wrong!");
                return false; 
            }
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 6, block.Size);
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        private (ulong, DateTime) GetDateTime(byte[] data)
        {
            Epoch1900DateTimeValue epoch1900DateTime = new(data);
            return (epoch1900DateTime.TotalSeconds, epoch1900DateTime.Timestamp);
        }
        public static class BlocksOverworld
        {
            public static DataBlock Overworld = new()
            {
                Name = "Overworld",
                Key = 0x173304D8,
                Type = SCTypeCode.Object,
                IsEncrypted = true,
                Size = 0x2490,
            };
            public static DataBlock KPlayerLastGreenPosition = new()
            {
                Name = "KPlayerLastGreenPosition",
                Key = 0x5C6F8291,
                Type = SCTypeCode.Array,
                SubType = SCTypeCode.Single,
                IsEncrypted = true,
                Size = 0xC,
            };
            public static DataBlock KCoordinates = new()
            {
                Name = "KCoordinates",
                Key = 0x708D1511,
                Type = SCTypeCode.Array,
                SubType = SCTypeCode.Single,
                IsEncrypted = true,
                Size = 0xC,
            };
            public static DataBlock LastSaved = new()
            {
                Name ="KLastSaved",
                Key = 0x1522C79C,
                Type = SCTypeCode.Object,
                IsEncrypted = true,
                Size = 0x8,
            };
        }


        private byte[] DecryptBlock(uint key, byte[] block)
        {
            var rng = new SCXorShift32(key);
            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)(block[i] ^ rng.Next());
            return block;
        }
        private void StopConditionsButton_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new(setcolors, ref encounterFilters, filtermode);
            miniform.ShowDialog();
        }
        public void DisableOptions()
        {
            checkBox5.Enabled = false;
            NonSave.Enabled = false;
            button1.Enabled = false;
            XCoord.Enabled = false;
            YCoord.Enabled = false;
            ZCoord.Enabled = false;
            ReadCoordButton.Enabled = false;
            TeleportCoordButton.Enabled = false;
            TeleportButton.Enabled = false;
            TeleportIndex.Enabled = false;
            CoordCheck.Enabled = false;
            CoordChangeButton.Enabled = false;
            PokemonCombo.Enabled = false;
            if (FormCombo.Visible)
                FormCombo.Enabled = false;
            CoordNum.Enabled = false;
            DelayNum.Enabled = false;
        }

        public void EnableOptions()
        {
            checkBox5.Enabled = true;
            NonSave.Enabled = true;
            button1.Enabled = true;
            XCoord.Enabled = true;
            YCoord.Enabled = true;
            ZCoord.Enabled = true;
            Language.Enabled = true;
            ReadCoordButton.Enabled = true;
            TeleportCoordButton.Enabled = true;
            CoordCheck.Enabled = true;
            CoordChangeButton.Enabled = true;
            PokemonCombo.Enabled = true;
            if (FormCombo.Visible)
                FormCombo.Enabled = true;
            TeleportButton.Enabled = coordList.Count > 0;
            TeleportIndex.Enabled = coordList.Count > 0;
            CoordNum.Enabled = true;
            DelayNum.Enabled = true;
        }
    }
}
