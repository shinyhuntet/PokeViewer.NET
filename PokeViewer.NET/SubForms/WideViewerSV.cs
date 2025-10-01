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
using System.Net.Sockets;
using System.Buffers;
using PokeViewer.NET.Misc;
using Color = System.Drawing.Color;
using Times = PokeViewer.NET.Misc.Pokemon_Enums.Times;
using TimeBase = PokeViewer.NET.Misc.Pokemon_Enums.TimeBase;
using TimesReroll = PokeViewer.NET.Misc.Pokemon_Enums.TimesReroll;
using Newtonsoft.Json;
using PokeViewer.NET.Properties;
using PokeViewer.NET.Util;
using NLog.Time;
using System.Diagnostics.Eventing.Reader;

namespace PokeViewer.NET.SubForms
{
    public partial class WideViewerSV : Form
    {
        private readonly ViewerState Executor;
        protected ViewerOffsets Offsets { get; } = new();
        private static ulong BaseBlockKeyPointer = 0;
        public ulong PlayerOnMountOffset = 0;
        public ulong TeraRaidBlockOffset = 0;
        public ulong PlayerCanMoveOffset = 0;
        private ulong OverWorldOffset = 0;
        private readonly Egg_Viewer eggviewer;
        private readonly ItemStructure itemStructure;
        private int PreType = -1;
        private int PreMode = -1;
        string coordx = string.Empty;
        string coordy = string.Empty;
        string coordz = string.Empty;
        public List<OutbreakStash> OutbreakCache = new();
        public List<OutbreakStash> BCATOutbreakCache = new();
        public ulong CountCacheP;
        public ulong CountCacheK;
        public ulong CountCacheB;
        public ulong CountCacheBCP;
        public ulong CountCacheBCK;
        public ulong CountCacheBCB;
        public ulong CacheField;
        public ulong CacheWeather;
        private List<byte[]?> OutbreakCoords = new();
        DateTime time = DateTime.Now;
        DateTime set_time = DateTime.Now + TimeSpan.FromMinutes(34);
        TimeSpan timediff;
        TimeSpan delay = TimeSpan.FromMinutes(5.5);
        private CancellationTokenSource? cts = null;
        private GameStrings Strings;
        private (Color, Color) setcolors;
        private FilterMode filtermode;
        private List<EncounterFilter> encounterFilters = new();
        private int[] IVFiltersMax = Array.Empty<int>();
        private int[] IVFiltersMin = Array.Empty<int>();
        private List<byte[]?> coordList = new();
        private (Color, Color) colors;
        private bool canceled = false;
        private int FleeFailCount = 0;
        private int ReConnectCount = 0;
        private List<string> SpeciesList = null!;
        private string[] FormsList = null!;
        private string[] TypesList = null!;
        private readonly string[] GenderList = null!;
        private readonly List<int> TypeIngredients = [1923, 1913, 1929, 1933, 1921, 1941, 1911, 1915, 1925, 1946, 1912, 1909, 1918, 1914, 1926, 1919, 1927, 1910];
        private readonly Dictionary<int, Dictionary<MoveType, List<(int, int)>>> AllMysticaSalt = [];
        private readonly Dictionary<MoveType, List<(int, int)>> MysticaSalt = [];
        private readonly Dictionary<MoveType, List<(int, int)>> MysticaSalt_Mark_SizeMax = [];
        private readonly Dictionary<MoveType, List<(int, int)>> MysticaSalt_Mark_SizeMin = [];
        private Dictionary<ushort, Dictionary<byte, int>> SpecFormsList = [];
        private Dictionary<string, List<RibbonIndex>> TargetWeatherMarkFilters = [];
        private List<Times> TargetTimesList = [];
        private long SetTime = -1;
        private DateTime SetDateTime = DateTime.UtcNow;
        private DateTime EnrollmentDate = DateTime.UtcNow;
        private long DateCycleTime = -1;
        private long DateCycleTimeFix = -1;
        private Times BaseTimes = Times.All_Times;
        private TeraRaidMapParent MapParent = TeraRaidMapParent.Paldea;
        private ulong seed = 0;
        private ushort TargetMon = 0;
        private byte TargetForm = 0;
        private readonly long DateCycle = (long)TimeSpan.FromMinutes(72).TotalSeconds;
        private readonly SimpleTrainerInfo TrainerInfo;
        private bool CollideTaskComplete = false;
        private bool OverworldTaskComplete = false;
        private Task ScanTask = Task.Delay(1_000);
        private Task OverworldTask = Task.Delay(1_000);
        private AutoResetEvent ScanEvent = new(false);
        private AutoResetEvent OverworldEvent = new(false);

        public void LoadOutbreakCache()
        {
            for (int i = 0; i < 18; i++)
                OutbreakCache.Add(new());
        }
        public void LoadBCATOutbreakCache()
        {
            for (int i = 0; i < 30; i++)
                BCATOutbreakCache.Add(new());
        }

