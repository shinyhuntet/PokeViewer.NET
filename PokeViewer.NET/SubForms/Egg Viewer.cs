using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static PokeViewer.NET.RoutineExecutor;
using PKHeX.Drawing.PokeSprite;
using Newtonsoft.Json;
using System.Text;
using PokeViewer.NET.Properties;
using PokeViewer.NET.Misc;
using System.Net.Sockets;
using System.Diagnostics;
using PokeViewer.NET.Util;

namespace PokeViewer.NET.SubForms
{
    public partial class Egg_Viewer : Form
    {
        private readonly ViewerState Executor;
        public int GameType;
        private List<EncounterFilter> encounterFilters = new();
        private FilterMode filtermode;
        private readonly SimpleTrainerInfo TrainerInfo;
        public Egg_Viewer(int gametype, ref ViewerState executor, (Color, Color) color, SimpleTrainerInfo trainer)
        {
            filtermode = FilterMode.Egg;
            InitializeComponent();
            Executor = executor;
            itemStructure = new(executor);
            GameType = gametype;
            Language.DataSource = Enum.GetValues(typeof(LanguageID)).Cast<LanguageID>().Where(z => z != LanguageID.None && z != LanguageID.UNUSED_6).ToArray();
            Language.SelectedIndex = 1;
            Strings = GameInfo.GetStrings(Language.SelectedIndex);
            FillingHoldTime.Text = Settings.Default.FillingHoldTime;
            var filterpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{filtermode}filters.json");
            if (File.Exists(filterpath))
                encounterFilters = System.Text.Json.JsonSerializer.Deserialize<List<EncounterFilter>>(File.ReadAllText(filterpath)) ?? new List<EncounterFilter>();
            SetColors(color);
            TrainerInfo = trainer;
        }
        private (Color, Color) setcolors;
        protected ViewerOffsets Offsets { get; } = new();
        private readonly ItemStructure itemStructure;
        private GameStrings Strings;
        private int eggcount = 0;
        private int basketcount = 0;
        private int sandwichcount = 0;
        private int starcount = 0;
        private int squarecount = 0;
        private PK9 pkprev = new();
        public int BlankVal = 0x01;
        private int[] IVFiltersMax = Array.Empty<int>();
        private int[] IVFiltersMin = Array.Empty<int>();
        private ulong OverworldOffset = 0;
        private ulong PlayerCanMoveOffset = 0;
        private DateTime StartTime;
        private CancellationTokenSource? cts = null;
        private System.Timers.Timer timer = new System.Timers.Timer { Interval = 1000 };
        private static readonly PK9 Blank = new();
        private string SpriteUrl = string.Empty;
        private List<uint> ECList = [];
        private List<Species> SpeciesResults = new();
        private List<Image> SpriteResults = new();
        private ulong BoxStartOffset;
        private ulong CurrentBoxIndexOffset;
        public ulong ItemOffset;
        private ulong CurrentBoxOffset;
        private int BoxSlotSize = 0x158;
        private bool ReadSlot = false;
        private byte CurrentBox = 0;
        private int CurrentBoxSlot = 0;
        private bool MasudaMethod = false;
        private bool ForceCancel = false;
        public List<(int, int)> Ingredients = [];
        public List<(int, int)> Condiments = [];
        public List<(int, int, int, bool)> EatItem1 = [];
        public List<(int, int, int, bool)> EatItem2 = [];
        private List<int> MiddleItem = [1909, 1910, 1920, 1921, 1922, 1923, 1924, 1926, 1927, 1928, 1938, 1940];
        private List<int> BigItems = [1925, 1930, 1931, 1932, 1933, 1934];
        
        private void SetColors((Color, Color) color)
        {
            BackColor = color.Item1;
            ForeColor = color.Item2;
            FetchButton.BackColor = color.Item1;
            FetchButton.ForeColor = color.Item2;
            HardStopButton.BackColor = color.Item1;
            HardStopButton.ForeColor = color.Item2;
            StopConditionsButton.BackColor = color.Item1;
            StopConditionsButton.ForeColor = color.Item2;
            WatchEggButton.BackColor = color.Item1;
            WatchEggButton.ForeColor = color.Item2;
            PokeStats.BackColor = color.Item1;
            PokeStats.ForeColor = color.Item2;
            PokeStats2.BackColor = color.Item1;
            PokeStats2.ForeColor = color.Item2;
            PokeStats3.BackColor = color.Item1;
            PokeStats3.ForeColor = color.Item2;
            PokeStats4.BackColor = color.Item1;
            PokeStats4.ForeColor = color.Item2;
            PokeStats5.BackColor = color.Item1;
            PokeStats5.ForeColor = color.Item2;
            PokeStats6.BackColor = color.Item1;
            PokeStats6.ForeColor = color.Item2;
            PokeStats7.BackColor = color.Item1;
            PokeStats7.ForeColor = color.Item2;
            PokeStats8.BackColor = color.Item1;
            PokeStats8.ForeColor = color.Item2;
            PokeStats9.BackColor = color.Item1;
            PokeStats9.ForeColor = color.Item2;
            PokeStats10.BackColor = color.Item1;
            PokeStats10.ForeColor = color.Item2;
            PokeStats11.BackColor = color.Item1;
            PokeStats11.ForeColor = color.Item2;
            PokeStats12.BackColor = color.Item1;
            PokeStats12.ForeColor = color.Item2;
            NumericWaitTime.BackColor = color.Item1;
            NumericWaitTime.ForeColor = color.Item2;
            setcolors = color;
        }
        private bool SanityCheck(PKM pk, int count)
        {
            PictureBox[] boxes = { pictureBox1, pictureBox2, pictureBox3, pictureBox4, pictureBox5, pictureBox6 };
            for (int i = 0; i < 6 && count == 0; i++)
            {
                if (boxes[i].Image is not null || boxes[i].Image != null)
                    boxes[i].Image = null;
            }
            bool isValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
            string? sprite;
            if (!isValid || pk.Species <= 0 || pk.Species > (int)Species.MAX_COUNT)
            {
                sprite = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/party{count + 1}.png";
                boxes[count].Load(sprite);
                boxes[count].SizeMode = PictureBoxSizeMode.CenterImage;
                return false;
            }
            try
            {
                sprite = PokeImg(pk, false);
            }
            catch
            {
                sprite = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/starter.png";
            }
            boxes[count].Load(sprite);
            boxes[count].SizeMode |= PictureBoxSizeMode.Zoom;
            return true;
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            var success = true;
            ForceCancel = false;
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    Strings = GameInfo.GetStrings(Language.SelectedIndex);
                    StartTime = DateTime.Now;
                    UptimeOnLoad(sender, e);
                    await PerformEggRoutine(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {                    
                    success = false;
                    timer.Stop();
                    MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex, 19)}");
                    EnableOptions();
                }
                catch (Exception ex)
                {
                    success = false;
                    timer.Stop();
                    if (Executor.SwitchConnection.Connected)
                    {
                        try
                        {
                            Executor.SwitchConnection.Disconnect();
                        }
                        catch (SocketException soketEx)
                        {
                            AutoReConnect.Checked = false;
                            MessageBox.Show(this, soketEx.ToString(), "Sokect Connetion Exception!");
                        }
                    }
                    if(AutoReConnect.Checked)
                    {
                        try
                        {
                            await Task.Delay(300_000, token).ConfigureAwait(false);
                            Executor.SwitchConnection.Reset();
                            if (!Executor.SwitchConnection.Connected)
                                throw new Exception("SwitchConnection can't reconnect!");
                            await ClosePicnic(token).ConfigureAwait(false);
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
                    EnableOptions();
                    MessageBox.Show(this, $"{ex}");
                }

                ForceCancel = true;
                if (Executor.SwitchConnection.Connected)
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                if (!cts.IsCancellationRequested && cts != null && success)
                    MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex < 0 ? 1 : Language.SelectedIndex, 20)}", "Process Finish", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        private void UptimeOnLoad(object sender, EventArgs e)
        {
            timer = new System.Timers.Timer { Interval = 1000 };
            timer.Elapsed += (o, args) =>
            {
                UptimeLabel.Text = $"{ChangeLanguageString(Language.SelectedIndex, 21)}: {StartTime - DateTime.Now:d\\.hh\\:mm\\:ss}";
            };
            timer.Start();
        }
        private async Task<(bool, ulong, byte, int)> ReadEmptySlot(bool ReadSlot, byte CurrentBox, int CurrentSlot, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var BoxSize = 30 * BoxSlotSize;
            var wait = 0;
            while (CurrentBoxIndexOffset == 0)
            {
                await Task.Delay(0_050, token).ConfigureAwait(false);
                wait++;
                CurrentBoxIndexOffset = await Executor.SwitchConnection.PointerAll(Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
                if (wait >= 90)
                    break;
            }
            wait = 0;
            while (BoxStartOffset == 0)
            {
                await Task.Delay(0_050, token).ConfigureAwait(false);
                wait++;
                BoxStartOffset = await Executor.SwitchConnection.PointerAll(Offsets.BoxStartPointer, token).ConfigureAwait(false);
                if (wait >= 90)
                    return (false, 0, 0, 0);
            }
            if (!ReadSlot)
            {
                var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(CurrentBoxIndexOffset, 1, token).ConfigureAwait(false);
                CurrentBox = data[0];
                CurrentSlot = 0;
            }
            while (CurrentBox <= 31)
            {
                var boxstart = BoxStartOffset + (ulong)(CurrentBox * BoxSize) + (ulong)(CurrentSlot * BoxSlotSize);
                var pokedata = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(boxstart, BoxSlotSize, token).ConfigureAwait(false);
                var pkm = new PK9(pokedata);
                bool isValid = PersonalTable.SV.IsPresentInGame(pkm.Species, pkm.Form);
                if (!isValid || pkm == null || !pkm.ChecksumValid || !pkm.Valid || pkm.Species <= 0 || (Species)pkm.Species > Species.MAX_COUNT)
                    return (true, boxstart, CurrentBox, CurrentSlot);
                CurrentSlot++;
                if (CurrentSlot == 30)
                {
                    CurrentBox++;
                    CurrentSlot = 0;
                }
            }
            return (false, 0, 0, 0);
        }
        private async Task SetupBoxState(CancellationToken token)
        {
            var existing = await ReadBoxPokemonSV(BoxStartOffset, 0x158, token).ConfigureAwait(false);
            bool isValid = PersonalTable.SV.IsPresentInGame(existing.Species, existing.Form);
            if (isValid && existing.Species > 0 && (Species)existing.Species <= Species.MAX_COUNT && existing.ChecksumValid && existing.Valid)
                ViewerUtil.DumpPokemon(AppDomain.CurrentDomain.BaseDirectory, "saved", existing);

            await SetBoxPokemonEgg(Blank, BoxStartOffset, token).ConfigureAwait(false);
        }

        private async Task SetCurrentBox(byte box, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.PointerPoke(new[] { box }, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
        }

        public async Task SetBoxPokemonEgg(PK9 pkm, ulong ofs, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            pkm.ResetPartyStats();
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, ofs, token).ConfigureAwait(false);
        }
        private async Task<(bool, PK9)> RetrieveEgg(CancellationToken token)
        {
            bool match = false;
            for (int a = 0; a < ((LanguageID)TrainerInfo.Language == LanguageID.Japanese ? 5 : 4); a++)
                await Click(A, 1_500, token).ConfigureAwait(false);

            await Task.Delay(0_500, token).ConfigureAwait(false);
            PK9 pk = await ReadBoxPokemonSV(BoxStartOffset, 344, token).ConfigureAwait(false);
            if ((Species)pk.Species != Species.None)
            {
                BasketCount.Text = "Status: Egg found!";
                eggcount++;
                for (int i = 0; i < encounterFilters.Count; i++)
                {
                    LogUtil.LogText($"{Environment.NewLine}Filter Name: {encounterFilters[i].Name}, " + $"encfilter enabled:{encounterFilters[i].Enabled}{Environment.NewLine}");
                    if (!encounterFilters[i].Enabled)
                        continue;

                    match = ValidateEncounter(pk, encounterFilters[i]);
                    LogUtil.LogText($"{Environment.NewLine}Filter {encounterFilters[i].Name} Satisfied is {match}{Environment.NewLine}");
                    if (match)
                        break;
                }
                if (ForceDumpCheck.Checked)
                    ViewerUtil.DumpPokemon(AppDomain.CurrentDomain.BaseDirectory, "eggs", pk);
                await Click(MINUS, 1_500, token).ConfigureAwait(false);
                for (int a = 0; a < 2; a++)
                    await Click(A, 1_500, token).ConfigureAwait(false);
                return (match, pk);
            }

            else
            {
                BasketCount.Text = "Status: No egg :(";
                for (int a = 0; a < 2; a++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
            }

            return (match, pk);
        }
        private async Task<(bool, PK9)> CollectEgg(CancellationToken token)
        {
            bool match = false;
            PK9 pk = new();
            for (int a = 0; a <((LanguageID)TrainerInfo.Language == LanguageID.Japanese ? 4 : 3); a++)
                await Click(A, 1_500, token).ConfigureAwait(false);

            await Task.Delay(0_500, token).ConfigureAwait(false);
            pk = await ReadBoxPokemonSV(CurrentBoxOffset, 344, token).ConfigureAwait(false);
            var isValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
            if (isValid && (Species)pk.Species <= Species.MAX_COUNT && (Species)pk.Species > Species.None && pk.ChecksumValid && pk.Valid)
            {
                BasketCount.Text = "Status: Egg found!";
                for (int i = 0; i < encounterFilters.Count; i++)
                {
                    LogUtil.LogText($"{Environment.NewLine}Filter Name: {encounterFilters[i].Name}, " + $"encfilter enabled:{encounterFilters[i].Enabled}{Environment.NewLine}");
                    if (!encounterFilters[i].Enabled)
                        continue;

                    match = ValidateEncounter(pk, encounterFilters[i]);
                    LogUtil.LogText($"{Environment.NewLine}Filter {encounterFilters[i].Name} Satisfied is {match}{Environment.NewLine}");
                    if (match)
                        break;
                }
                await Click(MINUS, 1_500, token).ConfigureAwait(false);
                await Click(A, 1_500, token).ConfigureAwait(false);
                return (match, pk);
            }

            else
            {
                BasketCount.Text = "Status: No egg :(";
                for (int a = 0; a < 2; a++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
            }

            return (match, pk);
        }
        public void Initialize(TextBox[] text, NumericUpDown[] Num)
        {
            for(int i = 0; i < text.Length; i++)
            {
                text[i].Text = string.Empty;
                Num[i].Value = 0;
            }
        }
        private async Task PerformEggRoutine(CancellationToken token)
        {
            if (encounterFilters.Count == 0 || encounterFilters.All(x => !x.Enabled))
            {
                MessageBox.Show("No Filter Active!");
                return;
            }

            if (FillingHoldTime.Text != string.Empty && !int.TryParse(FillingHoldTime.Text, out _))
            {
                MessageBox.Show("Enter number in FillingHoldTime!", "FillingHoldTime Value Error");
                return;
            }
            
            if (FetchButton.Enabled)
                DisableOptions();

            await InitializeSessionOffsets(token).ConfigureAwait(false);
            if (!SkipCheck.Checked || (EatItem1.Count <= 0 || EatItem2.Count <= 0))
            {
                TextBox[] ItemList = { Item1Value, Item2Value, Item3Value, Item4Value, Item5Value, Item6Value };
                NumericUpDown[] CountList = { Item1Count, Item2Count, Item3Count, Item4Count, Item5Count, Item6Count };
                Initialize(ItemList, CountList);
                EatItem1 = [];
                EatItem2 = [];
                var pouch = await itemStructure.GetPouches(token).ConfigureAwait(false);
                var bag = itemStructure.GetItems(pouch);
                Ingredients = itemStructure.GrabItems(bag);
                Condiments = itemStructure.GrabCondiments(bag);
                var success = itemStructure.SelectItem(Ingredients, ref EatItem1, 1923);
                if (!success)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, "Ingredients are out of stock!", "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableOptions();
                    return;
                }
                success = itemStructure.SelectCondiments(Condiments, ref EatItem2, 1904, 2);
                if (!success)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, "Condiments are out of stock!", "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableOptions();
                    return;
                }
                EatItem1.OrderBy(z => z.Item3);
                EatItem2.OrderBy(z => z.Item3);
                for (int i = 0; i < EatItem1.Count; i++)
                {
                    if (i >= 3)
                        break;

                    ItemList[i].Text = EatItem1[i].Item3.ToString();
                    CountList[i].Value = EatItem1[i].Item2;
                }
                for (int i = 0; i < EatItem2.Count; i++)
                {
                    if (i + 3 >= ItemList.Length) break;

                    ItemList[i + 3].Text = EatItem2[i].Item3.ToString();
                    CountList[i + 3].Value = EatItem2[i].Item2;
                }
            }
            if (checkBox12.Checked || ForceMasudaMethod.Checked)
                await GatherPokeParty(token).ConfigureAwait(false);
            if (ForceMasudaMethod.Checked && MasudaMethod)
                ForceMasudaMethod.Checked = false;
            if (ForceShinyCharm.Checked && await itemStructure.HasShinyCharm(token).ConfigureAwait(false))
                ForceShinyCharm.Checked = false;
            eggcount = 0;
            BlankVal = await PicnicState(token).ConfigureAwait(false);

            if(ForceMasudaMethod.Checked)
                await EnableForceMasudaMethodCheat(token).ConfigureAwait(false);
            if(ForceShinyCharm.Checked)
                await EnableForceShinyCharmCheat(token).ConfigureAwait(false);

            if(!CollectEggsCheck.Checked)
                await OpenPicnic(token).ConfigureAwait(false);
            if (checkBox9.Checked)
            {
                await Task.Delay(checkBox8.Checked ? 8_000 : 7_000).ConfigureAwait(false);
                var Check = await OnlyMakeSandwich(token).ConfigureAwait(false);
                if (!Check.Item1)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableOptions();
                    return;
                }
                await ClosePicnic(token).ConfigureAwait(false);
                timer.Stop();
                MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex, 18)}");
                EnableOptions();
                Activate();
                return;
            }