        public WideViewerSV(int Gametype, ref ViewerState executor, (Color, Color) color, SimpleTrainerInfo trainerInfo)
        {
            InitializeComponent();
            Executor = executor;
            filtermode = FilterMode.Wide;
            var filterpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{filtermode}filters.json");
            if (File.Exists(filterpath))
                encounterFilters = System.Text.Json.JsonSerializer.Deserialize<List<EncounterFilter>>(File.ReadAllText(filterpath)) ?? new List<EncounterFilter>();
            eggviewer = new Egg_Viewer(Gametype, ref executor, color, trainerInfo);
            TrainerInfo = trainerInfo;
            itemStructure = new(executor);
            Language.DataSource = Enum.GetValues(typeof(LanguageID)).Cast<LanguageID>().Where(z => z != LanguageID.None && z != LanguageID.UNUSED_6).ToArray();
            Language.SelectedIndex = 1;
            LastDateCombo.DataSource = Enum.GetValues(typeof(Times)).Cast<Times>().Where(z => z != Times.All_Times && z != Times.Morning_to_Evening).ToArray();
            LastDateCombo.SelectedIndex = 1;
            TimeZoneCombo.DataSource = TimeZoneInfo.GetSystemTimeZones();
            TimeZoneCombo.DisplayMember = "DisplayName";
            TimeZoneCombo.ValueMember = "Id";
            Strings = GameInfo.GetStrings(Language.SelectedIndex);
            SpeciesList = Strings.specieslist.ToList();
            OutbreakSpeciesBox.DataSource = Strings.specieslist.Where(z => PersonalTable.SV.IsSpeciesInGame((ushort)SpeciesList.IndexOf(z))).ToArray();
            poketype.DataSource = Strings.types;
            FormsList = Strings.forms;
            TypesList = Strings.types;
            GenderList = [.. GameInfo.GenderSymbolUnicode];
            LoadOutbreakCache();
            LoadBCATOutbreakCache();
            GetMysticaSalt();
            SetColors(color);
            colors = color;

        }
        private void GetMysticaSalt()
        {
            AllMysticaSalt.Clear();
            MysticaSalt.Clear();
            MysticaSalt_Mark_SizeMax.Clear();
            MysticaSalt_Mark_SizeMin.Clear();
            MysticaSalt.Add(MoveType.Any, [(1908, 1), (1905, 1)]);
            MysticaSalt.Add(MoveType.Psychic, [(1905, 2)]);
            MysticaSalt.Add(MoveType.Rock, [(1905, 2)]);
            MysticaSalt_Mark_SizeMax.Add(MoveType.Any, [(1908, 2)]);
            MysticaSalt_Mark_SizeMax.Add(MoveType.Ice, [(1896, 1), (1907, 1), (1908, 1)]);
            MysticaSalt_Mark_SizeMax.Add(MoveType.Ghost, [(1896, 1), (1907, 1), (1908, 1)]);
            MysticaSalt_Mark_SizeMax.Add(MoveType.Dragon, [(1896, 1), (1907, 1), (1908, 1)]);
            MysticaSalt_Mark_SizeMax.Add(MoveType.Psychic, [(1906, 1), (1908, 1)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Any, [(1906, 2)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Ice, [(1896, 1), (1906, 2)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Bug, [(1896, 1), (1906, 2)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Ghost, [(1896, 1), (1906, 2)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Dragon, [(1896, 1), (1906, 2)]);
            MysticaSalt_Mark_SizeMin.Add(MoveType.Fairy, [(1905, 1), (1906, 2)]);
            AllMysticaSalt.Add(0, MysticaSalt);
            AllMysticaSalt.Add(1, MysticaSalt_Mark_SizeMax);
            AllMysticaSalt.Add(2, MysticaSalt_Mark_SizeMin);
        }
        private void SetColors((Color, Color) color)
        {
            BackColor = color.Item1;
            ForeColor = color.Item2;
            ScanButton.BackColor = color.Item1;
            ScanButton.ForeColor = color.Item2;
            HardStopButton.BackColor = color.Item1;
            HardStopButton.ForeColor = color.Item2;
            OutbreakResetButton.BackColor = color.Item1;
            OutbreakResetButton.ForeColor = color.Item2;
            StopConditions.BackColor = color.Item1;
            StopConditions.ForeColor = color.Item2;

            TextBox[] textboxes =
            {
                textBox1, textBox2, textBox3, textBox4, textBox5, textBox6, textBox7, textBox8, textBox9, textBox10, textBox11, textBox12, textBox13, textBox14, textBox15, textBox16, textBox17, textBox18, textBox19, textBox20
            };

            PictureBox[] boxes =
            {
                pictureBox1, pictureBox2, pictureBox3, pictureBox4, pictureBox5, pictureBox6, pictureBox7, pictureBox8, pictureBox9, pictureBox10, pictureBox11, pictureBox12, pictureBox13, pictureBox14, pictureBox15,
                pictureBox16, pictureBox17, pictureBox18, pictureBox19, pictureBox20, pictureBox21, pictureBox22, pictureBox23, pictureBox24, pictureBox25, pictureBox26, pictureBox27, pictureBox28, pictureBox29, pictureBox30,
                pictureBox31, pictureBox32, pictureBox33, pictureBox34, pictureBox35, pictureBox36, pictureBox37, pictureBox38, pictureBox39, pictureBox40
            };

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].BackColor = color.Item1;
            }

            for (int i = 0; i < textboxes.Length; i++)
            {
                textboxes[i].BackColor = color.Item1;
            }
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

                if (filter.Shiny is 3 && pk.ShinyXor == 0) // StarOnly
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
        private async Task GetTaskStatus(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    //ScanEvent.Reset();
                    ScanEvent.Set();
                    //OverworldEvent.Reset();
                    OverworldEvent.Set();
                    break;
                }
                if (CollideTaskComplete)
                    break;
                await Task.Delay(0_100, CancellationToken.None).ConfigureAwait(false);
            }
        }
        private bool GetCheckState(CheckBox checkBox)
        {
            if (InvokeRequired)
            {
                return Invoke(() => checkBox.Checked);
            }
            else
                return checkBox.Checked;
        }
        private bool GetTextStatus(TextBox textBox)
        {
            if (InvokeRequired)
            {
                return Invoke(() => !string.IsNullOrEmpty(textBox.Text));
            }
            else
                return !string.IsNullOrEmpty(textBox.Text);
        }
        private async void button1_Click(object sender, EventArgs e) => await AutoScan().ConfigureAwait(false);
        private async Task AutoScan(bool ReConnect = false)
        {
            var success = true;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                if (!ReConnect)
                {
                    seed = 0;
                    SetTime = -1;
                    if (encounterFilters.Count == 0 || encounterFilters.All(x => !x.Enabled))
                    {
                        MessageBox.Show("No Filter Active!");
                        return;
                    }
                    TargetWeatherMarkFilters = [];
                    TargetTimesList = [];
                    foreach (var filter in encounterFilters)
                    {
                        var (istargettime, name, marks) = filter.IsTimeMarkTarget();
                        if (istargettime && name is not null && marks is not null)
                        {
                            TargetTimesList = TargetTimesList.Concat(marks.Select(x => Pokemon_Enums.ConvertMarkToTimes(x)).ToList()).ToList();
                        }
                        var (istargetweather, wname, wmarks) = filter.IsWeatherMarkTarget();
                        if (istargetweather && wname is not null && wmarks is not null)
                            TargetWeatherMarkFilters.Add(wname, wmarks);
                    }
                    TargetTimesList = BuildTargetList(TargetTimesList);
                    if (TimeZoneCombo.SelectedIndex < 0 || TimeZoneCombo.SelectedValue == null)
                    {
                        MessageBox.Show("Invalid TimeZone!");
                        return;
                    }
                    OutbreakSpeciesBox.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                    OutbreakText.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                    if (FormCombo.Visible)
                        FormCombo.Enabled = OutbreakSpeciesBox.Enabled;
                    DisableOptions();
                }
                if (InvokeRequired)
                    Invoke(() => ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected}");
                else
                    ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected}";
                canceled = false;
                try
                {
                    await InitilizeSessionOffsets(token);
                    if (checkBox6.Checked && !ReConnect)
                    {
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () => 
                        {
                            await CollideRead(token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                    }
                    if (!ReConnect && GetCheckState(MapCheck))
                    {
                        if (GetCheckState(TeleportMode))
                        {
                            if (string.IsNullOrEmpty(coordx) || string.IsNullOrEmpty(coordy) || string.IsNullOrEmpty(coordz))
                            {
                                MessageBox.Show("Telport location is empty! Go to Teleport location.");
                                ResetTaskStatus();
                                ScanTask = Task.Run(async () => 
                                { 
                                    await CollideReadPro(token).ConfigureAwait(false); 
                                    CollideTaskComplete = true; 
                                    while (!OverworldTaskComplete)
                                        await Task.Delay(0_010, token).ConfigureAwait(false);
                                });
                                OverworldTask = RecoverToOverworld(token);
                                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                    throw new OperationCanceledException();
                            }
                            else
                            {
                                ResetTaskStatus();
                                ScanTask = Task.Run(async () => 
                                { 
                                    await CollideToSpot(coordx, coordy, coordz, token).ConfigureAwait(false); 
                                    CollideTaskComplete = true; 
                                    while (!OverworldTaskComplete)
                                        await Task.Delay(0_010, token).ConfigureAwait(false);
                                });
                                OverworldTask =  RecoverToOverworld(token);
                                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                    throw new OperationCanceledException();
                            }
                        }
                        await SetUpMap(token).ConfigureAwait(false);
                        if (InvokeRequired)
                            Invoke(() => MapCheck.Checked = false);
                        else
                            MapCheck.Checked = false;
                    }
                    if (GetCheckState(checkBox7) && GetTextStatus(XCoord) && GetTextStatus(YCoord) && GetTextStatus(ZCoord))
                    {
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () =>
                        {
                            await CollideToCave(false, token).ConfigureAwait(false);
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                    }
                    if (InvokeRequired)
                    {
                        await Invoke(async () =>
                        {
                            if (OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0)
                            {
                                var waiting = 0;
                                var enable = false;
                                if (OutbreakSpeciesBox.SelectedIndex < 0)
                                {
                                    EnableOptions();
                                    enable = true;
                                }
                                while (OutbreakSpeciesBox.SelectedIndex < 0)
                                {
                                    await Task.Delay(2_000, token).ConfigureAwait(false);
                                    waiting++;
                                    if (waiting >= 30)
                                    {
                                        OutbreakType.SelectedIndex = 0;
                                        TimeCombo.SelectedIndex = 0;
                                        break;
                                    }
                                }
                                if (enable)
                                    DisableOptions();
                            }
                            if (Reset.Checked)
                            {
                                CountCacheP = 0;
                                CountCacheK = 0;
                                CountCacheB = 0;
                                CountCacheBCP = 0;
                                CountCacheBCK = 0;
                                CountCacheBCB = 0;
                                CacheField = 0;
                                OutbreakCache = new();
                                BCATOutbreakCache = new();
                                LoadOutbreakCache();
                                LoadBCATOutbreakCache();
                            }
                            if (!NonSaveMode.Checked && ShinyBoost.Checked && await itemStructure.HasShinyCharm(token).ConfigureAwait(false))
                                ShinyBoost.Checked = false;
                        });
                    }
                    else
                    {
                        if (OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0)
                        {
                            var waiting = 0;
                            var enable = false;
                            if (OutbreakSpeciesBox.SelectedIndex < 0)
                            {
                                EnableOptions();
                                enable = true;
                            }
                            while (OutbreakSpeciesBox.SelectedIndex < 0)
                            {
                                await Task.Delay(2_000, token).ConfigureAwait(false);
                                waiting++;
                                if (waiting >= 30)
                                {
                                    OutbreakType.SelectedIndex = 0;
                                    TimeCombo.SelectedIndex = 0;
                                    break;
                                }
                            }
                            if (enable)
                                DisableOptions();
                        }
                        if (Reset.Checked)
                        {
                            CountCacheP = 0;
                            CountCacheK = 0;
                            CountCacheB = 0;
                            CountCacheBCP = 0;
                            CountCacheBCK = 0;
                            CountCacheBCB = 0;
                            CacheField = 0;
                            OutbreakCache = new();
                            BCATOutbreakCache = new();
                            LoadOutbreakCache();
                            LoadBCATOutbreakCache();
                        }
                        if (!GetCheckState(NonSaveMode) && GetCheckState(ShinyBoost) && await itemStructure.HasShinyCharm(token).ConfigureAwait(false))
                            Invoke(() => ShinyBoost.Checked = false);
                    }
                    await BoostShinyRolls(token).ConfigureAwait(false);
                    await ScanOverworld(token, ReConnect).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    if (InvokeRequired)
                        Invoke(() => MessageBox.Show(this, "Process has been canceled!", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Information));
                    else
                        MessageBox.Show(this, "Process has been canceled!", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    EnableOptions();
                }
                catch (Exception ex)
                {
                    LogUtil.LogText(ex.ToString());
                    if (InvokeRequired)
                        Invoke(() => ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected} ");
                    else
                        ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected} ";
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            if (InvokeRequired)
                            {
                                Invoke(() =>
                                {
                                    AutoReConnet.Checked = false;
                                    MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                                });
                            }
                            else
                            {
                                AutoReConnet.Checked = false;
                                MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                            }
                        }
                    }
                    if (InvokeRequired)
                        Invoke(() => ConnectionBox.Text += $"{Environment.NewLine}Switch Connection Connected(Updated): {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected(Updated): {Executor.Connection.Connected} ");
                    else
                        ConnectionBox.Text += $"{Environment.NewLine}Switch Connection Connected(Updated): {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected(Updated): {Executor.Connection.Connected} ";
                    if (GetCheckState(AutoReConnet))
                    {
                        try
                        {
                            await Task.Delay(60_000, token).ConfigureAwait(false);
                            Executor.SwitchConnection.Reset();
                            if (!Executor.SwitchConnection.Connected)
                                throw new Exception("SwitchConnection can't reconnect!");
                            await CloseGame(token).ConfigureAwait(false);
                            if (SetTime > 0)
                            {
                                await Task.Delay(1_500, token).ConfigureAwait(false);
                                await SetCurrentTime((ulong)(SetTime - 120), token).ConfigureAwait(false);
                                await Task.Delay(1_500, token).ConfigureAwait(false);
                            }
                            await StartGame(token).ConfigureAwait(false);
                            if (SetTime > 0)
                                await Task.Delay(5_000, token).ConfigureAwait(false);
                            Invoke(new Action(() =>
                            {
                                Reset.Checked = true;
                                EatOnStart.Checked = true;
                            }));
                        }
                        catch (SocketException err)
                        {
                            Invoke(() => MessageBox.Show(this, err.ToString(), "ReConnect Sokcket Exception!"));
                            EnableOptions();
                            return;
                        }
                        catch (Exception RecEx)
                        {
                            Invoke(() => MessageBox.Show(this, RecEx.ToString(), "ReConnect Exception except for Socket!"));
                            EnableOptions();
                            return;
                        }
                        await AutoScan(true).ConfigureAwait(false);
                        return;
                    }
                    EnableOptions();
                    success = false;
                    Invoke(() => MessageBox.Show(this, ex.ToString(), "Exception!"));
                }
            }

            canceled = true;
            if (Executor.SwitchConnection.Connected)
                await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
            if (!cts.IsCancellationRequested && cts != null && success)
            {
                if (InvokeRequired)
                    Invoke(() => MessageBox.Show(this, "Process has been finished!", "Finish", MessageBoxButtons.OK, MessageBoxIcon.Information));
                else
                    MessageBox.Show(this, "Process has been finished!", "Finish", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private async Task TimeAdjust(CancellationToken token)
        {
            RefreshConnection();

            TeraRaidBlockOffset = await Executor.SwitchConnection.PointerAll(Offsets.RaidBlockPointerP, token).ConfigureAwait(false);
            var seed = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
            var seed_new = seed;
            DateTime date = DateTime.UtcNow;
            while (seed == seed_new)
            {
                if (InvokeRequired)
                    Invoke(() => OutbreakText.Text = $"{seed:X}");
                else
                    OutbreakText.Text = $"{seed:X}";
                await Task.Delay(5_000, token).ConfigureAwait(false);
                await CloseGame(token).ConfigureAwait(false);
                await Task.Delay(1_500, token).ConfigureAwait(false);
                await DayBackFaster((int)DateReset.Value, 0_400, token).ConfigureAwait(false);
                await StartGame(token).ConfigureAwait(false);
                await Task.Delay(8_000, token).ConfigureAwait(false);
                long curTime = await ResetNTPTime(token).ConfigureAwait(false);
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                date = epoch.AddSeconds(curTime).ToLocalTime();
                if (InvokeRequired)
                    Invoke(() => TimeText.Text = $"{date:yyyy/MM/dd HH:mm:ss}");
                else
                    TimeText.Text = $"{date:yyyy/MM/dd HH:mm:ss}";
                await Task.Delay(3_000, token).ConfigureAwait(false);
                await InitilizeSessionOffsets(token);
                seed_new = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
            }
            if (InvokeRequired)
            {
                await InvokeAsync(async() =>
                {
                    Reset.Checked = true;
                    if ((TeleportMode.Checked || ScanLocationCannotPicnic.Checked) && await PlayerNotOnMount(token).ConfigureAwait(false))
                        await Click(PLUS, 1_000, token).ConfigureAwait(false);
                    OutbreakText.Text = $"{seed_new:X}";
                });
            }
            else
            {
                Reset.Checked = true;
                if ((TeleportMode.Checked || ScanLocationCannotPicnic.Checked) && await PlayerNotOnMount(token).ConfigureAwait(false))
                    await Click(PLUS, 1_000, token).ConfigureAwait(false);
                OutbreakText.Text = $"{seed_new:X}";
            }
            if (coordList.Count <= 0)
                MessageBox.Show($"OutbreakReset success! Previous Dayseed {seed:X}, Current Dayseed {seed_new:X}{Environment.NewLine}Current Time: {date:yyyy/MM/dd HH:mm:ss}");
        }
        private async Task TimeAdjustToPreviousTime(ulong OriginalSeed, ulong setTime, CancellationToken token)
        {
            setTime = setTime - 120;
            await ResetToPreviousTime(setTime, token).ConfigureAwait(false);
            var CurrentSeed = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
            while (CurrentSeed != OriginalSeed)
            {
                if (InvokeRequired)
                    Invoke(() => OutbreakText.Text = $"{CurrentSeed:X}");
                else
                    OutbreakText.Text = $"{CurrentSeed:X}";
                await ResetToPreviousTime(setTime, token).ConfigureAwait(false);
                CurrentSeed = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
            }
        }
        private async Task ResetToPreviousTime(ulong setTime, CancellationToken token)
        {
            await Task.Delay(5_000, token).ConfigureAwait(false);
            await CloseGame(token).ConfigureAwait(false);
            await Task.Delay(1_500, token).ConfigureAwait(false);
            await SetCurrentTime(setTime, token).ConfigureAwait(false);
            await Task.Delay(1_500, token).ConfigureAwait(false);
            await StartGame(token).ConfigureAwait(false);
            await Task.Delay(5_000, token).ConfigureAwait(false);
            await InitilizeSessionOffsets(token);
        }
        private async void button3_Click(object sender, EventArgs e)
        {
            var success = true;
            canceled = false;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    DisableOptions();
                    ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected}";
                    await TimeAdjust(token).ConfigureAwait(false);
                    if (coordList.Count > 0)
                    {
                        bool Targetcoordvaild = TargetCoordCheck();
                        if (Targetcoordvaild)
                            await TeleportToMatch(coordList[(int)TeleportIndex.Value], token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    if (InvokeRequired)
                        Invoke(() => MessageBox.Show(this, "Process has been canceled!", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Information));
                    else
                        MessageBox.Show(this, "Process has been canceled!", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Invoke(() => ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected} ");
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
                    Invoke(() => ConnectionBox.Text += $"{Environment.NewLine}Switch Connection Connected(Updated): {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected(Updated): {Executor.Connection.Connected} ");
                    success= false;
                    MessageBox.Show(ex.ToString());
                }
                canceled = true;
                EnableOptions();
                if (Executor.SwitchConnection.Connected)
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                if (!cts.IsCancellationRequested && cts != null && success && coordList.Count <= 0)
                    Invoke(() => MessageBox.Show(this, "Process has been finished!", "Finish", MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
        }
        private async void SetTimeButton_Click(object sender, EventArgs e)
        {
            SetTimeButton.Visible = false;
            OutbreakResetButton.Visible = true;
            DisableOptions();
            ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected}";
            var success = true;
            canceled = false;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    var NTPTime = await ResetNTPTime(token).ConfigureAwait(false);
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime date = epoch.AddSeconds(NTPTime).ToLocalTime();
                    TimeText.Text = $"{date:yyyy/MM/dd HH:mm:ss}";

                }
                catch (OperationCanceledException)
                {
                    success = false;
                    MessageBox.Show("Setting time is canceled.", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
                catch (Exception ex)
                {
                    Invoke(() => ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected} ");
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
                    Invoke(() => ConnectionBox.Text += $"{Environment.NewLine}Switch Connection Connected(Updated): {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected(Updated): {Executor.Connection.Connected} ");
                    success = false;
                    MessageBox.Show(this, ex.ToString());
                }
                canceled = true;
                EnableOptions();
                if (Executor.SwitchConnection.Connected)
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                if (success)
                    MessageBox.Show("Settimg time is completed successfully!", "Switch NTP Server Clock");
            }
        }
        private void OutbreakCheck_CheckedChanged(object sender, EventArgs e)
        {
            OutbreakSpeciesBox.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
            OutbreakText.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
            if (!OutbreakSpeciesBox.Enabled)
                OutbreakSpeciesBox.SelectedIndex = -1;
            if (FormCombo.Visible)
                FormCombo.Enabled = OutbreakSpeciesBox.Enabled;
        }

        public async Task SVSaveGameOverworld(CancellationToken token)
        {
            if (await IsOnOverworldTitle(token).ConfigureAwait(false))
                await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(R, 1_000, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
        }
        private async Task ToOverworldScreen(CancellationToken token)
        {
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                await Click(B, 0_800, token).ConfigureAwait(false);
        }
        private async Task<bool> PlayerNotOnMount(CancellationToken token)
        {
            RefreshConnection();

            while (PlayerOnMountOffset == 0)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            }
            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(PlayerOnMountOffset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }
        private async Task RefreshOnMountOffset(CancellationToken token)
        {
            RefreshConnection();
            PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            while (PlayerOnMountOffset == 0)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            }
        }

        private void ResetTaskStatus()
        {
            CollideTaskComplete = false;
            OverworldTaskComplete = false;
            FleeFailCount = 0;
            OverworldEvent = new(false);
            ScanEvent = new(false);
        }
        private async Task CollideReposite(CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            await Reposition(token).ConfigureAwait(false);
        }
        private async Task DisCollideReposite(CancellationToken token)
        {
            await DisCollideOnly(token).ConfigureAwait(false);
            await Reposition(token).ConfigureAwait(false);
            CollideTaskComplete = true;
            while (!OverworldTaskComplete)
                await Task.Delay(0_010, token).ConfigureAwait(false);
        }
        private async Task CollideRead(CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            ScanEvent.WaitOne();
            var coords = await Executor.SwitchConnection.PointerPeek(12, Offsets.CollisionPointer, token).ConfigureAwait(false);
            Invoke(() =>
            {
                XCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[..4])}";
                YCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[4..8])}";
                ZCoord.Text = $"{BitConverter.ToSingle(coords.AsSpan()[8..12])}";
            });
            OverworldEvent.Set();
            if (Invoke(() => PicnicReset.Checked))
            {
                ScanEvent.WaitOne();
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
                OverworldEvent.Set();
            }
            if (InvokeRequired)
                Invoke(() => checkBox6.Checked = false);
            else
            {
                if (checkBox6.Checked)
                    checkBox6.Checked =false;
            }
        }

        private async Task Collide(CancellationToken token)
        {
            var CheckCount = 0;
            if (await PlayerNotOnMount(token).ConfigureAwait(false))
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
            while (await PlayerNotOnMount(token).ConfigureAwait(false))
            {
                OverworldEvent.Set();
                CheckCount++;
                ScanEvent.WaitOne();
                if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
                OverworldEvent.Set();
                ScanEvent.WaitOne();
                if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                
                if (CheckCount >= 1)
                {
                    OverworldEvent.Set();
                    ScanEvent.WaitOne();
                    await RefreshOnMountOffset(token).ConfigureAwait(false);
                    OverworldEvent.Set();
                }
                if (CheckCount >= 2 && CheckCount < 4)
                {
                    ScanEvent.WaitOne();
                    await ClosePicnic(token).ConfigureAwait(false);                    
                }
                if (CheckCount >= 4 && !await OverworldStateChanged(token).ConfigureAwait(false))
                {
                    await RemoveEmotes(token).ConfigureAwait(false);
                    OverworldEvent.Set();
                    CheckCount = 0;
                }
                ScanEvent.WaitOne();
            }
            OverworldEvent.Set();
        }


        private async Task RecoverToOverworld(CancellationToken token)
        {

            OverworldEvent.WaitOne();

            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();
            //int B_Count = 0;
            while (!token.IsCancellationRequested)
            {

                while (!await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                {
                    if (await IsInBattle(token).ConfigureAwait(false))
                    {
                        FleeFailCount++;
                        if (FleeFailCount >= 5 || TeleportMode.Checked)
                            await DefeatPokemon(token).ConfigureAwait(false);
                        else
                            await FastFlee(token).ConfigureAwait(false);
                    }
                    else
                    {
                        await Click(B, 0_600, token).ConfigureAwait(false);
                        //B_Count++;
                        /*if (B_Count >= 5)
                            break;*/
                    }
                    await RefreshOverworldOffset(token).ConfigureAwait(false);
                }
                //B_Count = 0;
                if (CollideTaskComplete)
                {
                    OverworldTaskComplete = true;
                    return;
                }
                else
                {
                    ScanEvent.Set();
                    OverworldEvent.WaitOne();
                }
            }
        }

        private async Task RemoveEmotes(CancellationToken token)
        {
            await SetStick(SwitchStick.LEFT, 0, 10000, 0_050, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 0, token).ConfigureAwait(false);
        }
        private async Task<bool> OverworldStateChanged(CancellationToken token)
        {
            ScanEvent.WaitOne();
            var state = await GetOverworldState(OverWorldOffset, token).ConfigureAwait(false);
            await Click(X, 2_000, token).ConfigureAwait(false);
            return state != await GetOverworldState(OverWorldOffset, token).ConfigureAwait(false);
        }
        private async Task ToOverworld(CancellationToken token)
        {
            //int B_Count = 0;
            while (!await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
            {
                await Click(B, 0_500, token).ConfigureAwait(false);
                //B_Count++;
                await RefreshOverworldOffset(token).ConfigureAwait(false);
                /*if (B_Count >= 5)
                    break;*/                
            }
        }
        private async Task DisCollide(CancellationToken token)
        {
            if (!GetCheckState(InWater))
            {
                if (!await PlayerNotOnMount(token).ConfigureAwait(false))                
                    await Click(PLUS, 0_800, token).ConfigureAwait(false);                                    
                var CheckCount = 0;
                while (!await PlayerNotOnMount(token).ConfigureAwait(false))
                {
                    OverworldEvent.Set();
                    ScanEvent.WaitOne();
                    if (await PlayerNotOnMount(token).ConfigureAwait(false))
                        break;
                    await Click(PLUS, 0_800, token).ConfigureAwait(false);
                    if (await PlayerNotOnMount(token).ConfigureAwait(false))
                        break;
                    OverworldEvent.Set();
                    CheckCount++;
                    ScanEvent.WaitOne();

                    if (CheckCount >= 2)
                    {
                        await RefreshOnMountOffset(token).ConfigureAwait(false);                                               
                    }
                    OverworldEvent.Set();
                    ScanEvent.WaitOne();
                }
                OverworldEvent.Set();
            }
            await Reposition(token).ConfigureAwait(false);
        }
        private async Task DisCollideOnly(CancellationToken token)
        {
            if (!await PlayerNotOnMount(token).ConfigureAwait(false))            
                await Click(PLUS, 0_800, token).ConfigureAwait(false);            
            var CheckCount = 0;
            while (!await PlayerNotOnMount(token).ConfigureAwait(false))
            {
                OverworldEvent.Set();
                ScanEvent.WaitOne();
                if (await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                await Click(PLUS, 0_800, token).ConfigureAwait(false);
                if (await PlayerNotOnMount(token).ConfigureAwait(false))
                    break;
                OverworldEvent.Set();
                CheckCount++;
                ScanEvent.WaitOne();
                
                if (CheckCount >= 1)
                {
                    await RefreshOnMountOffset(token).ConfigureAwait(false);                                       
                }
                if (CheckCount >= 3)
                    break;
                OverworldEvent.Set();
                ScanEvent.WaitOne();
            }
            OverworldEvent.Set();
        }
        private async Task CollideReadPro(CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            ScanEvent.WaitOne();
            var coords = await Executor.SwitchConnection.PointerPeek(12, Offsets.CollisionPointer, token).ConfigureAwait(false);
            coordx = $"{BitConverter.ToSingle(coords.AsSpan()[..4])}";
            coordy = $"{BitConverter.ToSingle(coords.AsSpan()[4..8])}";
            coordz = $"{BitConverter.ToSingle(coords.AsSpan()[8..12])}";
            OverworldEvent.Set();
        }
        private (string, string, string) CoordReader(byte[]? coords)
        {
            string x = $"{BitConverter.ToSingle(coords.AsSpan()[..4])}";
            string y = $"{BitConverter.ToSingle(coords.AsSpan()[4..8])}";
            string z = $"{BitConverter.ToSingle(coords.AsSpan()[8..12])}";

            return (x, y, z);
        }
        private async Task CollideToCave(bool outbreakmode, CancellationToken token)
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

            ScanEvent.WaitOne();
            for (int i = 0; i < 15; i++)
                await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
            OverworldEvent.Set();
            ScanEvent.WaitOne();
            
            if (outbreakmode)
            {
                await Task.Delay(2_500).ConfigureAwait(false);
                OverworldEvent.Set();
                ScanEvent.WaitOne();
                await Click(B, 19_000, token).ConfigureAwait(false);                         
            }

            await Task.Delay(5_000, token).ConfigureAwait(false);
            OverworldEvent.Set();
            ScanEvent.WaitOne();
            await DisCollide(token).ConfigureAwait(false);
            Invoke(() =>
            {
                if (checkBox7.Checked)
                    checkBox7.Checked = false;
            });
        }
        private async Task TeleportToMatch(byte[]? cp, CancellationToken token)
        {
            await Collide(token).ConfigureAwait(false);
            /*float Y = BitConverter.ToSingle(cp!, 4);
            Y += 20;
            WriteSingleLittleEndian(cp.AsSpan()[4..], Y);*/

            for (int i = 0; i < 15; i++)
                await Executor.SwitchConnection.PointerPoke(cp!, Offsets.CollisionPointer, token).ConfigureAwait(false);

            await Task.Delay(2_000, token).ConfigureAwait(false);
        }
        private async Task<(Single, Single, Single)> PlayerCoordRead(CancellationToken token)
        {
            var coords = await Executor.SwitchConnection.PointerPeek(12, Offsets.CollisionPointer, token).ConfigureAwait(false);            
            Single x = BitConverter.ToSingle(coords.AsSpan()[..4]);
            Single y = BitConverter.ToSingle(coords.AsSpan()[4..8]);
            Single z = BitConverter.ToSingle(coords.AsSpan()[8..12]);
            OverworldEvent.Set();
            return (x, y, z);
        }
        private async Task PrepareToPicnic(string x, string Y, string z, CancellationToken token)
        {
            float coordx = Single.Parse(x, NumberStyles.Float);
            float coordy = Single.Parse(Y, NumberStyles.Float);
            float coordz = Single.Parse(z, NumberStyles.Float);
            var TeleportCoords = (coordx, coordy, coordz).ToTuple();

            var PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
            ScanEvent.WaitOne();
            bool Teleport = false;
            if (InvokeRequired)
                Invoke(() => Teleport = TeleportMode.Checked);
            else
                Teleport = TeleportMode.Checked;
            if (Math.Abs(TeleportCoords.Item1 - PlayerCoords.Item1) > (Teleport ? 5 : 0.5) || Math.Abs(TeleportCoords.Item2 - PlayerCoords.Item2) > (Teleport ? 5 : 0.5) || Math.Abs(TeleportCoords.Item3 - PlayerCoords.Item3) > (Teleport ? 5 : 0.5))
            {
                await RefreshOnMountOffset(token).ConfigureAwait(false);
                await CollideToSpot(x, Y, z, token).ConfigureAwait(false);
                ScanEvent.WaitOne();
                await DisCollideOnly(token).ConfigureAwait(false);
                await Reposition(token).ConfigureAwait(false);
                ScanEvent.WaitOne();
                await PrepareToPicnic(x, Y, z, token).ConfigureAwait(false);
                return;
            }
            OverworldEvent.Set();
            CollideTaskComplete = true;
            while (!OverworldTaskComplete)
                await Task.Delay(0_010, token).ConfigureAwait(false);
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
                var TeleportCoords = (coordx, coordy + 20, coordz).ToTuple();

                X1 = X1.Concat(Y1).Concat(Z1).ToArray();
                float y = BitConverter.ToSingle(X1, 4);
                y += 20;
                WriteSingleLittleEndian(X1.AsSpan()[4..], y);

                ScanEvent.WaitOne();
                for (int i = 0; i < 15; i++)
                    await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
                var PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
                while (Math.Abs(TeleportCoords.Item1 - PlayerCoords.Item1) > 5 || Math.Abs(TeleportCoords.Item2 - PlayerCoords.Item2) > 5 || Math.Abs(TeleportCoords.Item3 - PlayerCoords.Item3) > 5)
                {
                    ScanEvent.WaitOne();
                    for (int i = 0; i < 15; i++)
                        await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);
                    PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();                    
                }
                ScanEvent.WaitOne();
                await Task.Delay(!GetCheckState(TeleportMode) ? 2_500 : 4_000, token).ConfigureAwait(false);
                OverworldEvent.Set();
            }
            else
            {
                if (Invoke(() => string.IsNullOrEmpty(XCoord.Text) || string.IsNullOrEmpty(YCoord.Text) || string.IsNullOrEmpty(ZCoord.Text)))
                {
                    MessageBox.Show("Scan location coord is empty. read Scan location coord!");
                    await CollideRead(token).ConfigureAwait(false);
                }
                else
                {
                    MessageBox.Show("Teleport location coord is empty. read Teleport location coord!");
                    await CollideReadPro(token).ConfigureAwait(false);
                }
            }
        }
        private async Task TeleportToSpot(string x, string Y, string z, CancellationToken token)
        {
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(Y) || string.IsNullOrEmpty(z))
                return;
            if (!Single.TryParse(x, out float coordx) || !Single.TryParse(Y, out float coordy) || !Single.TryParse(z, out float coordz))
                return;
            await Collide(token).ConfigureAwait(false);
            byte[] X1 = BitConverter.GetBytes(coordx);
            byte[] Y1 = BitConverter.GetBytes(coordy);
            byte[] Z1 = BitConverter.GetBytes(coordz);

            X1 = X1.Concat(Y1).Concat(Z1).ToArray();
            float y = BitConverter.ToSingle(X1, 4);
            y += 120;
            WriteSingleLittleEndian(X1.AsSpan()[4..], y);

            for (int i = 0; i < 15; i++)
                await Executor.SwitchConnection.PointerPoke(X1, Offsets.CollisionPointer, token).ConfigureAwait(false);

            await Task.Delay(3_000).ConfigureAwait(false);
            await Click(B, 19_000, token).ConfigureAwait(false);
            await Task.Delay(6_000, token).ConfigureAwait(false);
            await DisCollide(token).ConfigureAwait(false);
        }

        private new async Task Click(SwitchButton b, int delay, CancellationToken token)
        {
            RefreshConnection();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Click(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
        private bool SandwichmodeValid(int Mode)
        {
            if (InvokeRequired)
                return Invoke(() => Mode >= 0 && Mode < SandwichMode.Items.Count);
            else
                return Mode >= 0 && Mode < SandwichMode.Items.Count;
        }
        private bool Sandwichtypevalid(int Type)
        {
            if (InvokeRequired)
                return Invoke(() => Type >= 0 && Type < poketype.Items.Count);
            else
                return Type >= 0 && Type < poketype.Items.Count;
        }
        private bool SandwichTypeNotChanged()
        {
            if(InvokeRequired)
                return Sandwichtypevalid(PreType) && Invoke(() => poketype.SelectedIndex == PreType) && Sandwichtypevalid(Invoke(() => poketype.SelectedIndex));
            else
                return Sandwichtypevalid(PreType) && poketype.SelectedIndex == PreType && Sandwichtypevalid(poketype.SelectedIndex);
        }
        private bool SandwichModeNotChanged()
        {
            if (InvokeRequired)
                return SandwichmodeValid(PreMode) && Invoke(() => SandwichMode.SelectedIndex == PreMode) && SandwichmodeValid(Invoke(() => SandwichMode.SelectedIndex));
            else
                return SandwichmodeValid(PreMode) && SandwichMode.SelectedIndex == PreMode && SandwichmodeValid(SandwichMode.SelectedIndex);
        }
        private async Task<bool> SetUpItems(MoveType Type, CancellationToken token)
        {
            if (Type <= MoveType.Any || Type > MoveType.Fairy)
                return false;
            if (Invoke(() => SandwichMode.SelectedIndex) < 0 || Invoke(() => SandwichMode.SelectedIndex >= SandwichMode.Items.Count))
                return false;
            if (Invoke(() => ScanButton.Enabled))
                DisableOptions();
            TextBox[] ItemList = { eggviewer.Item1Value, eggviewer.Item2Value, eggviewer.Item3Value, eggviewer.Item4Value, eggviewer.Item5Value, eggviewer.Item6Value };
            NumericUpDown[] CountList = { eggviewer.Item1Count, eggviewer.Item2Count, eggviewer.Item3Count, eggviewer.Item4Count, eggviewer.Item5Count, eggviewer.Item6Count };
            Invoke(() => eggviewer.Initialize(ItemList, CountList));
            List<(int, int, int, bool)> EatItem = [];
            List<(int, int, int, bool)> EatItem2 = [];
            var pouch = await itemStructure.GetPouches(token).ConfigureAwait(false);
            var bag = itemStructure.GetItems(pouch);
            eggviewer.Ingredients = itemStructure.GrabItems(bag);
            eggviewer.Condiments = itemStructure.GrabCondiments(bag);
            var success = itemStructure.SelectItem(eggviewer.Ingredients, ref EatItem, TypeIngredients[(int)Type]);
            if (!success)
            {
                MessageBox.Show("Ingredients is out of stock!");
                return false;
            }
            eggviewer.EatItem1 = EatItem;
            if (!AllMysticaSalt.ContainsKey(Invoke(() => SandwichMode.SelectedIndex)))
                Invoke(() => SandwichMode.SelectedIndex = 0);
            var AllEatItemDict = AllMysticaSalt[Invoke(() => SandwichMode.SelectedIndex)];
            if (!AllEatItemDict.ContainsKey(Type))
                Type = MoveType.Any;
            var EatItemList = AllEatItemDict[Type];
            foreach (var data in EatItemList)
            {
                success = itemStructure.SelectCondiments(eggviewer.Condiments, ref EatItem2, data.Item1, data.Item2);
                if (!success)
                {
                    MessageBox.Show("Condiments is out of stock!");
                    return false;
                }
            }
            eggviewer.EatItem2 = EatItem2;
            eggviewer.EatItem1.OrderBy(z => z.Item3);
            eggviewer.EatItem2.OrderBy(z => z.Item3);
            for (int i = 0; i < eggviewer.EatItem1.Count; i++)
            {
                if (i >= 3)
                    break;
                Invoke(() =>
                { 
                    ItemList[i].Text = eggviewer.EatItem1[i].Item3.ToString();
                    CountList[i].Value = eggviewer.EatItem1[i].Item2;
                });
            }
            for (int i = 0; i < eggviewer.EatItem2.Count; i++)
            {
                if (i + 3 >= Invoke(() =>ItemList.Length)) break;

                Invoke(() =>
                {                    
                    ItemList[i + 3].Text = eggviewer.EatItem2[i].Item3.ToString();
                    CountList[i + 3].Value = eggviewer.EatItem2[i].Item2;
                });
            }
            return true;
        }
        private async Task ScanOverworld(CancellationToken token, bool ReConnect)
        {
            (float, float, float) TargetCoords = new();
            int Encounter = 0;
            if (InvokeRequired)
                Invoke(() => RateBox.Text = string.Empty);
            else
                RateBox.Text = string.Empty;
            SpecFormsList = [];
            ulong init = 0;
            ulong lastsavedinit = 0;
            PictureBox[] boxes = { pictureBox1, pictureBox3, pictureBox5, pictureBox7, pictureBox9, pictureBox11, pictureBox13, pictureBox15, pictureBox17, pictureBox19, pictureBox21, pictureBox23, pictureBox25, pictureBox27, pictureBox29, pictureBox31, pictureBox33, pictureBox35, pictureBox37, pictureBox39 };
            TextBox[] outputBox = { textBox1, textBox2, textBox3, textBox4, textBox5, textBox6, textBox7, textBox8, textBox9, textBox10, textBox11, textBox12, textBox13, textBox14, textBox15, textBox16, textBox17, textBox18, textBox19, textBox20 };
            PictureBox[] markboxes = { pictureBox2, pictureBox4, pictureBox6, pictureBox8, pictureBox10, pictureBox12, pictureBox14, pictureBox16, pictureBox18, pictureBox20, pictureBox22, pictureBox24, pictureBox26, pictureBox28, pictureBox30, pictureBox32, pictureBox34, pictureBox36, pictureBox38, pictureBox40 };
            coordList = [];
            string url;
            string sprite;
            bool initialize = true;
            int waitcount = 0;
            DialogResult result1 = DialogResult.Yes;
            DialogResult result2 = DialogResult.Yes;
            bool spawnfound = false;
            TeraRaidBlockOffset = await Executor.SwitchConnection.PointerAll(Offsets.RaidBlockPointerP, token).ConfigureAwait(false);
            if (!ReConnect || seed == 0)
                seed = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    if (!ReConnect || (OutbreakSpeciesBox.SelectedIndex >= 0 && TargetMon <= 0))
                    {
                        TargetMon = OutbreakSpeciesBox.SelectedIndex < 0 ? (ushort)0 : (ushort)SpeciesList.IndexOf((string)OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex]!);
                        if (!FormCombo.Visible)
                            TargetForm = 0;
                        else
                        {
                            var formslist = FormConverter.GetFormList(TargetMon, TypesList, FormsList, GenderList, EntityContext.Gen9).ToList();
                            TargetForm = (byte)formslist.IndexOf((string)FormCombo.Items[FormCombo.SelectedIndex]!);
                        }
                    }
                });
            }
            else
            {
                if (!ReConnect || (OutbreakSpeciesBox.SelectedIndex >= 0 && TargetMon <= 0))
                {
                    TargetMon = OutbreakSpeciesBox.SelectedIndex < 0 ? (ushort)0 : (ushort)SpeciesList.IndexOf((string)OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex]!);
                    if (!FormCombo.Visible)
                        TargetForm = 0;
                    else
                    {
                        var formslist = FormConverter.GetFormList(TargetMon, TypesList, FormsList, GenderList, EntityContext.Gen9).ToList();
                        TargetForm = (byte)formslist.IndexOf((string)FormCombo.Items[FormCombo.SelectedIndex]!);
                    }
                }
            }
            long unixTime = 0;
            TimeZoneInfo info = TimeZoneInfo.Local;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ulong PreviousSaved = 0;
            if (!token.IsCancellationRequested)
            {
                if (!GetCheckState(NonSaveMode))
                {
                    if (Invoke(() =>OutbreakType.SelectedIndex) is not 0 && (!await IsOnDesiredMap(token).ConfigureAwait(false) || !await OutbreakFound(token).ConfigureAwait(false)))
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        MessageBox.Show("Target Outbreak is not found.");
                        canceled = true;
                        EnableOptions();
                        return;
                    }
                    Invoke(() => info = TimeZoneInfo.FindSystemTimeZoneById((string)TimeZoneCombo.SelectedValue!));
                    unixTime = await GetUnixTime(token).ConfigureAwait(false);
                    if (!ReConnect || SetTime <= 0)
                    {
                        SetTime = unixTime;
                        SetDateTime = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddSeconds(SetTime), info);
                        EnrollmentDate = SetDateTime.Date + TimeSpan.FromDays(1.0);
                    }
                    
                    var datapack = await GetLastSaveTime(lastsavedinit, token).ConfigureAwait(false);
                    if (lastsavedinit == 0)
                        lastsavedinit = datapack.Item2;
                    PreviousSaved = datapack.Item1;
                    Invoke(() => ConnectionBox.Text += $"{Environment.NewLine}ReConnect State: {ReConnect}");
                    if (!ReConnect || DateCycleTime <= 0)
                    {
                        DateCycleTime = (await GetLastDateCycle(0, token).ConfigureAwait(false)).Item1;
                        (BaseTimes, DateCycleTimeFix) = GetFixedLastDateCycle(DateCycleTime, MapParent);
                    }
                    if (BaseTimes == Times.All_Times)
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        MessageBox.Show("Wrong DateCycle!");
                        canceled = true;
                        EnableOptions();
                        return;
                    }
                    //ConnectionBox.Text += $"{Environment.NewLine}SetTime: {(SetTime > 0 ? SetDateTime.ToString("yyyy/MM/dd HH:mm:ss") : SetTime)}{Environment.NewLine}Enrollment Date: {EnrollmentDate.ToString("yyyy/MM/dd HH:mm:ss")}{Environment.NewLine}Original Seed: {seed:X}";
                    Invoke(() =>TimeText.Text = $"{TimeZoneInfo.ConvertTimeFromUtc(epoch.AddSeconds(unixTime), info): yyyy/MM/dd HH:mm:ss}");
                    Invoke(() => TargetCoords = (Single.Parse(XCoord.Text), Single.Parse(YCoord.Text), Single.Parse(ZCoord.Text)));
                    if (GetCheckState(TeleportMode))
                    {
                        if (!ReConnect)
                        {
                            if (!string.IsNullOrEmpty(coordx) && !string.IsNullOrEmpty(coordy) && !string.IsNullOrEmpty(coordz))
                            {
                                result1 = MessageBox.Show(this, "Teleport Location is not empty. continue in same location?", "Teleport location prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            }
                            if (!string.IsNullOrEmpty(Invoke(() => XCoord.Text)) && !string.IsNullOrEmpty(Invoke(() => YCoord.Text)) && !string.IsNullOrEmpty(Invoke(() => ZCoord.Text)))
                            {
                                result2 = MessageBox.Show(this, "Scan Location is not empty. continue in same location?", "Scan location prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            }
                            if (result2 == DialogResult.No || string.IsNullOrEmpty(Invoke(() => XCoord.Text)) || string.IsNullOrEmpty(Invoke(() => YCoord.Text)) || string.IsNullOrEmpty(Invoke(() => ZCoord.Text)))
                            {
                                ResetTaskStatus();
                                ScanTask = Task.Run(async () => 
                                { 
                                    await CollideRead(token).ConfigureAwait(false); 
                                    CollideTaskComplete = true; 
                                    while (!OverworldTaskComplete)
                                        await Task.Delay(0_010, token).ConfigureAwait(false);
                                });
                                OverworldTask = RecoverToOverworld(token);
                                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                    throw new OperationCanceledException();
                            }
                            if (result1 == DialogResult.No || string.IsNullOrEmpty(coordx) || string.IsNullOrEmpty(coordy) || string.IsNullOrEmpty(coordz))
                            {
                                MessageBox.Show(this, "Scan location coord is read! Go to Teleport location.", "Teleport Location Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                ResetTaskStatus();
                                ScanTask = Task.Run(async () => 
                                { 
                                    await CollideReadPro(token).ConfigureAwait(false); 
                                    CollideTaskComplete = true; 
                                    while (!OverworldTaskComplete)
                                        await Task.Delay(0_010, token).ConfigureAwait(false);
                                });
                                OverworldTask = RecoverToOverworld(token);
                                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                    throw new OperationCanceledException();
                            }
                        }
                        ResetTaskStatus();
                        Task CollideTaskScan = Task.Run(async() => 
                        { 
                            await CollideToSpot(Invoke(() => XCoord.Text), Invoke(() => YCoord.Text), Invoke(() => ZCoord.Text), token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        Task CollideTaskTeleport = Task.Run(async() => 
                        { 
                            await CollideToSpot(coordx, coordy, coordz, token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        if (!GetCheckState(EatOnStart))
                            await Task.WhenAny(CollideTaskScan, OverworldTask).ConfigureAwait(false);
                        else if (GetCheckState(ScanLocationCannotPicnic))
                            await Task.WhenAny(CollideTaskTeleport, OverworldTask).ConfigureAwait(false);
                        else
                            await Task.WhenAny(CollideTaskScan, OverworldTask).ConfigureAwait(false);
                        if (CollideTaskScan.IsFaulted || CollideTaskTeleport.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(CollideTaskScan.IsFaulted ? CollideTaskScan.Exception!.InnerException!.ToString() : CollideTaskTeleport.IsFaulted ? CollideTaskTeleport.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (CollideTaskScan.IsCanceled || CollideTaskTeleport.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                        if (GetCheckState(EatOnStart))
                        {
                            await RefreshOnMountOffset(token).ConfigureAwait(false);
                            ResetTaskStatus();
                            ScanTask = DisCollideReposite(token);
                            OverworldTask = RecoverToOverworld(token);
                            await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                            if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                            if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                throw new OperationCanceledException();
                        }
                    }
                    ResetTaskStatus();
                    ScanTask = PrepareToPicnic((!GetCheckState(EatOnStart) ? Invoke(() => XCoord.Text) : GetCheckState(ScanLocationCannotPicnic) ? coordx : Invoke(() => XCoord.Text)), (!GetCheckState(EatOnStart) ? Invoke(() => YCoord.Text) : GetCheckState(ScanLocationCannotPicnic) ? coordy : Invoke(() => YCoord.Text)), (!GetCheckState(EatOnStart) ? Invoke(() => ZCoord.Text) : GetCheckState(ScanLocationCannotPicnic) ? coordz : Invoke(() => ZCoord.Text)), token);
                    OverworldTask = RecoverToOverworld(token);
                    await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                    if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                        throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                    if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                        throw new OperationCanceledException();
                    bool Teleport = false;
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            Teleport = TeleportMode.Checked;
                            if (!eggviewer.HoldIngredients.Checked)
                                eggviewer.HoldIngredients.Checked = true;
                        });
                    }
                    else
                    {
                        Teleport = TeleportMode.Checked;
                        if (!eggviewer.HoldIngredients.Checked)
                            eggviewer.HoldIngredients.Checked = true;
                    }
                    await Task.Delay(Teleport ? 4_000 : 2_000, token).ConfigureAwait(false);
                    bool TypeChangeSuccess = ReConnect;
                    bool ModeChangeSuccess = ReConnect;
                    if (!ReConnect && SandwichTypeNotChanged())
                    {
                        if (InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                DialogResult dialogResult = MessageBox.Show(this, "Seleted shiny power type is same as previous one. change?", "Shiny power type prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (dialogResult == DialogResult.Yes)
                                {
                                    poketype.SelectedIndex = -1;
                                    EnableOptions();
                                }
                                else
                                    TypeChangeSuccess = true;
                            });                            
                        }
                        else
                        {
                            DialogResult dialogResult = MessageBox.Show(this, "Seleted shiny power type is same as previous one. change?", "Shiny power type prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dialogResult == DialogResult.Yes)
                            {
                                poketype.SelectedIndex = -1;
                                EnableOptions();
                            }
                            else
                                TypeChangeSuccess = true;
                        }
                    }
                    if (!ReConnect && SandwichModeNotChanged())
                    {
                        if(InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                DialogResult dialogResult = MessageBox.Show(this, "Seleted shiny power mode is same as previous one. change?", "Shiny power mode prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (dialogResult == DialogResult.Yes)
                                {
                                    SandwichMode.SelectedIndex = -1;
                                    if (!TypeChangeSuccess)
                                        EnableOptions();
                                }
                                else
                                    ModeChangeSuccess = true;
                            });
                        }
                        else
                        {
                            DialogResult dialogResult = MessageBox.Show(this, "Seleted shiny power mode is same as previous one. change?", "Shiny power mode prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (dialogResult == DialogResult.Yes)
                            {
                                SandwichMode.SelectedIndex = -1;
                                if (!TypeChangeSuccess)
                                    EnableOptions();
                            }
                            else
                                ModeChangeSuccess = true;
                        }                        
                    }
                    bool success = TypeChangeSuccess && ModeChangeSuccess;
                    int PokeType = 0;
                    int SandwichModeIndex = 0;
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            PokeType = poketype.SelectedIndex;
                            SandwichModeIndex = SandwichMode.SelectedIndex;
                        });
                    }
                    else
                    {
                        PokeType = poketype.SelectedIndex;
                        SandwichModeIndex = SandwichMode.SelectedIndex;
                    }
                    while (!success || !Sandwichtypevalid(PokeType) || !SandwichmodeValid(SandwichModeIndex))
                    {
                        success = await SetUpItems((MoveType)PokeType, token).ConfigureAwait(false);
                        if (!Invoke(() =>ScanButton.Enabled) && (!Sandwichtypevalid(PokeType) || !SandwichmodeValid(SandwichModeIndex)))
                            EnableOptions();

                        if (success || (!success && Sandwichtypevalid(PokeType) && SandwichmodeValid(SandwichModeIndex)))
                        {
                            Invoke(() =>
                            {
                                if (string.IsNullOrEmpty(poketype.Text))
                                    poketype.Text = poketype.Items[poketype.SelectedIndex]!.ToString();
                                if (string.IsNullOrEmpty(SandwichMode.Text))
                                    SandwichMode.Text = SandwichMode.Items[SandwichMode.SelectedIndex]!.ToString();
                            });
                            DisableOptions();
                            break;
                        }
                        await Task.Delay(0_500, token).ConfigureAwait(false);
                        if (InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                PokeType = poketype.SelectedIndex;
                                SandwichModeIndex = SandwichMode.SelectedIndex;
                            });
                        }
                        else
                        {
                            PokeType = poketype.SelectedIndex;
                            SandwichModeIndex = SandwichMode.SelectedIndex;
                        }
                        waitcount++;
                        if (waitcount > 30)
                        {
                            Invoke(() =>
                            {
                                poketype.SelectedIndex = 8;
                                SandwichMode.SelectedIndex = 0;
                            });
                            PreType = 8;
                            PreMode = 0;
                        }
                    }
                    if (!success)
                    {
                        EnableOptions();
                        canceled = true;
                        return;
                    }
                    if (waitcount <= 30)
                    {
                        Invoke(() =>
                        {
                            PreType = !Sandwichtypevalid(poketype.SelectedIndex) ? -1 : poketype.SelectedIndex;
                            PreMode = !SandwichmodeValid(SandwichMode.SelectedIndex) ? -1 : SandwichMode.SelectedIndex;
                        });
                    }
                    if (GetCheckState(EatOnStart))
                    {
                        await RefreshOnMountOffset(token).ConfigureAwait(false);
                        await OpenPicnic(token).ConfigureAwait(false);
                        var Check = await eggviewer.OnlyMakeSandwich(token, TeleportMode.Checked).ConfigureAwait(false);
                        if (!Check.Item1)
                        {
                            await Click(HOME, 1_000, token).ConfigureAwait(false);
                            if (InvokeRequired)
                                Invoke(() => MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                            else
                                MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            EnableOptions();
                            return;
                        }
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () => 
                        { 
                            await ClosePicnic(token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                        if (GetCheckState(ScanLocationCannotPicnic))
                        {
                            ResetTaskStatus();
                            ScanTask = Task.Run(async () => 
                            { 
                                await CollideToSpot(Invoke(() => XCoord.Text), Invoke(() => YCoord.Text), Invoke(() => ZCoord.Text), token).ConfigureAwait(false); 
                                CollideTaskComplete = true; 
                                while (!OverworldTaskComplete)
                                    await Task.Delay(0_010, token).ConfigureAwait(false);
                            });
                            OverworldTask = RecoverToOverworld(token);
                            await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                            if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                            if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                throw new OperationCanceledException();
                            await Task.Delay(3_000).ConfigureAwait(false);
                        }
                    }
                }
            }
            bool Scan = false;
            if (InvokeRequired)
                Invoke(() => Scan = ScanButton.Enabled);
            else
                Scan = ScanButton.Enabled;
            if (Scan)
                DisableOptions();

            while (!token.IsCancellationRequested)
            {
                var wait = TimeSpan.FromMinutes(30);
                var endTime = DateTime.Now + wait;
                while (DateTime.Now < endTime)
                {
                    int matchcount = 0;
                    string species = string.Empty;
                    string caption = string.Empty;
                    List<string> Spec = new();
                    List<string> captions = new();
                    List<int> matchindex = new();
                    Invoke(() =>
                    {
                        ScanButton.Text = "Scan!";
                        for (int i = 0; i < 20; i++)
                        {
                            boxes[i].Image = null;
                            markboxes[i].Image = null;
                            outputBox[i].Text = string.Empty;
                            outputBox[i].BackColor = colors.Item1;
                            outputBox[i].ForeColor = colors.Item2;
                        }
                    });

                    if (GetCheckState(PicnicReset) && !GetCheckState(NonSaveMode) && !initialize)
                    {
                        await OpenPicnic(token).ConfigureAwait(false);
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () => 
                        { 
                            await ClosePicnic(token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                    }
                    else if (!GetCheckState(NonSaveMode) && !initialize && !GetCheckState(TeleportMode))
                    {
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () =>
                        {
                            await CollideReposite(token).ConfigureAwait(false);
                            ScanEvent.WaitOne();
                            await SetStick(SwitchStick.LEFT, 0, -30000, 2000, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.LEFT, 0, 0, 0, token).ConfigureAwait(false);
                            OverworldEvent.Set();
                            ScanEvent.WaitOne();
                            await SetStick(SwitchStick.LEFT, 0, 30000, 2200, token).ConfigureAwait(false);
                            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false);
                            OverworldEvent.Set();
                            CollideTaskComplete = true;
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                    }
                    else if (!initialize && !GetCheckState(NonSaveMode))
                    {
                        ResetTaskStatus();
                        ScanTask = Task.Run(async () =>
                        {
                            await CollideToSpot(coordx, coordy, coordz, token).ConfigureAwait(false);
                            await CollideToSpot(Invoke(() => XCoord.Text), Invoke(() => YCoord.Text), Invoke(() => ZCoord.Text), token).ConfigureAwait(false);
                            CollideTaskComplete = true;
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                        await Task.Delay(1_000).ConfigureAwait(false);
                    }
                    if (GetCheckState(PicnicReset))
                    {
                        await RefreshOnMountOffset(token).ConfigureAwait(false);
                        ResetTaskStatus();
                        ScanTask = Task.Run(async() => 
                        { 
                            await DisCollideOnly(token).ConfigureAwait(false); 
                            CollideTaskComplete = true; 
                            while (!OverworldTaskComplete)
                                await Task.Delay(0_010, token).ConfigureAwait(false);
                        });
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                    }
ReSave:
                    ulong seed_new = 0;
                    if (!GetCheckState(NonSaveMode))
                    {
                        await Task.Delay(2_000).ConfigureAwait(false);
                        ResetTaskStatus();
                        ScanTask = PrepareSave(TargetCoords, token);
                        OverworldTask = RecoverToOverworld(token);
                        await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                        if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                            throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                        if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                            throw new OperationCanceledException();
                        seed_new = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 8, token).ConfigureAwait(false), 0);
                        long CurrentTime = -1;
                        CurrentTime = await GetUnixTime(token).ConfigureAwait(false);
                        DateTime CurDateTime = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddSeconds(CurrentTime), info);
                        if ((seed != seed_new || EnrollmentDate - TimeSpan.FromMinutes(1.0) < CurDateTime) && Invoke(() => OutbreakType.SelectedIndex) > 0)
                        {
                            LogUtil.LogText($"Target outbreak is lost! Adjust time{Environment.NewLine}Current seed(New): {seed_new:X}, Desired seed: {seed:X}");
                            await TimeAdjustToPreviousTime(seed, (ulong)unixTime, token).ConfigureAwait(false);
                            LogUtil.LogText($"Target outbreak is found! Adjusting time is success!{Environment.NewLine}Set Time: {TimeZoneInfo.ConvertTimeFromUtc(epoch.AddSeconds(unixTime), info): yyyy/MM/dd HH:mm:ss}{Environment.NewLine}Current seed(New): {seed_new:X}, Desired seed: {seed:X}");
                            init = 0;
                            lastsavedinit = 0;
                            Invoke(new Action(() => Reset.Checked = true));
                            await BoostShinyRolls(token).ConfigureAwait(false);
                            break;
                        }
                        Times Curtimes = Times.All_Times;
                        long LimitTime = -1;
                        bool TimeMatch = true;
                        Invoke(() =>
                        {
                            if (TimeCombo.SelectedIndex > 0 || TargetTimesList.Count > 0)
                            {
                                (Curtimes, LimitTime) = GetTimesFromLastDate(DateCycleTimeFix, CurrentTime, BaseTimes);
                                LimitTime = GetTrueLimitTime(TargetTimesList, Curtimes, LimitTime);
                            }
                            if (TimeCombo.SelectedIndex > 0)
                            {
                                TimeMatch = CheckTimesSatisfied(Curtimes);
                            }
                            if (TargetTimesList.Count > 0)
                            {
                                if (!TargetTimesList.Contains(Curtimes))
                                    TimeMatch = false;
                            }
                            if (TimeCombo.SelectedIndex > 0 || TargetTimesList.Count > 0)
                            {
                                RateBox.Text = $"Current Times: {Curtimes}" + RateBox.Text;
                                RateBox.Text = $"Time Match: {TimeMatch}{(TimeMatch ? $"{Environment.NewLine}Scan until {TimeSpan.FromSeconds(LimitTime).TotalMinutes:0.0000} minutes later from Now!" : "")}{Environment.NewLine}" + RateBox.Text;
                            }
                        });
                        if (!TimeMatch)
                        {
                            await TimeAdjustToPreviousTime(seed, (ulong)unixTime, token).ConfigureAwait(false);
                            init = 0;
                            lastsavedinit = 0;
                            Reset.Invoke(new Action(() => Reset.Checked = true));
                            await BoostShinyRolls(token).ConfigureAwait(false);
                            break;
                        }
                        await SVSaveGameOverworld(token).ConfigureAwait(false);
                        var timetuple = await GetLastSaveTime(lastsavedinit, token).ConfigureAwait(false);
                        if (lastsavedinit == 0)
                            lastsavedinit = timetuple.Item2;
                        if (timetuple.Item1 == PreviousSaved)
                        {
                            if (GetCheckState(PicnicReset))
                            {
                                ResetTaskStatus();
                                ScanTask = Task.Run(async () => 
                                { 
                                    await ClosePicnic(token).ConfigureAwait(false); 
                                    CollideTaskComplete = true; 
                                    while (!OverworldTaskComplete)
                                        await Task.Delay(0_010, token).ConfigureAwait(false);
                                });
                                OverworldTask = RecoverToOverworld(token);
                                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                                    throw new OperationCanceledException();
                            }
                            if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                            {
                                await Task.Delay(0_500, token).ConfigureAwait(false);
                            }
                            goto ReSave;
                        }
                        PreviousSaved = timetuple.Item1;
                    }
                    var tuple = await ReadBlock(BlocksOverworld.Overworld, init, token).ConfigureAwait(false);
                    var test = tuple.Item1;
                    if (init == 0)
                        init = tuple.Item2;
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            ScanButton.Text = "Scanning...";
                            if (OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0)
                                spawnfound = false;
                        });
                    }
                    else
                    {
                        ScanButton.Text = "Scanning...";
                        if (OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0)
                            spawnfound = false;
                    }
                    RibbonIndex TimeMark = RibbonIndex.MAX_COUNT;
                    RibbonIndex WeatherMark = RibbonIndex.MAX_COUNT;
                    for (int i = 0; i < 20; i++)
                    {
                        PK9 pk = new(test.AsSpan(0 + (i * 0x1D4), 0x158).ToArray());
                        var coord = test.AsSpan(0 + (i * 0x1D4) + 0x158, 0xC).ToArray();

                        bool isValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                        if (!isValid || pk == null || pk.Species < 0 || pk.Species > (int)Species.MAX_COUNT)
                        {
                            Invoke(() =>
                            {
                                outputBox[i].Text = "No Pokémon present.";
                                sprite = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/starter.png";
                                boxes[i].Load(sprite);
                                ScanButton.Text = "Done";
                            });
                            break;
                        }
                        Encounter++;
                        if (SpecFormsList.ContainsKey(pk.Species))
                        {
                            if (SpecFormsList[pk.Species].ContainsKey(pk.Form))
                                SpecFormsList[pk.Species][pk.Form] += 1;
                            else
                                SpecFormsList[pk.Species].Add(pk.Form, 1);
                        }
                        else
                        {
                            var addDict = new Dictionary<byte, int>
                            {
                                { pk.Form, 1 }
                            };
                            SpecFormsList.Add(pk.Species, addDict);
                        }
                        sprite = PokeImg(pk, false);
                        Invoke(() =>
                        {
                            string output = GetRealPokemonString(pk);
                            outputBox[i].Text = output;
                            boxes[i].Load(sprite);
                        });

                        if (HasMark(pk, out RibbonIndex mark))
                        {
                            if (IsTimeMark(mark))
                                TimeMark = mark;
                            if (IsWeatherMark(mark))
                                WeatherMark = mark;
                            url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/ribbonmark{mark.ToString().Replace("Mark", "").ToLower()}.png";
                            Invoke(() =>markboxes[i].Load(url));
                        }
                        for (int j = 0; j < encounterFilters.Count; j++)
                        {
                            if (!encounterFilters[j].Enabled)
                                continue;
                            if (WeatherMark < RibbonIndex.MAX_COUNT && TargetWeatherMarkFilters.ContainsKey(encounterFilters[j].Name) && !TargetWeatherMarkFilters[encounterFilters[j].Name].Contains(WeatherMark))
                                continue;


                            if (ValidateEncounter(pk, encounterFilters[j]))
                            {
                                species = $"Match Found in index{i + 1} {Strings.Species[pk.Species]}{FormOutput(Strings, pk.Species, pk.Form, out _)}!";
                                matchindex.Add(i);
                                caption = $"Filter {encounterFilters[j].Name} is satisfied!";
                                Spec.Add(species);
                                captions.Add(caption);
                                matchcount++;
                                WebHookUtil.SendDetailNotifications(pk, sprite, true, TrainerInfo);
                                Invoke(() =>
                                {
                                    if (pk.IsShiny)
                                    {
                                        outputBox[i].BackColor = Color.Gold;
                                        outputBox[i].ForeColor = Color.BlueViolet;
                                    }
                                    else
                                    {
                                        outputBox[i].BackColor = Color.YellowGreen;
                                        outputBox[i].ForeColor = Color.OrangeRed;
                                    }
                                });
                                break;
                            }
                        }
                        if(pk.IsShiny && matchcount <= 0)
                            WebHookUtil.SendDetailNotifications(pk, sprite, false, TrainerInfo);
                        if (InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                if ((OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0) && pk.Species == TargetMon && (!FormCombo.Visible || pk.Form == TargetForm))
                                {
                                    spawnfound = true;
                                    OutbreakText.Text = $"{Strings.Species[pk.Species]}{FormOutput(pk.Species, pk.Form, out _)}";
                                }
                            });
                        }
                        else
                        {
                            if ((OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0) && pk.Species == TargetMon && (!FormCombo.Visible || pk.Form == TargetForm))
                            {
                                spawnfound = true;
                                OutbreakText.Text = $"{Strings.Species[pk.Species]}{FormOutput(pk.Species, pk.Form, out _)}";
                            }
                        }
                            coordList.Add(coord);
                    }
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            RateBox.Text = $"TotalEncounters: {Encounter}{Environment.NewLine}{PrintPokemonPopulation(SpecFormsList, Encounter)}";
                            ScanButton.Text = "Done";
                            TeleportIndex.Maximum = coordList.Count - 1;
                        });
                    }
                    else
                    {
                        RateBox.Text = $"TotalEncounters: {Encounter}{Environment.NewLine}{PrintPokemonPopulation(SpecFormsList, Encounter)}";
                        ScanButton.Text = "Done";
                        TeleportIndex.Maximum = coordList.Count - 1;
                    }
                    if (matchcount > 0)
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        if (InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                TeleportIndex.Value = matchindex.FirstOrDefault();
                                for (int i = 0; i < captions.Count; i++)
                                    MessageBox.Show(this, Spec[i], captions[i]);
                            });
                        }
                        else
                        {
                            TeleportIndex.Value = matchindex.FirstOrDefault();
                            for (int i = 0; i < captions.Count; i++)
                                MessageBox.Show(this, Spec[i], captions[i]);
                        }
                        EnableOptions();
                        return;
                    }
                    coordList = [];
                    if (GetCheckState(NonSaveMode))
                    {
                        EnableOptions();
                        return;
                    }
                    if (initialize)
                        initialize = false;
                    if (GetCheckState(Stop))
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        EnableOptions();
                        if (InvokeRequired)
                            Invoke(() => MessageBox.Show(this, "Stop Scaning!"));
                        else
                            MessageBox.Show(this, "Stop Scaning!");
                        return;
                    }
                    if (Invoke(() => TimeCombo.SelectedIndex) > 0 && !spawnfound)
                    {
                        LogUtil.LogText("Target pokemon is not found! Adjust time");
                        await TimeAdjustToPreviousTime(seed, (ulong)unixTime, token).ConfigureAwait(false);
                        LogUtil.LogText($"Target outbreak and Pokemon are found! Adjusting time is success!{Environment.NewLine}Set Time: {TimeZoneInfo.ConvertTimeFromUtc(epoch.AddSeconds(unixTime), info): yyyy/MM/dd HH:mm:ss}{Environment.NewLine}Current seed(New): {seed_new:X}, Desired seed: {seed:X}");
                        if (InvokeRequired)
                        {
                            Invoke(() =>
                            {
                                OutbreakText.Text = "Not Active";
                                Reset.Checked = true;
                            });
                        }
                        else
                        {
                            OutbreakText.Text = "Not Active";
                            Reset.Checked = true;
                        }
                        init = 0;
                        lastsavedinit = 0;
                        await BoostShinyRolls(token).ConfigureAwait(false);
                        break;
                    }
                    if (!TargetOutbreakIsHighPopulation(SpecFormsList, Encounter, TargetMon, TargetForm))
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        EnableOptions();
                        MessageBox.Show(this, "Target Outbreak Population is less than 50%");
                        return;
                    }
                }
                if(InvokeRequired)
                    Invoke(() => ScanButton.Text = "Scan!");
                else
                    ScanButton.Text = "Scan!";
                await RefreshOnMountOffset(token).ConfigureAwait(false);
                if (GetCheckState(ScanLocationCannotPicnic))
                {
                    ResetTaskStatus();
                    ScanTask = Task.Run(async () => 
                    { 
                        await CollideToSpot(coordx, coordy, coordz, token).ConfigureAwait(false); 
                        CollideTaskComplete = true; 
                        while (!OverworldTaskComplete)
                            await Task.Delay(0_010, token).ConfigureAwait(false);
                    });
                    OverworldTask = RecoverToOverworld(token);
                    await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                    if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                        throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                    if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                        throw new OperationCanceledException();
                }
                if (!await PlayerNotOnMount(token).ConfigureAwait(false))
                {
                    ResetTaskStatus();
                    ScanTask = DisCollideReposite(token);
                    OverworldTask = RecoverToOverworld(token);
                    await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                    if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                        throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                    if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                        throw new OperationCanceledException();
                }
                ResetTaskStatus();
                ScanTask = PrepareToPicnic((GetCheckState(ScanLocationCannotPicnic) ? coordx : Invoke(() => XCoord.Text)), (GetCheckState(ScanLocationCannotPicnic) ? coordy : Invoke(() => YCoord.Text)), (GetCheckState(ScanLocationCannotPicnic) ? coordz : Invoke(() => ZCoord.Text)), token);
                OverworldTask = RecoverToOverworld(token);
                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                    throw new OperationCanceledException();
                if (GetCheckState(TeleportMode))
                    await Task.Delay(8_000, token).ConfigureAwait(false);
                await OpenPicnic(token).ConfigureAwait(false);
                var Check = await eggviewer.OnlyMakeSandwich(token, TeleportMode.Checked).ConfigureAwait(false);
                if (!Check.Item1)
                {
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    if (InvokeRequired)
                        Invoke(() => MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    else
                        MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableOptions();
                    return;
                }
                ResetTaskStatus();
                ScanTask = Task.Run(async () => 
                { 
                    await ClosePicnic(token).ConfigureAwait(false); 
                    CollideTaskComplete = true; 
                    while (!OverworldTaskComplete)
                        await Task.Delay(0_010, token).ConfigureAwait(false);
                });
                OverworldTask = RecoverToOverworld(token);
                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                    throw new OperationCanceledException();
                if (GetCheckState(ScanLocationCannotPicnic))
                {
                    ResetTaskStatus();
                    ScanTask = Task.Run(async () => 
                    { 
                        await CollideToSpot(XCoord.Text, YCoord.Text, ZCoord.Text, token).ConfigureAwait(false); 
                        CollideTaskComplete = true; 
                        while (!OverworldTaskComplete)
                            await Task.Delay(0_010, token).ConfigureAwait(false);
                    });
                    OverworldTask = RecoverToOverworld(token);
                    await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                    if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                        throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                    if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                        throw new OperationCanceledException();
                    await Task.Delay(1_000).ConfigureAwait(false);
                }
                initialize = true;
            }
            EnableOptions();
            return;
        }
        private async Task<(ulong, ulong)> GetLastSaveTime(ulong init, CancellationToken token)
        {
            var data = await ReadBlock(BlocksOverworld.LastSaved, init, token).ConfigureAwait(false);
            var LastSavedTime = GetDateTime(data.Item1);
            return (LastSavedTime, data.Item2);
        }
        private ulong GetDateTime(byte[] data)
        {
            Epoch1900DateTimeValue epoch1900DateTime = new(data);
            return epoch1900DateTime.TotalSeconds;
        }
        private double GetTargetPopulationRate(Dictionary<ushort, Dictionary<byte, int>> PokeList, int Total, ushort TargetMon, byte TargetForm)
        {
            if (!PokeList.ContainsKey(TargetMon))
                return 0.0;
            if (IsVisible(FormCombo) && !PokeList[TargetMon].ContainsKey(TargetForm))
                return 0.0;
            var TargetCount = PokeList[TargetMon][TargetForm];
            var Rate = 100.00 * TargetCount / Total;
            return Rate;
        }
        private bool IsVisible(ComboBox form)
        {
            if (InvokeRequired)
            {
                return Invoke(new Func<bool>(() => form.Visible));
            }
            else
            {
                return form.Visible;
            }
        }
        private bool TargetOutbreakIsHighPopulation(Dictionary<ushort, Dictionary<byte, int>> PokeList, int Total, ushort TargetMon, byte TargetForm)
        {
            int SpeciesIndex = 0;
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    SpeciesIndex = OutbreakSpeciesBox.SelectedIndex;
                });
            }
            else
            {
                SpeciesIndex = OutbreakSpeciesBox.SelectedIndex;                
            }
            if (SpeciesIndex < 0)
                return true;


            var Population = GetTargetPopulationRate(PokeList, Total, TargetMon, TargetForm);
            if (Population < 20.0)
                return false;

            if (Total < 100)
                return true;

            return Population > 50.00;
        }
        private string PrintPokemonPopulation(Dictionary<ushort, Dictionary<byte, int>> PokeList, int Total)
        {
            string pkstr = string.Empty;
            foreach (var data in PokeList)
            {
                foreach (var dict in data.Value)
                {
                    pkstr += $"{Strings.specieslist[data.Key]}{FormOutput(data.Key, dict.Key, out _)}: {dict.Value} ({100.00 * dict.Value / Total:0.00}%){Environment.NewLine}";
                }
            }
            return pkstr;
        }
        private bool IsTimeMark(RibbonIndex mark) => mark >= RibbonIndex.MarkLunchtime && mark <= RibbonIndex.MarkDawn;
        private bool IsWeatherMark(RibbonIndex mark) => mark >= RibbonIndex.MarkCloudy && mark <= RibbonIndex.MarkMisty;
        private async Task<(long, ulong)> GetLastDateCycle(ulong init, CancellationToken token)
        {
            var data = await ReadEncryptedBlockInt64(Blocks.KLastDateCycle, init, token).ConfigureAwait(false);
            return (data.Item1, data.Item2);

        }
        private bool CheckTimesSatisfied(long LastDateCycle, long CurrentTime)
        {
            var diff = LastDateCycle - CurrentTime;
            var Mod = diff % DateCycle;
            var Diff_From_Start = DateCycle - Mod;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            RateBox.Text = $"LastDateCycle Time: {epoch.AddSeconds(LastDateCycle).ToLocalTime()}{Environment.NewLine}Current Time: {epoch.AddSeconds(CurrentTime).ToLocalTime()}{Environment.NewLine}Diff From Current Time: {diff} seconds";
            long BaseTime = TimeBase.GetTimeBase((Times)TimeCombo.SelectedIndex);
            RateBox.Text += $"{Environment.NewLine}Diff From {(Times)TimeCombo.SelectedIndex} Start: {TimeSpan.FromSeconds(Diff_From_Start).TotalMinutes}{Environment.NewLine}Scan until {TimeSpan.FromSeconds(BaseTime).TotalMinutes} later from Start!";
            if (BaseTime < 0)
                return true;
            if (Diff_From_Start <= BaseTime)
                return true;
            return false;
        }
        private Times GetTimesFromBase(long LastDateCycle, long CurrentTime)
        {
            var diff = LastDateCycle - CurrentTime;
            var Mod = diff % DateCycle;
            var Diff_From_Start = DateCycle - Mod;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            RateBox.Text = $"LastDateCycle Time: {epoch.AddSeconds(LastDateCycle).ToLocalTime()}{Environment.NewLine}Current Time: {epoch.AddSeconds(CurrentTime).ToLocalTime()}{Environment.NewLine}Diff From Current Time: {diff} seconds";
            Times baseTimes = (Times)TimeCombo.SelectedIndex == Times.Morning_to_Evening ? Times.Morning : (Times)TimeCombo.SelectedIndex;
            long BaseTime = TimeBase.GetTimeBase(baseTimes);
            long BaseNextTime = TimeBase.GetNextTimeBase(baseTimes);
            RateBox.Text += $"{Environment.NewLine}Diff From {(Times)TimeCombo.SelectedIndex} Start: {TimeSpan.FromSeconds(Diff_From_Start).TotalMinutes}{Environment.NewLine}Scan until {TimeSpan.FromSeconds(BaseTime).TotalMinutes} later from Start!";
            if (Diff_From_Start <= BaseTime)
                return baseTimes;
            if (BaseTime < Diff_From_Start && Diff_From_Start <= BaseNextTime)
                return TimeBase.GetNextTimes(baseTimes);
            if (Diff_From_Start > BaseNextTime)
                return TimeBase.GetNextTimes(TimeBase.GetNextTimes(baseTimes));

            return Times.All_Times;
        }
        private (DateTime, long) GetLocalDateTime(long Ticks)
        {
            int TimeIndex = 0;
            object? TimeValue = null;
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    TimeIndex = TimeZoneCombo.SelectedIndex;
                    TimeValue = TimeZoneCombo.SelectedValue;
                }));
            }
            else
            {
                TimeIndex = TimeZoneCombo.SelectedIndex;
                TimeValue = TimeZoneCombo.SelectedValue;
            }
            if (TimeIndex < 0 || TimeValue == null)
                return (DateTime.UtcNow, (long)TimeSpan.Zero.TotalSeconds);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime UTCDate = epoch.AddSeconds(Ticks);
            TimeZoneInfo info = TimeZoneInfo.FindSystemTimeZoneById((string)TimeValue);
            DateTime CurrentDate = TimeZoneInfo.ConvertTimeFromUtc(UTCDate, info);
            return (CurrentDate, (long)new TimeSpan(CurrentDate.Ticks - UTCDate.Ticks).TotalSeconds);

        }
        private (Times, long) GetFixedLastDateCycle(long LastDateCycle, TeraRaidMapParent Target)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime LastDate = epoch.AddSeconds(LastDateCycle);
            //ConnectionBox.Text += $"{Environment.NewLine}LastDate Cycle: {LastDate:yyyy/MM/dd HH:mm:ss}";
            (Times baseTimes, TeraRaidMapParent Map) = TimesReroll.GetLastDateDic(LastDate);
            if (baseTimes == Times.All_Times)
                return (Times.All_Times, -1);
            var LastDateCycleFix = TimesReroll.ChangeLastDateToTarget(Map, Target, LastDateCycle);
            //ConnectionBox.Text += $"{(string.IsNullOrEmpty(ConnectionBox.Text) ? "" : Environment.NewLine)}LastDateCycle Time in {Map}: {LastDate}{Environment.NewLine}LastDateCycle Time in {Target}: {epoch.AddSeconds(LastDateCycleFix)/*GetLocalDateTime(LastDateCycleFix).Item1*/}";
            if(InvokeRequired)
                Invoke(new Action(() => LastDateCombo.SelectedIndex = (int)baseTimes - 1));
            else
                LastDateCombo.SelectedIndex = (int)baseTimes - 1;
            return (baseTimes, LastDateCycleFix);
        }
        private (Times, long) GetTimesFromLastDate(long LastDateCycleFix, long CurrentTime, Times baseTimes)
        {
            (DateTime CurrentDate, long Offset) = GetLocalDateTime(CurrentTime);
            var diff = LastDateCycleFix - (CurrentTime + Offset);
            var difftemp = Math.Abs(diff);
            var Mod = difftemp % DateCycle;
            var Diff_From_Start = diff > 0 ? DateCycle - Mod : Mod;
            //RateBox.Text = $"{Environment.NewLine}Current Time: {CurrentDate}(UTC Offset: {Offset}){Environment.NewLine}Diff From Current Time: {difftemp} seconds";
            long BaseTime = TimeBase.GetTimeBase(baseTimes);
            long BaseNextTime = TimeBase.GetNextTimeBase(baseTimes);
            long BaseSencondNextTime = TimeBase.GetSecondNextTimeBase(baseTimes);
            //RateBox.Text += $"{Environment.NewLine}Diff From {baseTimes} Start: {TimeSpan.FromSeconds(Diff_From_Start).TotalMinutes}";
            if (Diff_From_Start <= BaseTime)
                return (baseTimes, BaseTime - Diff_From_Start);
            if (BaseTime < Diff_From_Start && Diff_From_Start <= BaseNextTime)
                return (TimeBase.GetNextTimes(baseTimes), BaseNextTime - Diff_From_Start);
            if (Diff_From_Start > BaseNextTime && Diff_From_Start <= BaseSencondNextTime)
                return (TimeBase.GetNextTimes(TimeBase.GetNextTimes(baseTimes)), BaseSencondNextTime - Diff_From_Start);
            if (BaseSencondNextTime < Diff_From_Start)
                return (TimeBase.GetNextTimes(TimeBase.GetNextTimes(TimeBase.GetNextTimes(baseTimes))), DateCycle - Diff_From_Start);

            return (Times.All_Times, -1);
        }
        private bool CheckTimesSatisfied(Times CurTimes)
        {
            if ((Times)TimeCombo.SelectedIndex == Times.All_Times)
                return true;

            if ((Times)TimeCombo.SelectedIndex == Times.Morning_to_Evening)
            {
                if (CurTimes == Times.Morning || CurTimes == Times.Day || CurTimes == Times.Evening)
                    return true;
            }

            if ((Times)TimeCombo.SelectedIndex == CurTimes)
                return true;

            return false;
        }
        private long GetLimitTime(Times CurrentTimes, long TempLimitTime)
        {
            if ((Times)TimeCombo.SelectedIndex == Times.All_Times)
                return long.MaxValue;

            if ((Times)TimeCombo.SelectedIndex != Times.Morning_to_Evening)
                return TempLimitTime;

            if (CurrentTimes == Times.Morning)
                return TempLimitTime + TimeBase.BaseDay + TimeBase.BaseEvening;
            if (CurrentTimes == Times.Day)
                return TempLimitTime + TimeBase.BaseEvening;
            if (CurrentTimes == Times.Evening)
                return TempLimitTime;

            return -1;
        }
        private long GetLimitTimeFromList(List<Times> TargetList, Times CurrentTimes, long TempLimitTime)
        {
            if (!TargetList.Contains(CurrentTimes))
                return long.MaxValue;

            if (TargetList.Count <= 1)
                return TempLimitTime;

            for (int i = TargetList.IndexOf(CurrentTimes); i < TargetList.Count; i++)
            {
                if (i + 1 >= TargetList.Count)
                {
                    if (TargetList[i] == Times.Night && TargetList[0] == Times.Morning)
                    {
                        TempLimitTime += TimeBase.GetNextBaseTime(TargetList[i]);
                        i = 0;
                    }
                    else
                        break;
                }
                if (TargetList[i] + 1 == TargetList[i + 1])
                    TempLimitTime += TimeBase.GetNextBaseTime(TargetList[i]);
                else
                    break;
            }
            return TempLimitTime;
        }
        private long GetTrueLimitTime(List<Times> TargetList, Times CurrentTimes, long TempLimitTime)
        {
            var templimit1 = GetLimitTime(CurrentTimes, TempLimitTime);
            var templimit2 = GetLimitTimeFromList(TargetList, CurrentTimes, TempLimitTime);
            if (templimit1 < templimit2)
                return templimit1;
            return templimit2;
        }
        private List<Times> BuildTargetList(List<Times> Target)
        {
            Target = Target.Distinct().ToList();
            Target.Remove(Times.All_Times);
            Target.Remove(Times.Morning_to_Evening);
            if (Target.Count >= 4)
                Target.Clear();
            if (Target.Count <= 1)
                return Target;

            Target = Target.OrderBy(i => i).ToList();
            return Target;
        }
        private async Task PrepareSave((float, float, float) TargetCoords, CancellationToken token)
        {
            var PlayerCoords = (await PlayerCoordRead(token).ConfigureAwait(false)).ToTuple();
            ScanEvent.WaitOne();
            bool Teleport = false;
            if (InvokeRequired)
                Invoke(() => Teleport = TeleportMode.Checked);
            else
                Teleport = TeleportMode.Checked;
            if (Math.Abs(TargetCoords.Item1 - PlayerCoords.Item1) > (Teleport ? 5 : 0.5) || Math.Abs(TargetCoords.Item2 - PlayerCoords.Item2) > (Teleport ? 5 : 0.5) || Math.Abs(TargetCoords.Item3 - PlayerCoords.Item3) > (Teleport ? 5 : 0.5))
            {
                await RefreshOnMountOffset(token).ConfigureAwait(false);
                (string X, string Y, string Z) = (string.Empty, string.Empty, string.Empty);
                if (InvokeRequired)
                {
                    Invoke(() =>
                    {
                        X = XCoord.Text;
                        Y = YCoord.Text;
                        Z = ZCoord.Text;                        
                    });
                }
                else
                {
                    X = XCoord.Text;
                    Y = YCoord.Text;
                    Z = ZCoord.Text;                    
                }
                await CollideToSpot(X, Y, Z, token).ConfigureAwait(false);
                if (GetCheckState(PicnicReset))
                {
                    ScanEvent.WaitOne();
                    await DisCollideOnly(token).ConfigureAwait(false);
                }
                if (!GetCheckState(TeleportMode))
                {
                    await Reposition(token).ConfigureAwait(false);
                }
                ScanEvent.WaitOne();
                await PrepareSave(TargetCoords, token).ConfigureAwait(false);
                return;
            }
            OverworldEvent.Set();
            CollideTaskComplete = true;
            while (!OverworldTaskComplete)
                await Task.Delay(0_010, token).ConfigureAwait(false);
        }
        private async Task DefeatPokemon(CancellationToken token)
        {
            while (await IsInBattle(token).ConfigureAwait(false))
                await Click(A, 0_800, token).ConfigureAwait(false);
        }
        private async Task Flee(CancellationToken token)
        {
            while (await IsInBattle(token).ConfigureAwait(false))
            {
                for (int i = 0; i < 3; i++)
                {
                    await Click(B, 0_500, token).ConfigureAwait(false);
                    if (!await IsInBattle(token).ConfigureAwait(false))
                        return;
                }
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                if (!await IsInBattle(token).ConfigureAwait(false))
                    break;
                await Click(B, 0_500, token).ConfigureAwait(false);
                if (!await IsInBattle(token).ConfigureAwait(false))
                    break;
                await Click(A, 0_500, token).ConfigureAwait(false);                
            }            
        }
        private async Task FastFlee(CancellationToken token)
        {
            while (await IsInBattle(token).ConfigureAwait(false) && !await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
            {
                for (int i = 0; i < 3; i++)
                {
                    await Click(B, 0_500, token).ConfigureAwait(false);
                    if (!await IsInBattle(token).ConfigureAwait(false) || await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                        return;
                }
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                if (!await IsInBattle(token).ConfigureAwait(false) || await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                    break;
                await Click(B, 0_500, token).ConfigureAwait(false);
                if (!await IsInBattle(token).ConfigureAwait(false) || await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                    break;
                await Click(A, 0_500, token).ConfigureAwait(false);
                await RefreshOverworldOffset(token).ConfigureAwait(false);
            }
        }

        private async Task<bool> IsInBattle(CancellationToken token)
        {
            RefreshConnection();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(Offsets.IsInBattle, 1, token).ConfigureAwait(false);
            return data[0] <= 0x05;
        }
        private async Task InitilizeSessionOffsets(CancellationToken token)
        {
            RefreshConnection();

            TeraRaidBlockOffset = await Executor.SwitchConnection.PointerAll(Offsets.RaidBlockPointerP, token).ConfigureAwait(false);
            BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            while (PlayerOnMountOffset <= 0)
                PlayerOnMountOffset = await Executor.SwitchConnection.PointerAll(Offsets.PlayerOnMountPointer, token).ConfigureAwait(false);
            OverWorldOffset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            eggviewer.ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
            return;
        }
        private async Task RefreshOverworldOffset(CancellationToken token)
        {
            RefreshConnection();
            OverWorldOffset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            while (OverWorldOffset <= 0)
                OverWorldOffset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            return;
        }
        private async Task<bool> IsOnDesiredMap(CancellationToken token)
        {
            await SVSaveGameOverworld(token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
redo:
            var data = await ReadEncryptedBlockByte(Blocks.KPlayerCurrentFieldID, CacheField, token).ConfigureAwait(false);
            if (data.Item1 > 2)
            {
                BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                CacheField = 0;
                goto redo;
            }
            int MapIndex = 0;
            if (InvokeRequired)
                Invoke(() => MapIndex = OutbreakType.SelectedIndex);
            else
                MapIndex = OutbreakType.SelectedIndex;
            var desired = MapIndex switch
            {
                1 or 5 => TeraRaidMapParent.Paldea,
                2 or 6 => TeraRaidMapParent.Kitakami,
                3 or 7 => TeraRaidMapParent.Blueberry,
                _ => TeraRaidMapParent.Paldea
            };
            if (CacheField == 0)
                CacheField = data.Item2;
            if (desired > TeraRaidMapParent.Blueberry || desired < TeraRaidMapParent.Paldea)
                return false;
            MapParent = (TeraRaidMapParent)data.Item1;
            if (MapIndex <= 0 || MapIndex == 4 || MapIndex == 8)
                return true;

            return MapParent == desired;
        }
        private async Task<bool> OutbreakFound(CancellationToken token)
        {
            OutbreakCoords = new();
            bool outbreakfound = false;
            var block = Blocks.KOutbreakSpecies1;
            var formblock = Blocks.KMassOutbreak01Form;
            var pos = Blocks.KMassOutbreak01CenterPos;
            int OutbreakTypeIndex = 0;
            if(InvokeRequired)
                Invoke(() => OutbreakTypeIndex = OutbreakType.SelectedIndex);
            else
                OutbreakTypeIndex = OutbreakType.SelectedIndex;
            if (OutbreakTypeIndex is 1 or 4)
            {
recalc:
                var dataP = await ReadEncryptedBlockByte(Blocks.KMassOutbreakTotalPaldea, CountCacheP, token).ConfigureAwait(false);
                if (CountCacheP == 0)
                    CountCacheP = dataP.Item2;

                var OutbreaktotalP = Convert.ToInt32(dataP.Item1);
                if (OutbreaktotalP > 8)
                {
                    BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                    // Rerun in case of bad pointer
                    OutbreakCache = new();
                    LoadOutbreakCache();
                    CountCacheP = 0;
                    goto recalc;
                }

                for (int i = 0; i < 8; i++)
                {
                    switch (i)
                    {
                        case 0: break;
                        case 1: block = Blocks.KOutbreakSpecies2; formblock = Blocks.KMassOutbreak02Form; pos = Blocks.KMassOutbreak02CenterPos; break;
                        case 2: block = Blocks.KOutbreakSpecies3; formblock = Blocks.KMassOutbreak03Form; pos = Blocks.KMassOutbreak03CenterPos; break;
                        case 3: block = Blocks.KOutbreakSpecies4; formblock = Blocks.KMassOutbreak04Form; pos = Blocks.KMassOutbreak04CenterPos; break;
                        case 4: block = Blocks.KOutbreakSpecies5; formblock = Blocks.KMassOutbreak05Form; pos = Blocks.KMassOutbreak05CenterPos; break;
                        case 5: block = Blocks.KOutbreakSpecies6; formblock = Blocks.KMassOutbreak06Form; pos = Blocks.KMassOutbreak06CenterPos; break;
                        case 6: block = Blocks.KOutbreakSpecies7; formblock = Blocks.KMassOutbreak07Form; pos = Blocks.KMassOutbreak07CenterPos; break;
                        case 7: block = Blocks.KOutbreakSpecies8; formblock = Blocks.KMassOutbreak08Form; pos = Blocks.KMassOutbreak08CenterPos; break;
                    }
                    if (i > OutbreaktotalP - 1)
                        break;

                    var (species, sofs) = await ReadEncryptedBlockUint(block, OutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesLoaded = sofs;
                    var (form, fofs) = await ReadEncryptedBlockByte(formblock, OutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesFormLoaded = fofs;
                    var (obpos, bofs) = await ReadEncryptedBlockArray(pos, OutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                    var specnum = SpeciesConverter.GetNational9((ushort)species);
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                            {
                                outbreakfound = true;
                                OutbreakCoords.Add(obpos);
                                OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";                                
                            }
                            return outbreakfound;
                        });
                    }
                    else
                    {
                        if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                        {
                            outbreakfound = true;
                            OutbreakCoords.Add(obpos);
                            OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                            return outbreakfound;
                        }
                    }

                }
            }
            if (OutbreakTypeIndex is 2 or 4)
            {
recalc:
                var dataK = await ReadEncryptedBlockByte(Blocks.KMassOutbreakTotalKitakami, CountCacheK, token).ConfigureAwait(false);
                //UpdateProgress(20, 100);
                if (CountCacheK == 0)
                    CountCacheK = dataK.Item2;

                var OutbreaktotalK = Convert.ToInt32(dataK.Item1);
                if (OutbreaktotalK > 4)
                {
                    BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                    // Rerun in case of bad pointer
                    OutbreakCache = new();
                    LoadOutbreakCache();
                    CountCacheK = 0;
                    goto recalc;
                }

                for (int i = 8; i < 12; i++)
                {
                    switch (i)
                    {
                        case 8: block = Blocks.KOutbreakSpecies9; formblock = Blocks.KMassOutbreak09Form; pos = Blocks.KMassOutbreak09CenterPos; break;
                        case 9: block = Blocks.KOutbreakSpecies10; formblock = Blocks.KMassOutbreak10Form; pos = Blocks.KMassOutbreak10CenterPos; break;
                        case 10: block = Blocks.KOutbreakSpecies11; formblock = Blocks.KMassOutbreak11Form; pos = Blocks.KMassOutbreak11CenterPos; break;
                        case 11: block = Blocks.KOutbreakSpecies12; formblock = Blocks.KMassOutbreak12Form; pos = Blocks.KMassOutbreak12CenterPos; break;
                    }
                    if (i > OutbreaktotalK + 8 - 1)
                        break;

                    var (species, sofs) = await ReadEncryptedBlockUint(block, OutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesLoaded = sofs;
                    var (form, fofs) = await ReadEncryptedBlockByte(formblock, OutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesFormLoaded = fofs;
                    var (obpos, bofs) = await ReadEncryptedBlockArray(pos, OutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                    var specnum = SpeciesConverter.GetNational9((ushort)species);
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                            {
                                outbreakfound = true;
                                OutbreakCoords.Add(obpos);
                                OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                            }
                            return outbreakfound;
                        });
                    }
                    else
                    {
                        if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                        {
                            outbreakfound = true;
                            OutbreakCoords.Add(obpos);
                            OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                            return outbreakfound;
                        }
                    }

                }
            }
            if (OutbreakTypeIndex is 3 or 4)
            {
recalc:
                var dataB = await ReadEncryptedBlockByte(Blocks.KMassOutbreakTotalBlueberry, CountCacheK, token).ConfigureAwait(false);
                //UpdateProgress(20, 100);
                if (CountCacheB == 0)
                    CountCacheB = dataB.Item2;

                var OutbreaktotalB = Convert.ToInt32(dataB.Item1);
                if (OutbreaktotalB > 5)
                {
                    BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                    // Rerun in case of bad pointer
                    OutbreakCache = new();
                    LoadOutbreakCache();
                    CountCacheB = 0;
                    goto recalc;
                }

                for (int i = 12; i < 17; i++)
                {
                    switch (i)
                    {
                        case 12: block = Blocks.KOutbreakSpecies13; formblock = Blocks.KMassOutbreak13Form; pos = Blocks.KMassOutbreak13CenterPos; break;
                        case 13: block = Blocks.KOutbreakSpecies14; formblock = Blocks.KMassOutbreak14Form; pos = Blocks.KMassOutbreak14CenterPos; break;
                        case 14: block = Blocks.KOutbreakSpecies15; formblock = Blocks.KMassOutbreak15Form; pos = Blocks.KMassOutbreak15CenterPos; break;
                        case 15: block = Blocks.KOutbreakSpecies16; formblock = Blocks.KMassOutbreak16Form; pos = Blocks.KMassOutbreak16CenterPos; break;
                        case 16: block = Blocks.KOutbreakSpecies17; formblock = Blocks.KMassOutbreak17Form; pos = Blocks.KMassOutbreak17CenterPos; break;
                    }
                    if (i > OutbreaktotalB + 12 - 1)
                        break;

                    var (species, sofs) = await ReadEncryptedBlockUint(block, OutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesLoaded = sofs;
                    var (form, fofs) = await ReadEncryptedBlockByte(formblock, OutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesFormLoaded = fofs;
                    var (obpos, bofs) = await ReadEncryptedBlockArray(pos, OutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                    OutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                    var specnum = SpeciesConverter.GetNational9((ushort)species);
                    if (InvokeRequired)
                    {
                        Invoke(() =>
                        {
                            if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                            {
                                outbreakfound = true;
                                OutbreakCoords.Add(obpos);
                                OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                            }
                            return outbreakfound;
                        });
                    }
                    else
                    {
                        if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                        {
                            outbreakfound = true;
                            OutbreakCoords.Add(obpos);
                            OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                            return outbreakfound;
                        }
                    }
                }
            }
            if (OutbreakTypeIndex is 5 or 6 or 7 or 8)
            {
                var BCATObEnabled = await ReadEncryptedBlockBool(Blocks.KBCATOutbreakEnabled, token).ConfigureAwait(false);
                if (BCATObEnabled)
                {
                    var BCOspecies = Blocks.KOutbreakBC01MainSpecies;
                    var BCOform = Blocks.KOutbreakBC01MainForm;
                    var BCOcenter = Blocks.KOutbreakBC01MainCenterPos;

                    if (OutbreakTypeIndex is 5 or 8)
                    {
recalc:
                        var dataBCP = await ReadEncryptedBlockByte(Blocks.KOutbreakBCMainNumActive, CountCacheBCP, token).ConfigureAwait(false);
                        if (CountCacheBCP == 0)
                            CountCacheBCP = dataBCP.Item2;

                        var OutbreaktotalBCP = Convert.ToInt32(dataBCP.Item1);
                        if (OutbreaktotalBCP > 10)
                        {
                            BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                            // Rerun in case of bad pointer
                            BCATOutbreakCache = new();
                            LoadBCATOutbreakCache();
                            CountCacheBCP = 0;
                            goto recalc;
                        }

                        for (int i = 0; i < 10; i++)
                        {
                            switch (i)
                            {
                                case 1: BCOspecies = Blocks.KOutbreakBC02MainSpecies; BCOform = Blocks.KOutbreakBC02MainForm; BCOcenter = Blocks.KOutbreakBC02MainCenterPos; break;
                                case 2: BCOspecies = Blocks.KOutbreakBC03MainSpecies; BCOform = Blocks.KOutbreakBC03MainForm; BCOcenter = Blocks.KOutbreakBC03MainCenterPos; break;
                                case 3: BCOspecies = Blocks.KOutbreakBC04MainSpecies; BCOform = Blocks.KOutbreakBC04MainForm; BCOcenter = Blocks.KOutbreakBC04MainCenterPos; break;
                                case 4: BCOspecies = Blocks.KOutbreakBC05MainSpecies; BCOform = Blocks.KOutbreakBC05MainForm; BCOcenter = Blocks.KOutbreakBC05MainCenterPos; break;
                                case 5: BCOspecies = Blocks.KOutbreakBC06MainSpecies; BCOform = Blocks.KOutbreakBC06MainForm; BCOcenter = Blocks.KOutbreakBC06MainCenterPos; break;
                                case 6: BCOspecies = Blocks.KOutbreakBC07MainSpecies; BCOform = Blocks.KOutbreakBC07MainForm; BCOcenter = Blocks.KOutbreakBC07MainCenterPos; break;
                                case 7: BCOspecies = Blocks.KOutbreakBC08MainSpecies; BCOform = Blocks.KOutbreakBC08MainForm; BCOcenter = Blocks.KOutbreakBC08MainCenterPos; break;
                                case 8: BCOspecies = Blocks.KOutbreakBC09MainSpecies; BCOform = Blocks.KOutbreakBC09MainForm; BCOcenter = Blocks.KOutbreakBC09MainCenterPos; break;
                                case 9: BCOspecies = Blocks.KOutbreakBC10MainSpecies; BCOform = Blocks.KOutbreakBC10MainForm; BCOcenter = Blocks.KOutbreakBC10MainCenterPos; break;
                            }
                            if (i > OutbreaktotalBCP - 1)
                                break;

                            var (species, sofs) = await ReadEncryptedBlockUint(BCOspecies, BCATOutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesLoaded = sofs;
                            var (form, fofs) = await ReadEncryptedBlockByte(BCOform, BCATOutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesFormLoaded = fofs;
                            var (obpos, bofs) = await ReadEncryptedBlockArray(BCOcenter, BCATOutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                            var specnum = SpeciesConverter.GetNational9((ushort)species);
                            if (InvokeRequired)
                            {
                                Invoke(() =>
                                {
                                    if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                    {
                                        outbreakfound = true;
                                        OutbreakCoords.Add(obpos);
                                        OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    }
                                    return outbreakfound;
                                });
                            }
                            else
                            {
                                if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                {
                                    outbreakfound = true;
                                    OutbreakCoords.Add(obpos);
                                    OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    return outbreakfound;
                                }
                            }
                        }
                    }

                    if (OutbreakTypeIndex is 6 or 8)
                    {
recalc:
                        var dataBCK = await ReadEncryptedBlockByte(Blocks.KOutbreakBCDLC1NumActive, CountCacheBCK, token).ConfigureAwait(false);
                        if (CountCacheBCK == 0)
                            CountCacheBCK = dataBCK.Item2;

                        var OutbreaktotalBCK = Convert.ToInt32(dataBCK.Item1);
                        if (OutbreaktotalBCK > 10)
                        {
                            BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                            // Rerun in case of bad pointer
                            BCATOutbreakCache = new();
                            LoadBCATOutbreakCache();
                            CountCacheBCK = 0;
                            goto recalc;
                        }

                        BCOspecies = Blocks.KOutbreakBC01DLC1Species;
                        BCOform = Blocks.KOutbreakBC01DLC1Form;
                        BCOcenter = Blocks.KOutbreakBC01DLC1CenterPos;

                        for (int i = 10; i < 20; i++)
                        {
                            switch (i)
                            {
                                case 11: BCOspecies = Blocks.KOutbreakBC02DLC1Species; BCOform = Blocks.KOutbreakBC02DLC1Form; BCOcenter = Blocks.KOutbreakBC02DLC1CenterPos; break;
                                case 12: BCOspecies = Blocks.KOutbreakBC03DLC1Species; BCOform = Blocks.KOutbreakBC03DLC1Form; BCOcenter = Blocks.KOutbreakBC03DLC1CenterPos; break;
                                case 13: BCOspecies = Blocks.KOutbreakBC04DLC1Species; BCOform = Blocks.KOutbreakBC04DLC1Form; BCOcenter = Blocks.KOutbreakBC04DLC1CenterPos; break;
                                case 14: BCOspecies = Blocks.KOutbreakBC05DLC1Species; BCOform = Blocks.KOutbreakBC05DLC1Form; BCOcenter = Blocks.KOutbreakBC05DLC1CenterPos; break;
                                case 15: BCOspecies = Blocks.KOutbreakBC06DLC1Species; BCOform = Blocks.KOutbreakBC06DLC1Form; BCOcenter = Blocks.KOutbreakBC06DLC1CenterPos; break;
                                case 16: BCOspecies = Blocks.KOutbreakBC07DLC1Species; BCOform = Blocks.KOutbreakBC07DLC1Form; BCOcenter = Blocks.KOutbreakBC07DLC1CenterPos; break;
                                case 17: BCOspecies = Blocks.KOutbreakBC08DLC1Species; BCOform = Blocks.KOutbreakBC08DLC1Form; BCOcenter = Blocks.KOutbreakBC08DLC1CenterPos; break;
                                case 18: BCOspecies = Blocks.KOutbreakBC09DLC1Species; BCOform = Blocks.KOutbreakBC09DLC1Form; BCOcenter = Blocks.KOutbreakBC09DLC1CenterPos; break;
                                case 19: BCOspecies = Blocks.KOutbreakBC10DLC1Species; BCOform = Blocks.KOutbreakBC10DLC1Form; BCOcenter = Blocks.KOutbreakBC10DLC1CenterPos; break;
                            }
                            if (i > OutbreaktotalBCK + 10 - 1)
                                break;

                            var (species, sofs) = await ReadEncryptedBlockUint(BCOspecies, BCATOutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesLoaded = sofs;
                            var (form, fofs) = await ReadEncryptedBlockByte(BCOform, BCATOutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesFormLoaded = fofs;
                            var (obpos, bofs) = await ReadEncryptedBlockArray(BCOcenter, BCATOutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                            var specnum = SpeciesConverter.GetNational9((ushort)species);
                            if (InvokeRequired)
                            {
                                Invoke(() =>
                                {
                                    if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                    {
                                        outbreakfound = true;
                                        OutbreakCoords.Add(obpos);
                                        OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    }
                                    return outbreakfound;
                                });
                            }
                            else
                            {
                                if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                {
                                    outbreakfound = true;
                                    OutbreakCoords.Add(obpos);
                                    OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    return outbreakfound;
                                }
                            }
                        }
                    }
                    if (OutbreakTypeIndex is 7 or 8)
                    {
recalc:
                        var dataBCB = await ReadEncryptedBlockByte(Blocks.KOutbreakBCDLC2NumActive, CountCacheBCK, token).ConfigureAwait(false);
                        if (CountCacheBCB == 0)
                            CountCacheBCB = dataBCB.Item2;

                        var OutbreaktotalBCB = Convert.ToInt32(dataBCB.Item1);
                        if (OutbreaktotalBCB > 10)
                        {
                            BaseBlockKeyPointer = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
                            // Rerun in case of bad pointer
                            BCATOutbreakCache = new();
                            LoadBCATOutbreakCache();
                            CountCacheBCK = 0;
                            goto recalc;
                        }

                        BCOspecies = Blocks.KOutbreakBC01DLC2Species;
                        BCOform = Blocks.KOutbreakBC01DLC2Form;
                        BCOcenter = Blocks.KOutbreakBC01DLC2CenterPos;

                        for (int i = 20; i < 30; i++)
                        {
                            switch (i)
                            {
                                case 21: BCOspecies = Blocks.KOutbreakBC02DLC2Species; BCOform = Blocks.KOutbreakBC02DLC2Form; BCOcenter = Blocks.KOutbreakBC02DLC2CenterPos; break;
                                case 22: BCOspecies = Blocks.KOutbreakBC03DLC2Species; BCOform = Blocks.KOutbreakBC03DLC2Form; BCOcenter = Blocks.KOutbreakBC03DLC2CenterPos; break;
                                case 23: BCOspecies = Blocks.KOutbreakBC04DLC2Species; BCOform = Blocks.KOutbreakBC04DLC2Form; BCOcenter = Blocks.KOutbreakBC04DLC2CenterPos; break;
                                case 24: BCOspecies = Blocks.KOutbreakBC05DLC2Species; BCOform = Blocks.KOutbreakBC05DLC2Form; BCOcenter = Blocks.KOutbreakBC05DLC2CenterPos; break;
                                case 25: BCOspecies = Blocks.KOutbreakBC06DLC2Species; BCOform = Blocks.KOutbreakBC06DLC2Form; BCOcenter = Blocks.KOutbreakBC06DLC2CenterPos; break;
                                case 26: BCOspecies = Blocks.KOutbreakBC07DLC2Species; BCOform = Blocks.KOutbreakBC07DLC2Form; BCOcenter = Blocks.KOutbreakBC07DLC2CenterPos; break;
                                case 27: BCOspecies = Blocks.KOutbreakBC08DLC2Species; BCOform = Blocks.KOutbreakBC08DLC2Form; BCOcenter = Blocks.KOutbreakBC08DLC2CenterPos; break;
                                case 28: BCOspecies = Blocks.KOutbreakBC09DLC2Species; BCOform = Blocks.KOutbreakBC09DLC2Form; BCOcenter = Blocks.KOutbreakBC09DLC2CenterPos; break;
                                case 29: BCOspecies = Blocks.KOutbreakBC10DLC2Species; BCOform = Blocks.KOutbreakBC10DLC2Form; BCOcenter = Blocks.KOutbreakBC10DLC2CenterPos; break;
                            }
                            if (i > OutbreaktotalBCB + 20 - 1)
                                break;

                            var (species, sofs) = await ReadEncryptedBlockUint(BCOspecies, BCATOutbreakCache[i].SpeciesLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesLoaded = sofs;
                            var (form, fofs) = await ReadEncryptedBlockByte(BCOform, BCATOutbreakCache[i].SpeciesFormLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesFormLoaded = fofs;
                            var (obpos, bofs) = await ReadEncryptedBlockArray(BCOcenter, BCATOutbreakCache[i].SpeciesCenterPOSLoaded, token).ConfigureAwait(false);
                            BCATOutbreakCache[i].SpeciesCenterPOSLoaded = bofs;

                            var specnum = SpeciesConverter.GetNational9((ushort)species);
                            if (InvokeRequired)
                            {
                                Invoke(() =>
                                {
                                    if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                    {
                                        outbreakfound = true;
                                        OutbreakCoords.Add(obpos);
                                        OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    }
                                    return outbreakfound;
                                });
                            }
                            else
                            {
                                if (specnum == TargetMon && (!FormCombo.Visible || form == TargetForm))
                                {
                                    outbreakfound = true;
                                    OutbreakCoords.Add(obpos);
                                    OutbreakText.Text = $"{Strings.Species[specnum]}{FormOutput(Strings, specnum, form, out _)}";
                                    return outbreakfound;
                                }
                            }
                        }
                    }
                }
            }
            return outbreakfound;
        }

        public string GetRealPokemonString(PKM pkm)
        {
            string pid = $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 1)}: {pkm.PID:X8}";
            string ec = $"{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 2)}: {pkm.EncryptionConstant:X8}";
            var form = FormOutput(Strings, pkm.Species, pkm.Form, out _);
            string gender = string.Empty;
            string moveoutput = string.Empty;
            string msg = string.Empty;
            switch (pkm.Gender)
            {
                case 0: gender = $" ({eggviewer.ChangeLanguageString(Language.SelectedIndex, 3)})"; break;
                case 1: gender = $" ({eggviewer.ChangeLanguageString(Language.SelectedIndex, 4)})"; break;
                case 2: break;
            }
            if (pkm is PK9 or PK8)
            {
                var hasMark = HasMark(pkm is PK9 ? (PK9)pkm : (PK8)pkm, out RibbonIndex mark);
                var markinfo = string.Empty;
                if (hasMark)
                {
                    var markstr = Strings.ribbons[53 + (int)mark].Split(new char[] { ' ', '\t' }).ToArray().AsSpan()[1..].ToArray();
                    markinfo = string.Join("", markstr);
                    markinfo = markinfo.Replace("Mark", "").Replace("-Zeichen", "").Replace("Emblema", "").Replace("Insigne", "");
                }
                msg = hasMark ? $"{Environment.NewLine}{ChangeLanguageStringWide(Language.SelectedIndex, 0)}: {markinfo}" : "";
            }
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
            string output = $"{(pkm.ShinyXor == 0 ? "■ - " : pkm.ShinyXor <= 16 ? "★ - " : "")}{Strings.Species[pkm.Species]}{form}{gender}{pid}{ec}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 8)}: {Strings.Natures[(byte)pkm.Nature]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 9)}: {(Strings.Ability[pkm.Ability]).Replace(" ", "")}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 41)}: {Strings.Item[pkm.HeldItem]}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 10)}: {pkm.IV_HP}/{pkm.IV_ATK}/{pkm.IV_DEF}/{pkm.IV_SPA}/{pkm.IV_SPD}/{pkm.IV_SPE}{(pkm.FatefulEncounter ? $"{Environment.NewLine}"+"EVs: "+$"{pkm.EV_HP}/{pkm.EV_ATK}/{pkm.EV_DEF}/{pkm.EV_SPA}/{pkm.EV_SPD}/{pkm.EV_SPE}" : "")}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 40)}: {pkm.MetLevel}{Environment.NewLine}{eggviewer.ChangeLanguageString(Language.SelectedIndex, 11)}: {PokeSizeDetailedUtil.GetSizeRating(pkm is PK9 ? ((PK9)pkm).Scale : ((IScaledSize)pkm).HeightScalar)}{Environment.NewLine}{ChangeLanguageStringWide(Language.SelectedIndex, 21)}: {(pkm is PK9 ? ((PK9)pkm).Scale : ((IScaledSize)pkm).HeightScalar)}{msg}{Environment.NewLine}" + moveoutput;
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
            Real_Scale

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
            大きさ実数値

        }

        private List<string> OutbreakNotActiveStrings =
        [
            "未発生",
            "Not Active",
            "Outbreak is not Active",
        ];
        private void SetForm()
        {
            if (OutbreakSpeciesBox.SelectedIndex < 0)
                return;
            FormCombo.Items.Clear();
            FormCombo.Text = string.Empty;
            ushort SpeciesIndex = (ushort)SpeciesList.IndexOf((string)OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex]!);
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
        private void OutbreakSpeciesBox_SelectedIndexChanged(object sender, EventArgs e) => SetForm();
        private void LanguageChanged(object sender, EventArgs e)
        {
            if (Language.SelectedIndex < 0)
                return;

            Strings = GameInfo.GetStrings(Language.SelectedIndex);
            languageindex = Language.SelectedIndex;
            var specindex = OutbreakSpeciesBox.SelectedIndex;
            var formindex = FormCombo.Visible ? FormCombo.SelectedIndex : 0;
            var typeindex = poketype.SelectedIndex;
            SpeciesList = Strings.specieslist.ToList();
            OutbreakSpeciesBox.DataSource = Strings.specieslist.Where(z => PersonalTable.SV.IsSpeciesInGame((ushort)SpeciesList.IndexOf(z))).ToArray();
            poketype.DataSource = Strings.Types;
            FormsList = Strings.forms;
            TypesList = Strings.types;
            OutbreakSpeciesBox.SelectedIndex = specindex < 0 ? 0 : specindex;
            if (FormCombo.Visible)
                FormCombo.SelectedIndex = formindex < 0 ? 0 : formindex;
            poketype.SelectedIndex = typeindex;
            switch (Language.SelectedIndex)
            {
                case 0:
                    {
                        ScanButton.Text = "スキャン！";
                        HardStopButton.Text = "強制終了";
                        StopConditions.Text = "厳選条件";
                        OutbreakResetButton.Text = "大量発生固定";
                        PicnicReset.Text = "ピクニックリセット";
                        NonSaveMode.Text = "ノンセーブモード";
                        EatOnStart.Text = "最初に食べる？";
                        Stop.Text = "中断";
                        Reset.Text = "リセットした?";
                        checkBox6.Text = "現在位置読取";
                        checkBox7.Text = "テレポート実行";
                        TeleportMode.Text = "テレポートモード";
                        ScanLocationCannotPicnic.Text = "ピクニック不可";
                        OutbreakLabel.Text = "大量発生ポケモン";
                        OutbreakText.Text = !OutbreakNotActiveStrings.Contains(OutbreakText.Text) ? OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex] + (FormCombo.Visible ? (string)FormCombo.Items[FormCombo.SelectedIndex]! : "") : OutbreakNotActiveStrings[Language.SelectedIndex];
                        XCoordLabel.Text = "X座標";
                        YCoordLabel.Text = "Y座標";
                        ZCoordLabel.Text = "Z座標";
                        break;
                    }
                case 1:
                    {
                        ScanButton.Text = "Scan!";
                        HardStopButton.Text = "HardStop";
                        StopConditions.Text = "StopConditions";
                        OutbreakResetButton.Text = "OutbreakReset";
                        PicnicReset.Text = "Picnic Reset";
                        NonSaveMode.Text = "Non Save Mode";
                        EatOnStart.Text = "Eat On Start?";
                        Stop.Text = "Stop";
                        Reset.Text = "Reset?";
                        checkBox6.Text = "Read Coords";
                        checkBox7.Text = "Teleport to Coords";
                        TeleportMode.Text = "Teleport mode";
                        ScanLocationCannotPicnic.Text = "Can't PIcnic in Scan Loc";
                        OutbreakLabel.Text = "OutbreakMon";
                        OutbreakText.Text = !OutbreakNotActiveStrings.Contains(OutbreakText.Text) ? OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex] + (FormCombo.Visible ? (string)FormCombo.Items[FormCombo.SelectedIndex]! : "") : OutbreakNotActiveStrings[Language.SelectedIndex];
                        XCoordLabel.Text = "Coord X";
                        YCoordLabel.Text = "Coord Y";
                        ZCoordLabel.Text = "Coord Z";
                        break;
                    }
                default:
                    {
                        OutbreakText.Text = !OutbreakNotActiveStrings.Contains(OutbreakText.Text) ? OutbreakSpeciesBox.Items[OutbreakSpeciesBox.SelectedIndex] + (FormCombo.Visible ? (string)FormCombo.Items[FormCombo.SelectedIndex]! : "") : OutbreakNotActiveStrings.Last();
                        break;
                    }
            }
        }

        private async Task<bool> PlayerCannotMove(CancellationToken token)
        {
            RefreshConnection();

            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(PlayerCanMoveOffset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }
        private async Task<(uint, ulong)> ReadEncryptedBlockUint(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (ReadUInt32LittleEndian(header.AsSpan()[1..]), address);
        }

        private async Task<(byte[]?, ulong)> ReadEncryptedBlockArray(DataBlock block, ulong init, CancellationToken token)
        {
            RefreshConnection();

            if (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);

            return (data[6..], init);
        }
        private async Task<(byte, ulong)> ReadEncryptedBlockByte(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (header[1], address);
        }

        private async Task<(byte[], ulong)> ReadEncryptedBlockHeader(DataBlock block, ulong init, CancellationToken token)
        {
            RefreshConnection();

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

        private async Task DaySkip(CancellationToken token)
        {
            await Click(B, 0_150, token).ConfigureAwait(false);
            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen

            await Touch(845, 545, 0_050, 1_000, token).ConfigureAwait(false);
            await Touch(845, 545, 0_050, 1_000, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            await PressAndHold(DDOWN, 0_820, 0_100, token).ConfigureAwait(false);
            await Click(DUP, 0_500, token).ConfigureAwait(false);

            await Click(A, 1_250, token).ConfigureAwait(false);
            var curtime = DateTime.Now + timediff + delay;

            await Touch(1006, 386, 0_050, 1_000, token).ConfigureAwait(false);
            await Task.Delay(0_150).ConfigureAwait(false);
            if (curtime.Hour < set_time.Hour)
            {
                for (int i = 0; i < 3; i++)
                    await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
                for (int i = 0; i < set_time.Hour - curtime.Hour; i++)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
                await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
                for (int i = 0; i < (set_time.Minute - curtime.Minute + 60); i++)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
            }
            else if (curtime.Hour > set_time.Hour)
            {
                for (int i = 0; i < 3; i++)
                    await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
                for (int i = 0; i < curtime.Hour - set_time.Hour; i++)
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                await Click(DRIGHT, 0_800, token).ConfigureAwait(false);
                for (int i = 0; i < set_time.Minute - curtime.Minute + 60; i++)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    await Click(DRIGHT, 0_800, token).ConfigureAwait(false);
                for (int i = 0; i < set_time.Minute - curtime.Minute; i++)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
            }
            await Task.Delay(0_150).ConfigureAwait(false);

            await Touch(1102, 470, 0_050, 1_000, token).ConfigureAwait(false);
            await Task.Delay(0_150).ConfigureAwait(false);

            await Click(HOME, 1_000, token).ConfigureAwait(false);
            await Click(A, 4_000, token).ConfigureAwait(false); // Back to title screen
            await Click(X, 0_500, token).ConfigureAwait(false);
            timediff += TimeSpan.FromMinutes(34);
        }

        public async Task SetCurrentTime(ulong date, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode($"setCurrentTime {date}", Executor.UseCRLF);
            await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
        }
        public async Task DaySkipFaster(int day, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("daySkip", Executor.UseCRLF);
            for (int i = 0; i < day; i++)
            {
                await Executor.Connection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
        public async Task DayBackFaster(int day, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("dateBack", Executor.UseCRLF);
            for (int i = 0; i < day; i++)
            {
                await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }

        public async Task TimeSkipFwd(int hour, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipForward", Executor.UseCRLF);
            for (int i = 0; i < hour; i++)
            {
                await Executor.Connection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
        public async Task TimeSkipBwd(int hour, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipBack", Executor.UseCRLF);
            for (int i = 0; i < hour; i++)
            {
                await Executor.Connection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
        public async Task ResetTime(CancellationToken token)
        {
            RefreshConnection();

            await Executor.Connection.SendAsync(SwitchCommand.ResetTime(Executor.UseCRLF), token).ConfigureAwait(false);
        }
        public async Task TimeSkipFwdMinute(int minute, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipForwardMinute", Executor.UseCRLF);
            for (int i = 0; i < minute; i++)
            {
                await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }

        }
        public async Task TimeSkipBwdMinute(int minute, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipBackMinute", Executor.UseCRLF);
            for (int i = 0; i < minute; i++)
            {
                await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }

        }
        public async Task TimeSkipFwdSecond(int second, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipForwardSecond", Executor.UseCRLF);
            for (int i = 0; i < second; i++)
            {
                await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }

        }
        public async Task TimeSkipBwdSecond(int second, int delay, CancellationToken token)
        {
            RefreshConnection();

            var command = SwitchCommand.Encode("timeSkipBackSecond", Executor.UseCRLF);
            for (int i = 0; i < second; i++)
            {
                await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }

        }
        public async Task<long> GetUnixTime(CancellationToken token)
        {
            RefreshConnection();

            return await Executor.SwitchConnection.GetCurrentTime(token).ConfigureAwait(false);
        }
        public async Task<long> ResetNTPTime(CancellationToken token)
        {
            RefreshConnection();

            return await Executor.SwitchConnection.ResetTimeNTP(token).ConfigureAwait(false);
        }
        private async Task RolloverCorrectionSV(CancellationToken token)
        {
            await Click(B, 0_150, token).ConfigureAwait(false);

            await Touch(845, 545, 0_050, 1_000, token).ConfigureAwait(false);
            await Touch(845, 545, 0_050, 1_000, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            await PressAndHold(DDOWN, 0_820, 0_100, token).ConfigureAwait(false);
            await Click(DUP, 0_500, token).ConfigureAwait(false);

            await Click(A, 1_250, token).ConfigureAwait(false);
            await Touch(1006, 386, 0_050, 1_000, token).ConfigureAwait(false);
            await Task.Delay(0_150).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
            await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            await Task.Delay(0_150).ConfigureAwait(false);

            await Touch(1102, 470, 0_050, 1_000, token).ConfigureAwait(false);
            await Task.Delay(0_150).ConfigureAwait(false);

            await Click(HOME, 1_000, token).ConfigureAwait(false);
        }

        public async Task CloseGame(CancellationToken token)
        {
            // Close out of the game
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(HOME, 2_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
        }

        public async Task StartGame(CancellationToken token)
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
            await Task.Delay(19_000, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 5_000;
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0)
                {
                    while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }
            await Task.Delay(2_000, token).ConfigureAwait(false);
        }

        private async Task<bool> IsOnOverworldTitle(CancellationToken token)
        {
            RefreshConnection();

            var offset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }

        private async Task<bool> IsOnTitleScreen(CancellationToken token)
        {
            RefreshConnection();

            var offset = await Executor.SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return true;
        }
        public async Task PressAndHold(SwitchButton b, int hold, int delay, CancellationToken token)
        {
            RefreshConnection();

            await Executor.Connection.SendAsync(SwitchCommand.Hold(b, true), token).ConfigureAwait(false);
            await Task.Delay(hold, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        public async Task MultiPressAndHold(SwitchButton b, SwitchButton c, SwitchButton d, int hold, int delay, CancellationToken token)
        {
            RefreshConnection();

            await Executor.Connection.SendAsync(SwitchCommand.Hold(b, true), token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Hold(c, true), token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Hold(d, true), token).ConfigureAwait(false);
            await Task.Delay(hold, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(b, true), token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(c, true), token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(d, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        public async Task Touch(int x, int y, int hold, int delay, CancellationToken token, bool crlf = true)
        {
            RefreshConnection();

            var command = Encoding.ASCII.GetBytes($"touchHold {x} {y} {hold}{(crlf ? "\r\n" : "")}");
            await Executor.SwitchConnection.SendAsync(command, token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task Reposition(CancellationToken token, bool FirstMove = false)
        {
            if(!FirstMove)
                ScanEvent.WaitOne();
            await Click(Y, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                await Click(Y, 0_500, token).ConfigureAwait(false);
            OverworldEvent.Set();
            ScanEvent.WaitOne();
            await Click(L, 0_600, token).ConfigureAwait(false);
            OverworldEvent.Set();
        }
        private async Task SetUpMap(CancellationToken token)
        {
            await ToOverworld(token).ConfigureAwait(false);
            await Click(Y, 1_000, token).ConfigureAwait(false);
            while (await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
                await Click(Y, 1_000, token).ConfigureAwait(false);
            await Task.Delay(1_500, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 10_000, 0_650, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);
            await ToOverworld(token).ConfigureAwait(false);
        }
        private async Task OpenPicnic(CancellationToken token)
        {
            bool HasReset = false;
            bool Teleport = false;
            await Task.Delay(0_500, token).ConfigureAwait(false);

            bool Overworld = await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false);
            if (Overworld)
                await Click(X, 1_000, token).ConfigureAwait(false);
            Invoke(() => HasReset = Reset.Checked);
            if (HasReset)
            {
                //await Task.Delay(0_800, token).ConfigureAwait(false);
                await Click(DRIGHT, 0_800, token).ConfigureAwait(false);
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                CountCacheP = 0;
                CountCacheK = 0;
                CountCacheB = 0;
                CountCacheBCP = 0;
                CountCacheBCK = 0;
                CountCacheBCB = 0;
                CacheField = 0;
                CacheWeather = 0;
                OutbreakCache = new();
                BCATOutbreakCache = new();
                LoadOutbreakCache();
                LoadBCATOutbreakCache();
            }
            await Click(A, HasReset ? 9_000 : 7_000, token).ConfigureAwait(false);
            if (HasReset)
                Invoke(() => Reset.Checked = false);
            Invoke(() => Teleport = TeleportMode.Checked);
            if (Teleport)
                await Click(MINUS, 2_000, token).ConfigureAwait(false);
            Overworld = await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false);
            if (!Overworld)
            {
                ResetTaskStatus();
                ScanTask = Task.Run(async () =>
                {
                    await Reposition(token, true).ConfigureAwait(false);
                    CollideTaskComplete = true;
                    while (!OverworldTaskComplete)
                        await Task.Delay(0_010, token).ConfigureAwait(false);
                });
                OverworldTask = RecoverToOverworld(token);
                await Task.WhenAny(ScanTask, OverworldTask).ConfigureAwait(false);
                if (ScanTask.IsFaulted || OverworldTask.IsFaulted)
                    throw new Exception($"{(ScanTask.IsFaulted ? ScanTask.Exception!.InnerException!.ToString() : OverworldTask.Exception!.InnerException!.ToString())}");
                if (ScanTask.IsCanceled || OverworldTask.IsCanceled)
                    throw new OperationCanceledException();
                await OpenPicnic(token).ConfigureAwait(false);
            }

        }
        private async Task ClosePicnic(CancellationToken token)
        {
            for (int i = 0; i < 3; i++)
                await Click(Y, 0_500, token).ConfigureAwait(false);
            if(!await IsOnOverworld(OverWorldOffset, token).ConfigureAwait(false))
            {
                OverworldEvent.Set();
                return;
            }
            OverworldEvent.Set();
            ScanEvent.WaitOne();
            await Click(A, 1_000, token).ConfigureAwait(false);
            for (int i = 0; i < 8; i++)
                await Click(A, 0_450, token).ConfigureAwait(false);
            OverworldEvent.Set();
        }
        private bool TargetCoordCheck()
        {
            if (coordList.Count <= 0)
            {
                MessageBox.Show("Coord List is Empty!");
                return false;
            }
            var coords = coordList[(int)TeleportIndex.Value];
            if (coords == null)
            {
                MessageBox.Show("Target Coord is null!");
                return false;
            }
            var x = BitConverter.ToSingle(coords, 0);
            var y = BitConverter.ToSingle(coords, 4);
            var z = BitConverter.ToSingle(coords, 8);
            if (x == 0 || y == 0 || z == 0)
            {
                MessageBox.Show("Target Coord is Invalid!");
                return false;
            }
            return true;
        }
        private async void TeleportButton_Click(object sender, EventArgs e)
        {
            bool CoordisValid = TargetCoordCheck();
            if (!CoordisValid)
                return;
            DisableOptions();
            ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected}";
            canceled = false;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    if (Reset.Checked)
                    {
                        await InitilizeSessionOffsets(token).ConfigureAwait(false);
                        Reset.Checked = false;
                    }
                    await TeleportToMatch(coordList[(int)TeleportIndex.Value], token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show(this, "Process has been canceled!", "Cancel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    ConnectionBox.Text = $"Switch Connection Connected: {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected: {Executor.Connection.Connected} ";
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
                    ConnectionBox.Text += $"{Environment.NewLine}Switch Connection Connected(Updated): {Executor.SwitchConnection.Connected}{Environment.NewLine}Console Connection Connected(Updated): {Executor.Connection.Connected} ";
                    MessageBox.Show(this, ex.ToString(), "Exception Occured!");
                }
            }
            canceled = true;
            EnableOptions();
        }
        private void TeleportMode_CheckedChanged(object sender, EventArgs e)
        {
            if (TeleportMode.Checked && PicnicReset.Checked)
                PicnicReset.Checked = false;
        }

        private void PicnicReset_CheckedChanged(object sender, EventArgs e)
        {
            if (PicnicReset.Checked && TeleportMode.Checked)
                TeleportMode.Checked = false;
            if (PicnicReset.Checked && InWater.Checked)
                InWater.Checked = false;
        }
        private void InWater_CheckedChanged(object sender, EventArgs e)
        {
            if (InWater.Checked && PicnicReset.Checked)
                PicnicReset.Checked = false;
        }
        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("This will restart the application. Do you wish to continue?", "Hard Stop Initiated", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                try
                {
                    if (Executor.SwitchConnection.Connected)
                        Executor.Disconnect();
                }
                catch (SocketException ex)
                {
                    MessageBox.Show(this, ex.ToString(), "Soket Exception!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                System.Windows.Forms.Application.Restart();
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
            }
        }
        
        private async Task<(byte[], ulong)> ReadBlock(DataBlock block, ulong init, CancellationToken token)
        {
            return await ReadEncryptedBlockObject(block, init, token).ConfigureAwait(false);
        }

        private async Task SetStick(SwitchStick stick, short x, short y, int delay, CancellationToken token)
        {
            RefreshConnection();

            var cmd = SwitchCommand.SetStick(stick, x, y, true);
            await Executor.SwitchConnection.SendAsync(cmd, token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            RefreshConnection();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x11;
        }
        private async Task<byte> GetOverworldState(ulong offset, CancellationToken token)
        {
            RefreshConnection();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0];
        }

        private async Task<bool> ReadEncryptedBlockBool(DataBlock block, CancellationToken token)
        {
            var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
            address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data);
            return res[0] == 2;
        }
        private async Task<(byte[], ulong)> ReadEncryptedBlockObject(DataBlock block, ulong init, CancellationToken token)
        {
            RefreshConnection();

            if (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 5 + block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data)[5..];

            return (res, init);
        }
        private async Task<(long, ulong)> ReadEncryptedBlockInt64(DataBlock block, ulong init, CancellationToken token)
        {
            RefreshConnection();

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
        public async Task<ulong> SearchSaveKey(uint key, CancellationToken token)
        {
            RefreshConnection();

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
        private async Task BoostShinyRolls(CancellationToken token)
        {
            bool BoostShinyRolls = false;
            if (InvokeRequired)
                Invoke(() => BoostShinyRolls = ShinyBoost.Checked);
            else
                BoostShinyRolls = ShinyBoost.Checked;
            if (!BoostShinyRolls)
                return;
            // Source: https://gbatemp.net/threads/pokemon-scarlet-violet-cheat-database.621563/

            // Original cheat:
            /*
             *{ShinyRoll + 2}
             * 04000000 00DEF448 11000915
             * 04000000 00DEF480 52800028
             * 04000000 00DEF484 510006B5
             * 04000000 00DEF488 6B15011F
             * 04000000 00DEFCB8 11000917
             * 04000000 00DEFCF4 52800028
             * 04000000 00DEFCF8 510006F7
             * 04000000 00DEFCFC 6B17011F
             */

            RefreshConnection();

            LogUtil.LogText("Applying Shiny Roll Boost Cheat...");
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x11000915), 0x00DEF448, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x00DEF480, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x510006B5), 0x00DEF484, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x6B15011F), 0x00DEF488, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x11000917), 0x00DEFCB8, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x00DEFCF4, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x510006F7), 0x00DEFCF8, token);
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x6B17011F), 0x00DEFCFC, token);
        }
        private void StopConditionsButton_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new(setcolors, ref encounterFilters, filtermode);
            miniform.ShowDialog();
        }
        public void RefreshConnection()
        {

            if (!Executor.SwitchConnection.Connected ||!Executor.Connection.Connected)
            {
                Executor.SwitchConnection.Reset();
                ReConnectCount++;
                string msg = $"ReConnect Count: {ReConnectCount}";
                if (!Executor.Connection.Connected)
                    msg += $"{Environment.NewLine}Connection issue: Executor.Connection is not Connected!";
                if (InvokeRequired)
                    Invoke(() => ConnectionBox.Text = $"{msg}");
                else
                    ConnectionBox.Text = $"{msg}";
            }

        }
        public void DisableOptions()
        {
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    InWater.Enabled = false;
                    SandwichMode.Enabled = false;
                    PicnicReset.Enabled = false;
                    NonSaveMode.Enabled = false;
                    EatOnStart.Enabled = false;
                    Reset.Enabled = false;
                    checkBox6.Enabled = false;
                    checkBox7.Enabled = false;
                    TeleportMode.Enabled = false;
                    ScanLocationCannotPicnic.Enabled = false;
                    OutbreakType.Enabled = false;
                    TimeCombo.Enabled = false;
                    if (OutbreakSpeciesBox.Enabled)
                        OutbreakSpeciesBox.Enabled = false;
                    if (FormCombo.Visible && FormCombo.Enabled)
                        FormCombo.Enabled = false;
                    poketype.Enabled = false;
                    ScanButton.Enabled = false;
                    OutbreakResetButton.Enabled = false;
                    SetTimeButton.Enabled = false;
                    if (OutbreakText.Enabled)
                        OutbreakText.Enabled = false;
                    XCoord.Enabled = false;
                    YCoord.Enabled = false;
                    ZCoord.Enabled = false;
                    Language.Enabled = false;
                    DateReset.Enabled = false;
                    TeleportButton.Enabled = false;
                    TeleportIndex.Enabled = false;
                    TimeText.Enabled = false;
                    ShinyBoost.Enabled = false;
                    AutoReConnet.Enabled = false;
                    MapCheck.Enabled = false;
                    TimeZoneCombo.Enabled = false;
                    LastDateCombo.Enabled = false;
                });
            }
            else
            {
                InWater.Enabled = false;
                SandwichMode.Enabled = false;
                PicnicReset.Enabled = false;
                NonSaveMode.Enabled = false;
                EatOnStart.Enabled = false;
                Reset.Enabled = false;
                checkBox6.Enabled = false;
                checkBox7.Enabled = false;
                TeleportMode.Enabled = false;
                ScanLocationCannotPicnic.Enabled = false;
                OutbreakType.Enabled = false;
                TimeCombo.Enabled = false;
                if (OutbreakSpeciesBox.Enabled)
                    OutbreakSpeciesBox.Enabled = false;
                if (FormCombo.Visible && FormCombo.Enabled)
                    FormCombo.Enabled = false;
                poketype.Enabled = false;
                ScanButton.Enabled = false;
                OutbreakResetButton.Enabled = false;
                SetTimeButton.Enabled = false;
                if (OutbreakText.Enabled)
                    OutbreakText.Enabled = false;
                XCoord.Enabled = false;
                YCoord.Enabled = false;
                ZCoord.Enabled = false;
                Language.Enabled = false;
                DateReset.Enabled = false;
                TeleportButton.Enabled = false;
                TeleportIndex.Enabled = false;
                TimeText.Enabled = false;
                ShinyBoost.Enabled = false;
                AutoReConnet.Enabled = false;
                MapCheck.Enabled = false;
                TimeZoneCombo.Enabled = false;
                LastDateCombo.Enabled = false;
            }
        }

        public void EnableOptions()
        {
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    InWater.Enabled = true;
                    SandwichMode.Enabled = true;
                    PicnicReset.Enabled = true;
                    NonSaveMode.Enabled = true;
                    EatOnStart.Enabled = true;
                    Reset.Enabled = true;
                    checkBox6.Enabled = true;
                    checkBox7.Enabled = true;
                    TeleportMode.Enabled = true;
                    ScanLocationCannotPicnic.Enabled = true;
                    OutbreakType.Enabled = true;
                    TimeCombo.Enabled = true;
                    poketype.Enabled = true;
                    ScanButton.Enabled = true;
                    OutbreakResetButton.Enabled = true;
                    SetTimeButton.Enabled = true;
                    XCoord.Enabled = true;
                    YCoord.Enabled = true;
                    ZCoord.Enabled = true;
                    Language.Enabled = true;
                    DateReset.Enabled = true;
                    OutbreakSpeciesBox.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                    OutbreakText.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                    if (FormCombo.Visible)
                        FormCombo.Enabled = OutbreakSpeciesBox.Enabled;
                    TeleportButton.Enabled = coordList.Count > 0;
                    TeleportIndex.Enabled = true;
                    TimeText.Enabled = true;
                    ShinyBoost.Enabled = true;
                    AutoReConnet.Enabled = true;
                    MapCheck.Enabled = true;
                    TimeZoneCombo.Enabled = true;
                    LastDateCombo.Enabled = true;
                });
            }
            else
            {
                InWater.Enabled = true;
                SandwichMode.Enabled = true;
                PicnicReset.Enabled = true;
                NonSaveMode.Enabled = true;
                EatOnStart.Enabled = true;
                Reset.Enabled = true;
                checkBox6.Enabled = true;
                checkBox7.Enabled = true;
                TeleportMode.Enabled = true;
                ScanLocationCannotPicnic.Enabled = true;
                OutbreakType.Enabled = true;
                TimeCombo.Enabled = true;
                poketype.Enabled = true;
                ScanButton.Enabled = true;
                OutbreakResetButton.Enabled = true;
                SetTimeButton.Enabled = true;
                XCoord.Enabled = true;
                YCoord.Enabled = true;
                ZCoord.Enabled = true;
                Language.Enabled = true;
                DateReset.Enabled = true;
                OutbreakSpeciesBox.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                OutbreakText.Enabled = OutbreakType.SelectedIndex > 0 || TimeCombo.SelectedIndex > 0;
                if (FormCombo.Visible)
                    FormCombo.Enabled = OutbreakSpeciesBox.Enabled;
                TeleportButton.Enabled = coordList.Count > 0;
                TeleportIndex.Enabled = true;
                TimeText.Enabled = true;
                ShinyBoost.Enabled = true;
                AutoReConnet.Enabled = true;
                MapCheck.Enabled = true;
                TimeZoneCombo.Enabled = true;
                LastDateCombo.Enabled = true;
            }
        }
    }
}