            if (CollectEggsCheck.Checked)
            {
                await SetupBoxState(token).ConfigureAwait(false);
                await SetCurrentBox(0, token).ConfigureAwait(false);
                if (EatOnStart.Checked)
                {
                    var Check = await OnlyMakeSandwich(token).ConfigureAwait(false);
                    if (!Check.Item1)
                    {
                        await Click(HOME, 0_500, token).ConfigureAwait(false);
                        timer.Stop();
                        MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        EnableOptions();
                        return;
                    }
                }
                await WaitAndCollectEggs(token).ConfigureAwait(false);
            }
            else if (EatOnStart.Checked)
            {
                await Task.Delay(checkBox8.Checked ? 8_000 : 7_000).ConfigureAwait(false);
                if (checkBox8.Checked)
                    checkBox8.Checked = false;
                var Check = await OnlyMakeSandwich(token).ConfigureAwait(false);
                if (!Check.Item1)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableOptions();
                    return;
                }
                pkprev =  await ReopenPicnic(token).ConfigureAwait(false);
                await WaitForEggs(token).ConfigureAwait(false);
            }
            else
            {
                var count = 7.0;
                if (Executor.UseCRLF)
                    count += 0.5;
                if (checkBox8.Checked)
                {
                    count += 1.0;
                    checkBox8.Checked = false;
                }
                var countstring = count;
                while (count > 0)
                {
                    await Task.Delay(0_500).ConfigureAwait(false);
                    count -= 0.5;
                }
                pkprev = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                GetEggString(countstring, pkprev);
                await WaitForEggs(token).ConfigureAwait(false);
            }
            EnableOptions();
            Activate();
        }
        private async Task EnableForceMasudaMethodCheat(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x1A8813E8), 0x020F070C, token).ConfigureAwait(false);

            /*[Egg Shiny Roll Change(Force Masuda Method)] (v3.0.1)
               it only happnes when parent1lang == parent2lang
               020F070C 1A8813E8*/
        }
        private async Task EnableForceShinyCharmCheat(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x1A880136), 0x020F0718, token).ConfigureAwait(false);

            /*[Egg Shiny Rolls Change(Force shiny Charm)](v3.0.1)
               it only happnes when player doesn't have ShinyCharm
               020F0718 1A880136*/
        }
        private async Task BuildImage(PK9 enc, PictureBox[] Sprites, CancellationToken token, bool collectegg = false)
        {
            try
            {
                SpriteUrl = PokeImg(enc, false);
            }
            catch
            {
                SpriteUrl = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Pokemon%20Sprite%20Overlays/starter.png";
            }
            Image image = null!;
            using (var httpClient = new HttpClient())
            {
                var imageContent = await httpClient.GetByteArrayAsync(SpriteUrl, token);
                using var imageBuffer = new MemoryStream(imageContent);
                image = Image.FromStream(imageBuffer);
            }

            SpriteResults.Add(image);
            SpeciesResults.Add((Species)enc.Species);

            if (!collectegg)
            {
                PokeSpriteBox.Image = image;
                Sprites[(eggcount - 1) % 10].Image = image;
            }
            else
            {
                PokeSpriteBox12.Image = image;
            }
        }
        private async Task WaitAndCollectEggs(CancellationToken token)
        {
            RateBox.Text = string.Empty;
            SpriteUrl = string.Empty;
            PictureBox[] Ball = { BallBox2, BallBox3, BallBox4, BallBox5, BallBox6, BallBox7, BallBox8, BallBox9, BallBox10, BallBox11 };
            PictureBox[] Sprites = { PokeSpriteBox2, PokeSpriteBox3, PokeSpriteBox4, PokeSpriteBox5, PokeSpriteBox6, PokeSpriteBox7, PokeSpriteBox8, PokeSpriteBox9, PokeSpriteBox10, PokeSpriteBox11 };
            TextBox[] Pokestats = { PokeStats2, PokeStats3, PokeStats4, PokeStats5, PokeStats6, PokeStats7, PokeStats8, PokeStats9, PokeStats10, PokeStats11 };
            ECList = [];
            while (!token.IsCancellationRequested)
            {
                if (eggcount % 10 == 0)
                {
                    RefreshComponent(Sprites, Ball, Pokestats);
                    ECList = [];
                }
                var wait = TimeSpan.FromMinutes(31);
                var endTime = DateTime.Now + wait;
                var waiting = 0;
                NextSanwichLabel.Text = $"Next Sandwich: {endTime:hh\\:mm\\:ss}";
                while (DateTime.Now < endTime)
                {
                    if (eggcount % 10 == 0)
                    {
                        RefreshComponent(Sprites, Ball, Pokestats);
                        ECList = [];
                    }
                    BasketCount.Text = "Status: Waiting..";
                    var pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                    bool isValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                    while (!isValid || pk == pkprev || pk == null || pkprev!.EncryptionConstant == pk.EncryptionConstant || !pk.ChecksumValid || !pk.Valid || (Species)pk.Species == Species.None || (Species)pk.Species > Species.MAX_COUNT)
                    {
                        if (DateTime.Now >= endTime)
                            break;

                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                        waiting++;

                        if (waiting == (int)NumericWaitTime.Value)
                        {
                            await Click(B, 1_500, token).ConfigureAwait(false);
                            waiting = 0;
                        }
                    }
                    BasketCount.Text = "Status: Checking..";
                    var (match, enc) = await RetrieveEgg(token).ConfigureAwait(false);

                    if (enc.Species != (ushort)Species.None)
                    {
                        ECList.Add(enc.EncryptionConstant);
                        PokeStats.Text = GetRealEggString(enc);
                        Pokestats[(eggcount - 1) % 10].Text = GetRealEggString(enc);
                        if (SpriteResults.Count is not 0)
                        {
                            if(SpeciesResults.Contains((Species)enc.Species))
                            {
                                if(enc.IsShiny)
                                {
                                    SpriteUrl = PokeImg(enc, false);
                                    PokeSpriteBox.Load(SpriteUrl);
                                    Sprites[(eggcount - 1) % 10].Load(SpriteUrl);
                                }
                                else
                                {
                                    PokeSpriteBox.Image = SpriteResults[SpeciesResults.IndexOf((Species)enc.Species)];
                                    Sprites[(eggcount - 1) % 10].Image = SpriteResults[SpeciesResults.IndexOf((Species)enc.Species)];
                                }
                            }
                            else
                                await BuildImage(enc, Sprites, token).ConfigureAwait(false);
                        }
                        else
                            await BuildImage(enc, Sprites, token).ConfigureAwait(false); 
                        var ballsprite = SpriteUtil.GetBallSprite(enc.Ball);
                        BallBox.Image = ballsprite;
                        Ball[(eggcount - 1) % 10].Image = ballsprite;

                        if (!match)
                        {
                            if (enc.IsShiny)
                            {
                                var shinyurl = PokeImg(enc, false);
                                WebHookUtil.SendDetailNotifications(enc, shinyurl, match, TrainerInfo);
                            }
                        }
                        if (match)
                        {
                            pkprev = enc;
                            await Click(HOME, 0_500, token).ConfigureAwait(false);
                            if (enc.IsShiny)
                            {
                                var shinyurl = PokeImg(enc, false);
                                WebHookUtil.SendDetailNotifications(enc, shinyurl, match, TrainerInfo);
                            }
                            else
                                WebHookUtil.SendDetailNotifications(enc, SpriteUrl, match, TrainerInfo);
                            timer.Stop();
                            MessageBox.Show(this, "Match found! Egg should be first one in your boxes! Double check with BoxView! " +
                                "Make sure to move your match to a different spot from Box 1 Slot 1 or it will be deleted on the next bot start.");
                            return;
                        }
                        if (!match)
                            await SetBoxPokemonEgg(Blank, BoxStartOffset, token).ConfigureAwait(false);

                        pkprev = pk!;
                        waiting = 0;
                    }
                }
                BasketCount.Text = "Status: Time to eat..";
                var Check = await OnlyMakeSandwich(token).ConfigureAwait(false);
                if (!Check.Item1)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
        }
        private async Task WatchAndCollectEggs(CancellationToken token)
        {
            SpriteUrl = string.Empty;
            PictureBox[] Ball = { BallBox2, BallBox3, BallBox4, BallBox5, BallBox6, BallBox7, BallBox8, BallBox9, BallBox10, BallBox11 };
            PictureBox[] Sprites = { PokeSpriteBox2, PokeSpriteBox3, PokeSpriteBox4, PokeSpriteBox5, PokeSpriteBox6, PokeSpriteBox7, PokeSpriteBox8, PokeSpriteBox9, PokeSpriteBox10, PokeSpriteBox11 };
            TextBox[] Pokestats = { PokeStats2, PokeStats3, PokeStats4, PokeStats5, PokeStats6, PokeStats7, PokeStats8, PokeStats9, PokeStats10, PokeStats11 };
            var ctr = CheckComponentindex(Sprites, Ball, Pokestats);
            basketcount = 0;
            bool matchfound = false;
            ReadSlot = false;
            while (!token.IsCancellationRequested && basketcount < ctr)
            {
                (ReadSlot, CurrentBoxOffset, CurrentBox, CurrentBoxSlot) = await ReadEmptySlot(ReadSlot, CurrentBox, CurrentBoxSlot, token).ConfigureAwait(false);
                BasketCount.Text = $"Status: Checking..| Current egg {basketcount}";
                if (basketcount == 0)
                    await Click(A, 1_500, token).ConfigureAwait(false);
                var (match, enc) = await CollectEgg(token).ConfigureAwait(false);
                var isValid = PersonalTable.SV.IsPresentInGame(enc.Species, enc.Form);
                if (isValid && (Species)enc.Species <= Species.MAX_COUNT && (Species)enc.Species > Species.None && enc.ChecksumValid && enc.Valid)
                    basketcount++;
                else
                    break;

                if (enc.Species != (ushort)Species.None)
                {
                    if (ECList.Contains(enc.EncryptionConstant))
                        ECList.Remove(enc.EncryptionConstant);
                    PokeStats12.Text = GetRealEggString(enc, true);
                    if (SpriteResults.Count is not 0)
                    {
                        if (SpeciesResults.Contains((Species)enc.Species))
                        {
                            if (enc.IsShiny)
                            {
                                SpriteUrl = PokeImg(enc, false);
                                PokeSpriteBox12.Load(SpriteUrl);
                            }
                            else
                                PokeSpriteBox12.Image = SpriteResults[SpeciesResults.IndexOf((Species)enc.Species)];                            
                        }
                        else
                            await BuildImage(enc, Sprites, token, true).ConfigureAwait(false);
                    }
                    else
                        await BuildImage(enc, Sprites, token, true).ConfigureAwait(false);
                    var ballsprite = SpriteUtil.GetBallSprite(enc.Ball);
                    BallBox12.Image = ballsprite;
                    
                    if (!match)
                    {
                        if (enc.IsShiny)
                        {
                            var shinyurl = PokeImg(enc, false);
                            WebHookUtil.SendDetailNotifications(enc, shinyurl, match, TrainerInfo);
                        }
                    }
                    if (match)
                    {
                        pkprev = enc;
                        matchfound = true;
                        if (enc.IsShiny)
                        {
                            var shinyurl = PokeImg(enc, false);
                            WebHookUtil.SendDetailNotifications(enc, shinyurl, match, TrainerInfo);;
                        }
                        else
                            WebHookUtil.SendDetailNotifications(enc, SpriteUrl, match, TrainerInfo);
                    }

                }
            }
            await Click(!token.IsCancellationRequested && basketcount == ctr ? A : MINUS, 1_000, token).ConfigureAwait(false);
            await Click(HOME, 0_500, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested && ECList.Count > 0)
                MessageBox.Show(this, "Memory Leak is detected");
            if (!token.IsCancellationRequested && !matchfound)
                MessageBox.Show(this, "Memory Leak is detected, and the target egg is lost. ");
            EnableOptions();
            Activate();            
        }
        private async Task WatchEggs(CancellationToken token)
        {
            PokeSpriteBox12.Image = null;
            BallBox12.Image = null;
            PokeStats12.Text = string.Empty;
            PictureBox[] Ball = { BallBox2, BallBox3, BallBox4, BallBox5, BallBox6, BallBox7, BallBox8, BallBox9, BallBox10, BallBox11 };
            PictureBox[] Sprites = { PokeSpriteBox2, PokeSpriteBox3, PokeSpriteBox4, PokeSpriteBox5, PokeSpriteBox6, PokeSpriteBox7, PokeSpriteBox8, PokeSpriteBox9, PokeSpriteBox10, PokeSpriteBox11 };
            TextBox[] Pokestats = { PokeStats2, PokeStats3, PokeStats4, PokeStats5, PokeStats6, PokeStats7, PokeStats8, PokeStats9, PokeStats10, PokeStats11 };
            var ctr = CheckComponentindex(Sprites, Ball, Pokestats);
            var waiting = 0;
            var leakcount = 0;
            while (!token.IsCancellationRequested)
            {
                BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}";
                var pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                var IsValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                while (!IsValid || pk == null || !pk.ChecksumValid || !pk.Valid || (Species)pk.Species <= Species.None || (Species)pk.Species > Species.MAX_COUNT || pkprev.EncryptionConstant == pk.EncryptionConstant)
                {
                    await Task.Delay(1_500, token).ConfigureAwait(false);
                    pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                    waiting++;
                    if (WatchDeeper.Checked && ctr >= 10 && waiting >= NumericWaitTime.Value)
                    {
                        BasketCount.Text = "Finish Checking Eggs Deeper!";
                        EnableOptions();
                        return;
                    }
                }

                pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                IsValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                while (IsValid && pk != null && pk.ChecksumValid && pk.Valid && (Species)pk.Species > Species.None && (Species)pk.Species <= Species.MAX_COUNT && pkprev.EncryptionConstant != pk.EncryptionConstant)
                {
                    ctr++;
                    eggcount++;
                    BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex , 12)}: {ctr}";
                    var output = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}{Environment.NewLine}" + GetRealEggString(pk);
                    PokeStats12.Text = output;
                    output = $"{Environment.NewLine}" + PokeStats12.Text;
                    LogUtil.LogText(output);
                    var sprite = PokeImg(pk, false);
                    PokeSpriteBox12.Load(sprite);
                    var ballsprite = SpriteUtil.GetBallSprite(pk.Ball);
                    BallBox12.Image = ballsprite;
                    if (ctr >= 1 && ctr <= 10)
                    {
                        Pokestats[ctr - 1].Text = PokeStats12.Text;
                        Sprites[ctr - 1].Load(sprite);
                        Ball[ctr - 1].Image = ballsprite;
                    }
                    if (ctr > 10 && WatchDeeper.Checked)
                    {
                        leakcount++;
                        NextSanwichLabel.Text = $"Memory Leaked Egg Count: {leakcount}";
                        CheckMemoryLeak(Sprites, Ball, Pokestats, ctr);
                    }

                    await Task.Delay(0_500, token).ConfigureAwait(false);

                    pkprev = pk;
                }
                if (ctr == 10 && !WatchDeeper.Checked)
                {
                    BasketCount.Text = "Finish Watching egg completely!";
                    EnableOptions();
                    return;
                }
            }
        }

        private async Task WaitForEggs(CancellationToken token)
        {
            RateBox.Text = string.Empty;
            PokeSpriteBox12.Image = null;
            BallBox12.Image = null;
            PokeStats12.Text = string.Empty;
            PictureBox[] Ball = { BallBox2, BallBox3, BallBox4, BallBox5, BallBox6, BallBox7, BallBox8, BallBox9, BallBox10, BallBox11 };
            PictureBox[] Sprites = { PokeSpriteBox2, PokeSpriteBox3, PokeSpriteBox4, PokeSpriteBox5, PokeSpriteBox6, PokeSpriteBox7, PokeSpriteBox8, PokeSpriteBox9, PokeSpriteBox10, PokeSpriteBox11 };
            TextBox[] Pokestats = { PokeStats2, PokeStats3, PokeStats4, PokeStats5, PokeStats6, PokeStats7, PokeStats8, PokeStats9, PokeStats10, PokeStats11 };
            RefreshComponent(Sprites, Ball, Pokestats);
            var clock = DateTime.Now;
            ECList = [];
            while (!token.IsCancellationRequested)
            {
                var wait = TimeSpan.FromMinutes(30);
                var endTime = DateTime.Now + wait;
                var ctr = 0;
                var waiting = 0;
                BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}";
                while (DateTime.Now < endTime)
                {
                    NextSanwichLabel.Text = $"{ChangeLanguageString(Language.SelectedIndex, 15)}: {endTime:hh\\:mm\\:ss}";
                    var pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                    var IsValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                    while (!IsValid || pk == null || !pk.ChecksumValid || !pk.Valid || (Species)pk.Species <= Species.None || (Species)pk.Species > Species.MAX_COUNT || pkprev.EncryptionConstant == pk.EncryptionConstant)
                    {
                        if (DateTime.Now >= endTime)
                            break;
                        waiting++;
                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                        IsValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                        if (waiting == (int)NumericWaitTime.Value)
                        {
                            if (DateTime.Now - clock >= TimeSpan.FromMinutes(45) && WatchDeeper.Checked)
                            {
                                LogUtil.LogText($"{Environment.NewLine}45{ChangeLanguageString(Language.SelectedIndex, 38)}..");
                                BasketCount.Text = $"45{ChangeLanguageString(Language.SelectedIndex, 38)}..";
                                await RecoveryReset(token).ConfigureAwait(false);
                                clock = DateTime.Now;
                            }
                            else
                            {
                                LogUtil.LogText($"3{ChangeLanguageString(Language.SelectedIndex, 16)}");
                                BasketCount.Text = $"3{ChangeLanguageString(Language.SelectedIndex, 16)}";
                            }
                            var SandwichCheck = await OnlyMakeSandwich(token).ConfigureAwait(false);
                            if (!SandwichCheck.Item1)
                            {
                                await Click(HOME, 0_500, token).ConfigureAwait(false);
                                timer.Stop();
                                MessageBox.Show(this, SandwichCheck.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            RefreshComponent(Sprites, Ball, Pokestats);
                            pkprev = await ReopenPicnic(token).ConfigureAwait(false);
                            wait = TimeSpan.FromMinutes(30);
                            endTime = DateTime.Now + wait;
                            NextSanwichLabel.Text = $"{ChangeLanguageString(Language.SelectedIndex, 15)}: {endTime:hh\\:mm\\:ss}";
                            waiting = 0;
                            ctr = 0;
                            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}";
                            pk = pkprev;
                        }
                    }

                    pk = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
                    IsValid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
                    while (IsValid && pk != null && pk.ChecksumValid && pk.Valid && (Species)pk.Species > Species.None && (Species)pk.Species <= Species.MAX_COUNT && pkprev.EncryptionConstant != pk.EncryptionConstant)
                    {
                        waiting = 0;
                        ctr++;
                        eggcount++;
                        ECList.Add(pk.EncryptionConstant);
                        BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}";
                        var output = GetRealEggString(pk);
                        Pokestats[ctr - 1].Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}{Environment.NewLine}" + output;
                        PokeStats.Text = output;
                        output = $"{Environment.NewLine}" + Pokestats[ctr - 1].Text;
                        LogUtil.LogText(output);
                        var sprite = PokeImg(pk, false);
                        Sprites[ctr - 1].Load(sprite);
                        PokeSpriteBox.Load(sprite);
                        var ballsprite = SpriteUtil.GetBallSprite(pk.Ball);
                        Ball[ctr - 1].Image = ballsprite;
                        BallBox.Image = ballsprite;

                        await Task.Delay(0_500, token).ConfigureAwait(false);
                        bool match = false;
                        for (int i = 0; i < encounterFilters.Count; i++)
                        {
                            LogUtil.LogText($"{Environment.NewLine}Filter Name: {encounterFilters[i].Name}, " + $"encfilter enabled:{encounterFilters[i].Enabled}{Environment.NewLine}");
                            if (!encounterFilters[i].Enabled)
                                continue;

                            match = ValidateEncounter(pk, encounterFilters[i]);
                            LogUtil.LogText($"{Environment.NewLine}Filter {encounterFilters[i].Name} Satisfied is {match}{Environment.NewLine}");
                            if (match)
                                break;
                        }
                        if (!match && pk.IsShiny)
                        {
                            ChangeComponetsColor(Color.LightGoldenrodYellow, (Color.LightSkyBlue, Color.DarkRed), Sprites[ctr - 1], Ball[ctr - 1], Pokestats[ctr - 1]);
                            if (ForceDumpCheck.Checked)
                                ForcifyEgg(pk);
                            WebHookUtil.SendDetailNotifications(pk, sprite, match, TrainerInfo);
                        }
                        if (match)
                        {
                            if (ForceDumpCheck.Checked)
                                ForcifyEgg(pk);
                            pkprev = pk;
                            await Click(HOME, 0_500, token).ConfigureAwait(false);
                            if (pk.IsShiny)
                                ChangeComponetsColor(Color.LightGoldenrodYellow, (Color.LightSkyBlue, Color.DarkRed), Sprites[ctr - 1], Ball[ctr - 1], Pokestats[ctr - 1]);
                            else
                                ChangeComponetsColor(Color.Silver, (Color.LightYellow, Color.DarkBlue), Sprites[ctr - 1], Ball[ctr - 1], Pokestats[ctr - 1]);
                            WebHookUtil.SendDetailNotifications(pk, sprite, match, TrainerInfo);
                            timer.Stop();
                            MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex, 13)}", "Match Found!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        pkprev = pk;
                    }
                    RateBox.Text = $"Encounter: {eggcount}{Environment.NewLine}Target Rate: {(1.00 - Math.Pow(1.00 - StopConditions.CalcRate(encounterFilters), eggcount)) * 100.00 :0.00}%";
                    if (ctr == 10)
                    {
                        ECList = [];
                        BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 14)}..";
                        pkprev = await ReopenPicnic(token).ConfigureAwait(false);
                        ctr = 0;
                        waiting = 0;
                        BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {ctr}";
                        RefreshComponent(Sprites, Ball, Pokestats);
                    }
                }
                if (DateTime.Now - clock >= TimeSpan.FromMinutes(45) && WatchDeeper.Checked)
                {
                    LogUtil.LogText($"{Environment.NewLine}45{ChangeLanguageString(Language.SelectedIndex, 38)}..");
                    BasketCount.Text = $"45{ChangeLanguageString(Language.SelectedIndex, 38)}..";
                    await RecoveryReset(token).ConfigureAwait(false);
                    clock = DateTime.Now;
                }
                var Check = await OnlyMakeSandwich(token).ConfigureAwait(false);
                if (!Check.Item1)
                {
                    await Click(HOME, 0_500, token).ConfigureAwait(false);
                    timer.Stop();
                    MessageBox.Show(this, Check.Item2, "Sandwich Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                RefreshComponent(Sprites, Ball, Pokestats);
                LogUtil.LogText($"{Environment.NewLine}30{ChangeLanguageString(Language.SelectedIndex, 17)}");
                BasketCount.Text = $"30{ChangeLanguageString(Language.SelectedIndex, 17)}";
                pkprev = await ReopenPicnic(token).ConfigureAwait(false);
            }
        }
        private void ChangeComponetsColor(Color targetcolors1, (Color, Color) targetcolors2, PictureBox sprite, PictureBox ball, TextBox pokestat)
        {
            sprite.BackColor = targetcolors1;
            ball.BackColor = targetcolors1;
            pokestat.BackColor = targetcolors2.Item1;
            pokestat.ForeColor = targetcolors2.Item2;
        }

        private void RefreshComponent(PictureBox[] Sprites, PictureBox[] Ball, TextBox[] Pokestats)
        {
            for (int i = 0; i < 10; i++)
            {
                Sprites[i].Image = null;
                Sprites[i].BackColor = SystemColors.GradientInactiveCaption;
                Ball[i].Image = null;
                Ball[i].BackColor = SystemColors.GradientInactiveCaption;
                Pokestats[i].Text = string.Empty;
                Pokestats[i].BackColor = setcolors.Item1;
                Pokestats[i].ForeColor = setcolors.Item2;
            }
        }

        private int CheckComponentindex(PictureBox[] Sprites, PictureBox[] Ball, TextBox[] Pokestats)
        {
            var ctr = 0;
            for (int i = 0; i < 10; i++)
            {
                if (Sprites[i].Image == null && Ball[i].Image == null || Pokestats[i].Text == string.Empty)
                    break;
                ctr++;
            }

            return ctr;
        }
        private void CheckMemoryLeak(PictureBox[] Sprites, PictureBox[] Ball, TextBox[] Pokestats, int count)
        {
            for (int i = 0; i < Sprites.Length - 1; i++)
            {
                Sprites[i].Image = Sprites[i + 1].Image;
                Sprites[i].BackColor = Sprites[i + 1].BackColor;
                Ball[i].Image = Ball[i + 1].Image;
                Ball[i].BackColor = Ball[i + 1].BackColor;
                Pokestats[i].Text = Pokestats[i + 1].Text.Replace($"{ChangeLanguageString(Language.SelectedIndex, 12)}: {i + 2}", $"{ChangeLanguageString(Language.SelectedIndex, 12)}: {i + 1}");
                Pokestats[i].BackColor = Pokestats[i + 1].BackColor;
                Pokestats[i].ForeColor = Pokestats[i + 1].ForeColor;
            }
            Sprites[Sprites.Length - 1].Image = PokeSpriteBox12.Image;
            Sprites[Sprites.Length - 1].BackColor = PokeSpriteBox12.BackColor;
            Ball[Ball.Length - 1].Image = BallBox12.Image;
            Ball[Ball.Length - 1].BackColor = BallBox12.BackColor;
            Pokestats[Pokestats.Length - 1].Text = PokeStats12.Text.Replace($"{ChangeLanguageString(Language.SelectedIndex, 12)}: {count}", $"{ChangeLanguageString(Language.SelectedIndex, 12)}: 10");
            Pokestats[Pokestats.Length - 1].BackColor = PokeStats12.BackColor;
            Pokestats[Pokestats.Length - 1].ForeColor = PokeStats12.ForeColor;
        }

        private string GetRealEggString(PK9 pkm, bool eggcollect = false)
        {
            string pid = $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 1)}: {pkm.PID:X8}";
            string ec = $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 2)}: {pkm.EncryptionConstant:X8}";
            var form = FormOutput(Strings, pkm.Species, pkm.Form, out _);
            string gender = string.Empty;
            string moveoutput = string.Empty;
            switch (pkm.Gender)
            {
                case 0: gender = $" ({ChangeLanguageString(Language.SelectedIndex, 3)})"; break;
                case 1: gender = $" ({ChangeLanguageString(Language.SelectedIndex, 4)})"; break;
                case 2: break;
            }
            string sensitiveinfo = HidePIDEC.Checked ? "" : $"{pid}{ec}";
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
                    moveoutput += $"{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move1_PP}/{pkm.GetMovePP(pkm.Move1, 0)}";
                else if (i == 1)
                    moveoutput += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move2_PP}/{pkm.GetMovePP(pkm.Move2, 0)}";
                else if (i == 2)
                    moveoutput += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move3_PP}/{pkm.GetMovePP(pkm.Move3, 0)}";
                else if (i == 3)
                    moveoutput += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move4_PP}/{pkm.GetMovePP(pkm.Move4, 0)}";
            }
            string output = $"{$"{Strings.EggName} #{(eggcollect ? basketcount : eggcount)}"}{Environment.NewLine}{(pkm.ShinyXor == 0 ? "■ - " : pkm.ShinyXor <= 16 ? "★ - " : "")}{Strings.Species[pkm.Species]}{form}{gender}{sensitiveinfo}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 8)}: {Strings.Natures[(byte)pkm.Nature]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 9)}: {(Strings.Ability[pkm.Ability]).Replace(" ", "")}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 10)}: {pkm.IV_HP}/{pkm.IV_ATK}/{pkm.IV_DEF}/{pkm.IV_SPA}/{pkm.IV_SPD}/{pkm.IV_SPE}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 11)}: {PokeSizeDetailedUtil.GetSizeRating(pkm.Scale)}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 39)}: {pkm.Scale}{Environment.NewLine}" + moveoutput;
            return output;
        }
        private void GetEggString(double countstring, PK9 pkm)
        {
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 0)}!";
            string pidpre = $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 1)}: {pkm.PID:X8}";
            string ecpre = $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 2)}: {pkm.EncryptionConstant:X8}";
            var formpre = FormOutput(Strings, pkm.Species, pkm.Form, out _);
            string genderpre = string.Empty;
            string moveoutputpre = string.Empty;
            switch (pkm.Gender)
            {
                case 0: genderpre = $" ({ChangeLanguageString(Language.SelectedIndex, 3)})"; break;
                case 1: genderpre = $" ({ChangeLanguageString(Language.SelectedIndex, 4)})"; break;
                case 2: break;
            }
            string sensitiveinfopre = HidePIDEC.Checked ? "" : $"{pidpre}{ecpre}";
            int moveoffsetpre = 0;
            for (int i = 0; i < 4; i++)
            {
                if (pkm.Moves[i] == 0)
                {
                    moveoffsetpre = i;
                    break;
                }
                else if (i == 3)
                    moveoffsetpre = 4;
            }
            for (int i = 0; i < moveoffsetpre; i++)
            {
                if (i == 0)
                    moveoutputpre += $"{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move1_PP}/{pkm.GetMovePP(pkm.Move1, 0)}";
                else if (i == 1)
                    moveoutputpre += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move2_PP}/{pkm.GetMovePP(pkm.Move2, 0)}";
                else if (i == 2)
                    moveoutputpre += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move3_PP}/{pkm.GetMovePP(pkm.Move3, 0)}";
                else if (i == 3)
                    moveoutputpre += $"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}: {Strings.Move[pkm.Moves[i]]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 6)}{i+1}PP: {pkm.Move4_PP}/{pkm.GetMovePP(pkm.Move4, 0)}";
            }
            string outputpre = $"{$"{ChangeLanguageString(Language.SelectedIndex, 5, countstring)}!"}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 7)}{Environment.NewLine}{(pkm.ShinyXor == 0 ? "■ - " : pkm.ShinyXor <= 16 ? "★ - " : "")}{Strings.Species[pkm.Species]}{formpre}{genderpre}{sensitiveinfopre}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 8)}: {Strings.Natures[(byte)pkm.Nature]}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 9)}: {(Strings.Ability[pkm.Ability]).Replace(" ", "")}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 10)}: {pkm.IV_HP}/{pkm.IV_ATK}/{pkm.IV_DEF}/{pkm.IV_SPA}/{pkm.IV_SPD}/{pkm.IV_SPE}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 11)}: {PokeSizeDetailedUtil.GetSizeRating(pkm.Scale)}{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 39)}: {pkm.Scale}{Environment.NewLine}" + moveoutputpre;
            PokeStats.Text = outputpre;
            outputpre = $"{Environment.NewLine}" + outputpre;
            LogUtil.LogText(outputpre);
            var spritepre = PokeImg(pkm, false);
            PokeSpriteBox.Load(spritepre);
            var ballspritepre = SpriteUtil.GetBallSprite(pkm.Ball);
            BallBox.Image = ballspritepre;
        }

        public string ChangeLanguageString(int language, int index, double count = 0)
        {
            string str;
            switch (language)
            {
                case 0: str = ((string_ja)index).ToString(); break;
                default: str = ((string_en)index).ToString().Replace("_", " "); break;
            }
            if (index == 5)
            {
                switch (language)
                {
                    case 1: var strarr = str.Split(" ").ToArray(); str = strarr.AsSpan()[0] + $" {count} " + strarr.AsSpan()[1]; break;
                    case 0: str = $"{count}" + str; break;

                }

            }
            if (language != 0)
            {
                var strarr = str.Split(" ").ToArray();
                if (index == 13)
                {
                    strarr.AsSpan()[1] += "!";
                }
                else if (index == 16 || index == 30)
                {
                    strarr.AsSpan()[4] += ".";
                }
                else if (index == 17 || index == 38)
                {
                    strarr.AsSpan()[2] += "!";
                }
                if (ChangeList.Contains(index))
                    strarr.AsSpan()[strarr.Length - 1] += ".";
                str = string.Join(" ", strarr);
            }
            else
            {
                var strarr = str.Split("_").ToArray();
                if (index == 13 || index == 17 || index == 25 || index == 16 || index == 38)
                {
                    strarr.AsSpan()[0] += "。";
                    if (index != 38)
                        strarr.AsSpan()[strarr.Length - 1] += "。";
                    if (index == 16)
                    {
                        strarr.AsSpan()[1] += "。";
                        str = strarr.AsSpan()[0] + strarr.AsSpan()[1] + " " + strarr.AsSpan()[strarr.Length - 1];
                        return str;
                    }
                    str = string.Join(" ", strarr);
                }
                else if (index == 19 || index == 20 || index == 24 || index == 36)
                    str += "。";

            }
            return str;
        }

        private IReadOnlyList<int> ChangeList = new List<int> { 13, 16, 17, 18, 19, 20, 24, 30, 33, 34, 36 };

        private enum string_en : int
        {
            Previous_egg_has_been_read,
            PID,
            EC,
            M,
            F,
            Waiting_seconds,
            Move,
            Previous_Egg,
            Nature,
            Ability,
            IVs,
            Scale,
            Basket_Count,
            Match_found_Claim_your_egg_before_closing_the_picnic,
            Resetting,
            Next_Sandwich,
            minutes_have_passed_without_an_egg_Attempting_full_recovery,
            min_has_passed_Start_reading_emptyegg_data,
            Finish_makeing_sandwiches,
            Process_has_been_canceled,
            Process_has_been_finished,
            Uptime,
            ReOpening_picinic,
            Finish_closing_picnic,
            Start_reading_empty_egg_data,
            Finish_reading_empty_egg_data_and_reopening_picnic,
            Finish_opening_picnic,
            Start_makeing_a_sandwish,
            Sandwiches_Made,
            Finish_reposition,
            This_will_restart_the_application_Do_you_wish_to_continue,
            Caching_offsets_complete,
            Hard_Stop_Initiated,
            Please_fill_the_fields_before_attempting_to_save,
            Done,
            Shinies_Found,
            Finish_reopening_picnic,
            The_Process_has_alredy_been_canceled,
            mins_has_passed_Attempting_full_recovery,
            Real_Scale,
            Level,
            HeldItem
        }

        private enum string_ja : int
        {
            以前のタマゴを読み取りました,
            性格値,
            暗号化定数,
            オス,
            メス,
            秒待機中,
            技,
            以前のタマゴ,
            性格,
            特性,
            個体値,
            大きさ,
            バスケットカウント,
            厳選完了です_ピクニックを閉じる前にタマゴを受け取りましょう,
            リセット中,
            次のサンドイッチ,
            分経過しましたが_卵ができていません_修正します,
            分が経過しました_以前のタマゴの読み取りを開始します,
            サンドイッチ作成完了,
            プロセスが中断されました,
            プロセスは正常に終了しました,
            経過時間,
            ピクニックを再度開いています,
            ピクニックを閉じました,
            以前のタマゴの読み取りを開始します,
            以前のタマゴの読み取り完了_再度ピクニックを開きます,
            ピクニックを開き終わりました,
            サンドイッチ作成開始,
            作成したサンドイッチの数,
            位置調整完了,
            アプリケーションを再起動しますか,
            オフセットの読み取り完了,
            プロセスの中断,
            保存前に必須項目を埋めてください,
            完了,
            厳選済み色違い,
            ピクニックを再度開きました,
            プロセスは既に中断されています,
            分が経過しました_ゲームをリセットします,
            大きさ実数値,
            レベル,
            持ち物
        }

        private void LanguageChanged(object sender, EventArgs e)
        {
            Strings = GameInfo.GetStrings(Language.SelectedIndex);
            languageindex = Language.SelectedIndex;
            switch (Language.SelectedIndex)
            {
                case 0:
                    {
                        WaitTime.Location = new Point(345, 94);
                        FetchButton.Text = "厳選開始";
                        HardStopButton.Text = "タスク終了";
                        EatOnStart.Text = "最初に食べる";
                        Item1Label.Text = "アイテム1";
                        Item2Label.Text = "アイテム2";
                        Item3Label.Text = "アイテム3";
                        Item4Label.Text = "アイテム4";
                        Item5Label.Text = "アイテム5";
                        Item6Label.Text = "アイテム6";
                        checkBox8.Text = "リセットした？";
                        checkBox9.Text = "サンドイッチのみ作る";
                        checkBox12.Text = "手持ちポケモン確認";
                        SandwichCount.Text = "作ったサンドイッチの数:" + SandwichCount.Text.Split(":").ToArray().Last();
                        ShinyFoundLabel.Text = "厳選済み色違い:" + ShinyFoundLabel.Text.Split(":").ToArray().Last();
                        BasketCount.Text = "バスケットカウント:" + BasketCount.Text.Split(":").ToArray().Last();
                        NextSanwichLabel.Text = "次のサンドイッチ: " + (NextSanwichLabel.Text.Split(": ").ToArray().Length == 1 ? "" : NextSanwichLabel.Text.Split(": ").ToArray().Last());
                        HoldIngredients.Text = "材料を掴む?";
                        label1.Text = "材料を掴む数";
                        HoldTimeToFillings.Text = "材料を掴む長さ";
                        StopConditionsButton.Text = "厳選条件";
                        UptimeLabel.Text = "経過時間: " + (UptimeLabel.Text.Split(" ").ToArray().Length == 1 ? "" : UptimeLabel.Text.Split(" ").ToArray().Last());
                        HidePIDEC.Text = "個人情報を隠す?";
                        WaitTime.Text = "ピクニックの待機時間";
                        break;
                    }
                case 1:
                    {
                        WaitTime.Location = new Point(358, 94);
                        FetchButton.Text = "Fetch";
                        HardStopButton.Text = "HardStop";
                        EatOnStart.Text = "Eat On Start?";
                        Item1Label.Text = "Item 1";
                        Item2Label.Text = "Item 2";
                        Item3Label.Text = "Item 3";
                        Item4Label.Text = "Item 4";
                        Item5Label.Text = "Item 5";
                        Item6Label.Text = "Item 6";
                        checkBox8.Text = "Has Reset";
                        checkBox9.Text = "Only Make Sandwiches";
                        checkBox12.Text = "Party Check";
                        SandwichCount.Text = "Sandwiches Made:" + SandwichCount.Text.Split(":").ToArray().Last();
                        ShinyFoundLabel.Text = "Shinies Found:" + ShinyFoundLabel.Text.Split(":").ToArray().Last();
                        BasketCount.Text = "Basket Count:" + BasketCount.Text.Split(":").ToArray().Last();
                        NextSanwichLabel.Text = "Next Sandwich: " + (NextSanwichLabel.Text.Split(": ").ToArray().Length == 1 ? "" : NextSanwichLabel.Text.Split(": ").ToArray().Last());
                        HoldIngredients.Text = "Hold Fillings?";
                        label1.Text = "Fillings Count?";
                        HoldTimeToFillings.Text = "Fillings HOLD Time";
                        StopConditionsButton.Text = "Stop Conditions";
                        UptimeLabel.Text = "Uptime: " + (UptimeLabel.Text.Split(" ").ToArray().Length == 1 ? "" : UptimeLabel.Text.Split(" ").ToArray().Last());
                        HidePIDEC.Text = "Hide Sensitive Info?";
                        WaitTime.Text = "Picnic Wait Time";
                        break;
                    }
                default: break;
            }
        }
        public static string FormOutput(GameStrings strings, ushort species, byte form, out string[] formString)
        {
            formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(PKM) == typeof(PKM) ? EntityContext.Gen9 : EntityContext.Gen4);
            if (formString.Length is 0)
                return string.Empty;

            formString[0] = "";
            if (form >= formString.Length)
                form = (byte)(formString.Length - 1);

            return formString[form].Contains("-") ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
        }
        private async Task RecoveryReset(CancellationToken token)
        {
            await ReOpenGame(token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_250, token).ConfigureAwait(false);
            await Click(DDOWN, 0_250, token).ConfigureAwait(false);
            await Click(DDOWN, 0_250, token).ConfigureAwait(false);
            await Click(A, 8_000, token).ConfigureAwait(false);

        }
        public async Task ReOpenGame(CancellationToken token)
        {
            await CloseGame(token).ConfigureAwait(false);
            await StartGame(token).ConfigureAwait(false);
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
            await Task.Delay(12_000 + 5_000, token).ConfigureAwait(false);

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

            await Task.Delay(5_000 + 3_000, token).ConfigureAwait(false);
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
        public async Task CloseGame(CancellationToken token)
        {
            // Close out of the game
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(HOME, 2_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
        }
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            OverworldOffset = await Executor.SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            BoxStartOffset = await Executor.SwitchConnection.PointerAll(Offsets.BoxStartPointer, token).ConfigureAwait(false);
            CurrentBoxIndexOffset = await Executor.SwitchConnection.PointerAll(Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
            ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
            LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 31)}!");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 31)}!";
        }


        private bool ValidateEncounter(PK9 pk, EncounterFilter filter)
        {
            if (pk.IsShiny)
            {
                if (pk.ShinyXor == 0)
                    squarecount++;
                else
                    starcount++;
                ShinyFoundLabel.Text = $"Shinies Found: {squarecount + starcount}";
                SquareStarCount.Text = $"■ - {squarecount} | ★ - {starcount}";
            }
            var log = $"{Environment.NewLine}Filter Name: {filter.Name}" + $"{Environment.NewLine}Target Species: {(filter.Species == null ? "None" : (Species)filter.Species)}, Target Form: {(filter.Form == null ? "None" : filter.Form)}" +
                $"{Environment.NewLine}Target Natures: {(filter.Nature == null || filter.Nature.Count == 0 ? "Null" : string.Join(", ", filter.Nature))}" + $"{Environment.NewLine}Target Abilities: {(filter.AbilityList == null || filter.AbilityList.Count == 0 ? "None" : string.Join(", ", filter.AbilityList))}" +
                $"{Environment.NewLine}Target Gender: {(filter.Gender > 2 ? "Any" : (Gender)filter.Gender)}" + $"{Environment.NewLine}Target Shiny Condition: {(Shiny)filter.Shiny}" +
                $"{Environment.NewLine}Scale Check?: {filter.Scale}" + $"{Environment.NewLine}Aim for ThreeSegment?: {filter.ThreeSegment}";
            if (filter.ignoreIVs)
                LogUtil.LogText(log);
            if (filter.Species is not null && filter.Species != 0 && pk.Species != filter.Species)
                return false;

            if (filter.Form is not null && filter.Form != pk.Form)
                return false;

            var ivs = RaidCrawler.Core.Structures.Utils.ToSpeedLast(pk.IVs);
            string ivstring = string.Join('/', ivs);
            string ivmaxstring;
            string ivminstring;
            if (!filter.ignoreIVs)
            {
                (IVFiltersMax, IVFiltersMin) = GrabIvFilters(filter);
                ivmaxstring= string.Join('/', IVFiltersMax);
                ivminstring = string.Join('/', IVFiltersMin);
                log += $"{Environment.NewLine}Mon IVs: " + ivstring + $"{Environment.NewLine}Target IVs Max: " + ivmaxstring + $"{Environment.NewLine}Target IVs MIn: " + ivminstring;
                LogUtil.LogText(log);
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

            if (pk.Gender != filter.Gender && filter.Gender != 3)
                return false; // gender != gender filter when gender is not Any

            if (filter.Scale && pk.Scale > 0 && pk.Scale < 255) // Mini/Jumbo Only
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
        private void ForcifyEgg(PK9 pk)
        {
            pk.IsEgg = true;
            pk.Language = TrainerInfo.Language;
            pk.Nickname = GameInfo.GetStrings(TrainerInfo.Language).EggName;
            pk.MetLocation = 0;
            pk.EggLocation = 30023;
            pk.MetDate = DateOnly.FromDateTime(DateTime.Now);
            pk.EggMetDate = pk.MetDate;
            pk.OriginalTrainerName = TrainerInfo.OT;
            pk.HeldItem = 0;
            pk.CurrentLevel = 1;
            pk.EXP = 0;
            pk.MetLevel = 1;
            pk.CurrentHandler = 0;
            pk.OriginalTrainerFriendship = PersonalTable.SV.GetFormEntry(pk.Species, pk.Form).HatchCycles;
            pk.HandlingTrainerName = "";
            pk.HandlingTrainerFriendship = 0;
            pk.ClearMemories();
            pk.StatNature = pk.Nature;
            pk.SetEVs(new int[] { 0, 0, 0, 0, 0, 0 });
            pk.SetMarking(0, 0);
            pk.SetMarking(1, 0);
            pk.SetMarking(2, 0);
            pk.SetMarking(3, 0);
            pk.SetMarking(4, 0);
            pk.SetMarking(5, 0);
            pk.ClearInvalidMoves();
            

            ViewerUtil.DumpPokemon(AppDomain.CurrentDomain.BaseDirectory, "forced-eggs", pk);
        }

        private async Task GatherPokeParty(CancellationToken token)
        {
            await GatherParty(token).ConfigureAwait(false);
        }

        private async Task GatherParty(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            List<PK9> pkList = [];
            for (int i = 0; i < 6; i++)
            {
                var val = 0x30;
                switch (i)
                {
                    case 0: break;
                    case 1: val = 0x38; break;
                    case 2: val = 0x40; break;
                    case 3: val = 0x48; break;
                    case 4: val = 0x50; break;
                    case 5: val = 0x58; break;
                }
                var pointer = new long[] { 0x4763C98, 0x08, val, 0x30, 0x00 };// ver 4.0.0
                var offset = await Executor.SwitchConnection.PointerAll(pointer, token).ConfigureAwait(false);
                var pk = await ReadBoxPokemonSV(offset, 0x158, token).ConfigureAwait(false);                
                bool sanity = SanityCheck(pk, i);
                if (sanity)
                    pkList.Add(pk);
            }
            List<int> LanguageList = [];
            foreach(var pkm in pkList)
            {
                if (!LanguageList.Contains(pkm.Language))
                    LanguageList.Add(pkm.Language);
            }
            if (LanguageList.Count  > 1)
                MasudaMethod = true;
            else
                MasudaMethod = false;            
        }

        private async Task<PK9> ReadBoxPokemonSV(ulong offset, int size, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            var pk = new PK9(data);
            return pk;
        }

        private async Task<PK9> ReadPokemonSV(uint offset, int size, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(offset, size, token).ConfigureAwait(false);
            var pk = new PK9(data);
            return pk;
        }

        private async Task SetStick(SwitchStick stick, short x, short y, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var cmd = SwitchCommand.SetStick(stick, x, y, true);
            await Executor.SwitchConnection.SendAsync(cmd, token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task<PK9> ReopenPicnic(CancellationToken token)
        {
            LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 22)}...");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 22)}...";
            await ClosePicnic(token).ConfigureAwait(false);
            await OpenPicnic(token).ConfigureAwait(false);
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 24)}";
            int count = 6_500;
            if (Executor.UseCRLF)
                count += 0_500;
            if (checkBox8.Checked)
            {
                count += 1_000;
                checkBox8.Checked = false;
            }
            var countstring = count / 1000.00;
            await Task.Delay(count, token).ConfigureAwait(false);
            var pkm = await ReadPokemonSV(Offsets.EggData, 344, token).ConfigureAwait(false);
            GetEggString(countstring, pkm);
            LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 36)}");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 36)}";
            return pkm;
        }
        private async Task ClosePicnic(CancellationToken token)
        {
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 1_500, token).ConfigureAwait(false);
            await Task.Delay(0_500, token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
                await Click(Y, 0_500, token).ConfigureAwait(false);
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_800).ConfigureAwait(false);
                for (int i = 0; i < 3; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                return;
            }
            await Click(A, 1_000, token).ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
            LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 23)}!");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 23)}";
        }

        private async Task OpenPicnic(CancellationToken token)
        {
redo:
            await Task.Delay(0_600, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await Click(X, 1_000, token).ConfigureAwait(false);
                if (checkBox8.Checked)
                {
                    await Click(DRIGHT, 0_800, token).ConfigureAwait(false);
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                }
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                goto redo;
            }
            LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 26)}!");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 26)}!";
        }
        private async Task<int> SetItem(int Item, int last, int index, CancellationToken token)
        {
            var shift = EatItem1[index - 1].Item4 ? EatItem1[index].Item4 ? -last + Item : -last - Item : EatItem1[index].Item4 ? last + Item : last - Item;

            for (int i = 0; i < Math.Abs(shift); i++)
            {
                if (shift > 0)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
                else
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            }
            return Item;
        }
        private async Task<int> SetCondiments(int Item, int last, int index, CancellationToken token)
        {
            var shift = EatItem2[index - 1].Item4 ? EatItem2[index].Item4 ? -last + Item : -last - Item : EatItem2[index].Item4 ? last + Item : last - Item;

            for (int i = 0; i < Math.Abs(shift); i++)
            {
                if (shift > 0)
                    await Click(DUP, 0_800, token).ConfigureAwait(false);
                else
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            }
            return Item;
        }
        public async Task<(bool, string)> OnlyMakeSandwich(CancellationToken token, bool TeleportMode = false)
        {
            Stopwatch Watch = new();
            BlankVal = await PicnicState(token).ConfigureAwait(false);
            Watch.Start();
            while (BlankVal != 1)
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                BlankVal =await PicnicState(token).ConfigureAwait(false);
                //LogUtil.LogText($"PicnicState: {BlankVal}");
                if (Watch.Elapsed > TimeSpan.FromSeconds(10))
                    await SetPicnicState(token).ConfigureAwait(false);
            }
            for (int i = 0; i < EatItem1.Count; i++)
            {
                var index = Ingredients.FindIndex(item => item.Item1 == EatItem1[i].Item1);
                if (index == -1)
                {
                    var msg = $"Ingredients {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem1[i].Item1]}) is not found!";
                    return (false, msg);
                }
                if (Ingredients[index].Item2 < EatItem1[i].Item2)
                {
                    var msg = $"Ingredients {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem1[i].Item1]}) is only {Ingredients[index].Item2} count!";
                    return (false, msg);
                }

            }
            for (int i = 0; i < EatItem2.Count; i++)
            {
                var index = Condiments.FindIndex(item => item.Item1 == EatItem2[i].Item1);
                if (index == -1)
                {
                    var msg = $"Condiments {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem2[i].Item1]}) is not found!";
                    return (false, msg);
                }
                if (Condiments[index].Item2 < EatItem2[i].Item2)
                {
                    var msg = $"Condiments {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem2[i].Item1]}) is only {Condiments[index].Item2} count!";
                    return (false, msg);
                }
            }
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 27)}!";
            await Task.Delay(1_000).ConfigureAwait(false);
            int itemindex = 0;
            int last = 0;
            int count = 0;
            await Click(MINUS, 0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 32323, 0_700, token).ConfigureAwait(false); // Face up to table
            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
            await Task.Delay(1_000).ConfigureAwait(false);
            if (TeleportMode)
                await Click(MINUS, 2_000, token).ConfigureAwait(false);
            await Click(A, 1_500, token).ConfigureAwait(false);// Dummy press if we're in union circle, doesn't affect routine
            await Click(A, 8_000, token).ConfigureAwait(false);
            await Click(X, 2_500, token).ConfigureAwait(false);
            List<int> HoldFillings = [];
            for (int i = 0; i < EatItem1.Count; i++)
            {
                if (BigItems.Contains(EatItem1[i].Item1) || MiddleItem.Contains(EatItem1[i].Item1))
                {
                    count += EatItem1[i].Item2;
                    if (BigItems.Contains(EatItem1[i].Item1))
                    {
                        for (int j = 0; j < EatItem1[i].Item2; j++)
                            HoldFillings.Add(1);
                    }
                    else
                        for (int j = 0; j < EatItem1[i].Item2; j++)
                            HoldFillings.Add(3);
                }
                else
                {
                    count += 1;
                    HoldFillings.Add(3 * EatItem1[i].Item2);
                }
            }
            //LogUtil.LogText($"Ingredients Count: {count}");
            //LogUtil.LogText($"Ingredients HoldFillings: {string.Join("/", HoldFillings)}");

            if (!string.IsNullOrEmpty(Item1Value.Text))
            {
                // Lettuce
                for (int i = 0; i < Convert.ToInt32(Item1Value.Text); i++)
                    await Click(EatItem1[itemindex].Item4 ? DUP : DDOWN, 0_800, token).ConfigureAwait(false);

                last = Convert.ToInt32(Item1Value.Text);
                itemindex++;
            }
            for (int i = 0; i < Item1Count.Value; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(Item2Value.Text))
            {
                // Lettuce
                last = await SetItem(Convert.ToInt32(Item2Value.Text), last, itemindex, token).ConfigureAwait(false);
                itemindex++;
                for (int i = 0; i < Item2Count.Value; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }
            if (!string.IsNullOrEmpty(Item3Value.Text))
            {
                // Lettuce
                await SetItem(Convert.ToInt32(Item3Value.Text), last, itemindex, token).ConfigureAwait(false);
                for (int i = 0; i < Item3Count.Value; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }

            await Click(PLUS, 0_800, token).ConfigureAwait(false);
            itemindex = 0;
            last = 0;
            if (!string.IsNullOrEmpty(Item4Value.Text))
            {
                // Mystica Sweet
                for (int i = 0; i < Convert.ToInt32(Item4Value.Text); i++)
                    await Click(EatItem2[itemindex].Item4 ? DUP : DDOWN, 0_800, token).ConfigureAwait(false);

                last = Convert.ToInt32(Item4Value.Text);
                itemindex++;
            }
            for (int i = 0; i < Item4Count.Value; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(Item5Value.Text))
            {
                // Mystica Salt
                last = await SetCondiments(Convert.ToInt32(Item5Value.Text), last, itemindex, token).ConfigureAwait(false);
                itemindex++;

                for (int i = 0; i < Item5Count.Value; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }
            if (!string.IsNullOrEmpty(Item6Value.Text))
            {
                // Mystica Salt
                await SetCondiments(Convert.ToInt32(Item6Value.Text), last, itemindex, token).ConfigureAwait(false);
                for (int i = 0; i < Item6Count.Value; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }
            await Click(PLUS, 0_800, token).ConfigureAwait(false);
            // Set pick
            await Click(A, 8_000, token).ConfigureAwait(false);
            //Wait for bread

            var fillingtime = Convert.ToInt32(FillingHoldTime.Text);
            await SetStick(LEFT, 0, 30000, 0_000 + fillingtime, token).ConfigureAwait(false); // Navigate to ingredients
            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
            await Task.Delay(0_500, token).ConfigureAwait(false);

            if (HoldIngredients.Checked)
            {
                for (int index = 0; index < count; index++)
                {
                    for (int i = 0; i < HoldFillings[index]; i++) // Amount of ingredients to drop
                    {
                        await Hold(A, 0_800, token).ConfigureAwait(false);

                        await SetStick(LEFT, 0, -30000, 0_000 + fillingtime, token).ConfigureAwait(false); // Navigate to ingredients
                        await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                        if (i % 3 == 1)
                        {
                            await SetStick(LEFT, -20000, 0, 0_450, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        else if (i % 3 == 2)
                        {
                            await SetStick(LEFT, 20000, 0, 0_450, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        await Task.Delay(0_500, token).ConfigureAwait(false);
                        await Release(A, 0_800, token).ConfigureAwait(false);
                        if (i == 0 || i == 3)
                        {
                            await Task.Delay(0_500).ConfigureAwait(false);
                            await SetStick(LEFT, -5000, 0, 0_200, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                            await Task.Delay(0_500).ConfigureAwait(false);

                        }
                        if (index == 0 && i == 0)
                        {
                            await SetStick(LEFT, 0, -5000, 0_400, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                            await Task.Delay(0_500).ConfigureAwait(false);
                        }
                        await SetStick(LEFT, 0, 30000, fillingtime, token).ConfigureAwait(false); // Navigate to ingredients
                        await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                        await Task.Delay(0_500, token).ConfigureAwait(false);
                        if (i % 3 == 1)
                        {
                            await SetStick(LEFT, 20000, 0, 0_450, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                        }
                        if (i % 3 == 2)
                        {
                            await SetStick(LEFT, -20000, 0, 0_450, token).ConfigureAwait(false); // Navigate to ingredients
                            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                            if (index >= 1)
                            {
                                await SetStick(LEFT, 0, 5000, 0_250, token).ConfigureAwait(false); // Navigate to ingredients
                                await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
                                await Task.Delay(0_500, token).ConfigureAwait(false);
                            }

                        }
                    }
                    await Click(R, 1_000, token).ConfigureAwait(false);
                    if (index == 0)
                        fillingtime -= 0_200;
                }
            }

            for (int i = 0; i < 12; i++) // If everything is properly positioned
                await Click(A, 0_800, token).ConfigureAwait(false);

            // Sandwich failsafe
            for (int i = 0; i < 5; i++) //Attempt this several times to ensure it goes through
                await SetStick(LEFT, 0, 30000, 1_000, token).ConfigureAwait(false); // Scroll to the absolute top
            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);

            while (await PicnicState(token).ConfigureAwait(false) == BlankVal + 1) // Until we start eating the sandwich
            {
                await SetStick(LEFT, 0, -5000, 0_300, token).ConfigureAwait(false); // Scroll down slightly and press A a few times; repeat until un-stuck
                await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);

                for (int i = 0; i < 6; i++)
                    await Click(A, 0_800, token).ConfigureAwait(false);
            }

            while (await PicnicState(token).ConfigureAwait(false) == BlankVal + 2)  // eating the sandwich
                await Task.Delay(1_000, token).ConfigureAwait(false);

            sandwichcount++;
            SandwichCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 28)}: {sandwichcount}";

            while (!await IsInPicnic(token).ConfigureAwait(false)) // Acknowledge the sandwich and return to the picnic
            {
                await Click(A, 5_000, token).ConfigureAwait(false); // Wait a long time to give the flag a chance to update and avoid sandwich re-entry
                if (TeleportMode)                
                    await Task.Delay(2_000, token).ConfigureAwait(false); // Dummy Press and Cancel A Press                                    

                await SetStick(LEFT, 0, -5000, 0_500, token).ConfigureAwait(false); // Face down to basket
                await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
            }
            //LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 18)}!{Environment.NewLine}");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 18)}!";
            await Task.Delay(0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, -10000, 0_300, token).ConfigureAwait(false); // Face down to basket
            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);
            await Task.Delay(0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 10000, 0_310, token).ConfigureAwait(false); // Face up to basket
            await SetStick(LEFT, 0, 0, 0, token).ConfigureAwait(false);

            //LogUtil.LogText($"{Environment.NewLine}{ChangeLanguageString(Language.SelectedIndex, 29)}!{Environment.NewLine}");
            BasketCount.Text = $"{ChangeLanguageString(Language.SelectedIndex, 29)}!";
            for (int i = 0; i < EatItem1.Count; i++)
            {
                var index = Ingredients.FindIndex(item => item.Item1 == EatItem1[i].Item1);
                if (index == -1)
                {
                    var msg = $"Ingredients {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem1[i].Item1]}) is not found!";
                    return (false, msg);
                }
                Ingredients[index] = (Ingredients[index].Item1, Ingredients[index].Item2 - EatItem1[i].Item2);
            }
            for (int i = 0; i < EatItem2.Count; i++)
            {
                var index = Condiments.FindIndex(item => item.Item1 == EatItem2[i].Item1);
                if (index == -1)
                {
                    var msg = $"Condiments {i + 1} ({GameInfo.GetStrings(1).itemlist[EatItem2[i].Item1]}) is not found!";
                    return (false, msg);
                }
                Condiments[index] = (Condiments[index].Item1, Condiments[index].Item2 - EatItem2[i].Item2);
            }
            return (true, string.Empty);
        }
        private async Task<bool> PlayerCannotMove(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(PlayerCanMoveOffset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }
        private new async Task Click(SwitchButton b, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Click(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task Hold(SwitchButton b, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Hold(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task Release(SwitchButton b, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Release(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show($"{ChangeLanguageString(Language.SelectedIndex, 30)}?", $"{ChangeLanguageString(Language.SelectedIndex, 32)}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                Settings.Default.FillingHoldTime = FillingHoldTime.Text;
                Settings.Default.Save();
                Task.Run(async () => await Executor.SwitchConnection.SendAsync(SwitchCommand.DetachController(Executor.UseCRLF), CancellationToken.None).ConfigureAwait(false));
                try
                {
                    Executor.Disconnect();
                }
                catch(SocketException ex)
                {
                    MessageBox.Show(this, ex.ToString(), "Soket Exception!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                System.Windows.Forms.Application.Restart();
            }
            else if (dialogResult == DialogResult.No)
            {
                if (cts == null)
                    return;
                else if (cts.IsCancellationRequested || ForceCancel)
                {
                    MessageBox.Show($"{ChangeLanguageString(Language.SelectedIndex, 37)}!");
                    return;
                }
                cts.Cancel();
            }
        }

        public async Task<int> PicnicState(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(Offsets.PicnicMenu, 1, token).ConfigureAwait(false);
            return data[0]; // 1 when in picnic, 2 in sandwich menu, 3 when eating, 2 when done eating
        }
        public async Task SetPicnicState(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();

            var DefaultState = 0x01;
            await Executor.SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(DefaultState), Offsets.PicnicMenu, token).ConfigureAwait(false);
        }
        public async Task<bool> IsInPicnic(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesMainAsync(Offsets.PicnicMenu, 1, token).ConfigureAwait(false);
            return data[0] == BlankVal; // 1 when in picnic, 2 in sandwich menu, 3 when eating, 2 when done eating
        }

        private async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x11;
        }

        public void DisableOptions()
        {
            FetchButton.Enabled = false;
            WatchEggButton.Enabled = false;
            Item1Value.Enabled = false;
            Item2Value.Enabled = false;
            Item3Value.Enabled = false;
            Item4Value.Enabled = false;
            Item5Value.Enabled = false;
            Item6Value.Enabled = false;
            checkBox8.Enabled = false;
            checkBox9.Enabled = false;
            checkBox12.Enabled = false;
            Item1Count.Enabled = false;
            Item2Count.Enabled = false;
            Item3Count.Enabled = false;
            Item4Count.Enabled = false;
            Item5Count.Enabled = false;
            Item6Count.Enabled = false;
            FillingHoldTime.Enabled = false;
            NumberOfFillings.Enabled = false;
            EatOnStart.Enabled = false;
            HoldIngredients.Enabled = false;
            Language.Enabled = false;
            ForceDumpCheck.Enabled = false;
            WatchDeeper.Enabled = false;
            CollectEggsCheck.Enabled = false;
            ForceShinyCharm.Enabled = false;
            ForceMasudaMethod.Enabled = false;
            AutoReConnect.Enabled = false;
            SkipCheck.Enabled = false;
        }

        public void EnableOptions()
        {
            FetchButton.Enabled = true;
            WatchEggButton.Enabled = true;
            Item1Value.Enabled = true;
            Item2Value.Enabled = true;
            Item3Value.Enabled = true;
            Item4Value.Enabled = true;
            Item5Value.Enabled = true;
            Item6Value.Enabled = true;
            Item1Count.Enabled = true;
            Item2Count.Enabled = true;
            Item3Count.Enabled = true;
            Item4Count.Enabled = true;
            Item5Count.Enabled = true;
            Item6Count.Enabled = true;
            checkBox8.Enabled = true;
            checkBox9.Enabled = true;
            checkBox12.Enabled = true;
            FillingHoldTime.Enabled = true;
            NumberOfFillings.Enabled = true;
            EatOnStart.Enabled = true;
            HoldIngredients.Enabled = true;
            Language.Enabled = true;
            ForceDumpCheck.Enabled = true;
            WatchDeeper.Enabled = true;
            CollectEggsCheck.Enabled = true;
            ForceShinyCharm.Enabled = true;
            ForceMasudaMethod.Enabled = true;
            AutoReConnect.Enabled = true;
            SkipCheck.Enabled = true;
        }
        
        private void StopConditionsButton_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new(setcolors, ref encounterFilters, filtermode);
            miniform.ShowDialog();
        }
        private async void WatchEggButton_Click(object sender, EventArgs e)
        {
            ForceCancel = false;
            var success = true;
            if (WatchEggButton.Enabled)
                DisableOptions();
            using (cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    await InitializeSessionOffsets(token).ConfigureAwait(false);
                    if (CollectEggsCheck.Checked)
                        await WatchAndCollectEggs(token).ConfigureAwait(false);
                    else
                        await WatchEggs(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex, 19)}");
                    EnableOptions();
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
                    MessageBox.Show(this, $"{ex}");
                    EnableOptions();
                }

                ForceCancel = true;
                if (Executor.SwitchConnection.Connected)
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                if (!cts.IsCancellationRequested && cts != null && success)
                    MessageBox.Show(this, $"{ChangeLanguageString(Language.SelectedIndex, 20)}");
                return;
            }
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
        private void ScreenshotButton_Click(object sender, EventArgs e)
        {
            Rectangle bounds = Bounds;
            Bitmap bmp = new(250, 260);
            DrawToBitmap(bmp, bounds);
            Bitmap CroppedImage = bmp.Clone(new(80, 30, bmp.Width - 80, bmp.Height - 30), bmp.PixelFormat);
            Clipboard.SetImage(CroppedImage);
            MessageBox.Show("Copied to clipboard!");
        }
        private void ForceEgg_CheckedChanged(object sender, EventArgs e)
        {
            if (ForceDumpCheck.Checked)
                MessageBox.Show("You have enabled force dump eggs. These should not be considered legitimate and are only a backup for ghost eggs. Please do not pass these off as legitimate eggs.");
        }
        private void CollectEggsCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (CollectEggsCheck.Checked)
                MessageBox.Show("This mode uses the legacy method of collecting the egg from the basket as soon as it spawns. If you are experiencing an unstable experience with Wi-Fi desync/lag," +
                    " please leave this unchecked.");
        }

    }
}
