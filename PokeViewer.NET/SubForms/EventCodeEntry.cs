using Microsoft.VisualBasic;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using PokeViewer.NET.Misc;
using PokeViewer.NET.Util;
using RaidCrawler.Core.Structures;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net.Sockets;
using System.Security.Policy;
using static PokeViewer.NET.RoutineExecutor;
using static PokeViewer.NET.ViewerUtil;
using static SysBot.Base.SwitchButton;
using static System.Buffers.Binary.BinaryPrimitives;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace PokeViewer.NET.SubForms
{
    public partial class EventCodeEntry : Form
    {
        private readonly ViewerState Executor;
        private readonly WideViewerSV wideViewerSV;
        private readonly SimpleTrainerInfo simpleTrainerInfo;
        private ItemStructure itemStructure;
        protected ViewerOffsets ViewerOffsets { get; } = new();
        public EventCodeEntry(int GameType, ref ViewerState executor, (Color, Color) color, SimpleTrainerInfo trainerInfo)
        {
            wideViewerSV = new(GameType, ref executor, color, trainerInfo);
            simpleTrainerInfo = trainerInfo;
            InitializeComponent();
            Executor = executor;
            itemStructure = new(Executor);
            filtermode = FilterMode.MysteryGift_SV;
            var filterpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{filtermode}filters.json");
            if (File.Exists(filterpath))
                encounterFilters = System.Text.Json.JsonSerializer.Deserialize<List<EncounterFilter>>(File.ReadAllText(filterpath)) ?? new List<EncounterFilter>();
            SetColors(color);
        }
        private readonly string[] badVals = { "@", "I", "O", "=", "&", ";", "Z", "*", "#", "!", "?" };
        private int encounter = 0;
        private int[] IVFiltersMax = Array.Empty<int>();
        private int[] IVFiltersMin = Array.Empty<int>();
        private (Color, Color) setcolors;
        private FilterMode filtermode;
        private List<EncounterFilter> encounterFilters = new();
        private Image? ShinySquare = null!;
        private Image? ShinyStar = null!;
        private bool StartFromOverworld = true;
        private ulong BaseBlockKeyPointer = 0;
        private ulong ConnectedOffset = 0;
        private ulong PortalOffset = 0;
        private ulong CurrentBoxOffset = 0;
        private ulong OverworldOffset = 0;
        private ulong CurrentBoxIndexOffset = 0;
        private ulong BoxStartOffset = 0;
        private ulong ItemBlockOffset = 0;
        private int BoxSlotSize = 0x158;
        private bool ReadSlot = false;
        private byte CurrentBox = 0;
        private int CurrentBoxSlot = 0;
        private CancellationTokenSource? cts = null;
        private bool canceled = false;
        private bool FirstTry = true;
        List<InventoryPouch> Itemdata = [];
        private void SetColors((Color, Color) color)
        {
            BackColor = color.Item1;
            ForeColor = color.Item2;
            RedeemButton.BackColor = color.Item1;
            RedeemButton.ForeColor = color.Item2;
            ClearButton.BackColor = color.Item1;
            ClearButton.ForeColor = color.Item2;
            HardStop.BackColor = color.Item1;
            HardStop.ForeColor = color.Item2;
            ResetMysteryGifts.BackColor = color.Item1;
            ResetMysteryGifts.ForeColor = color.Item2;
            GiftCode.BackColor = color.Item1;
            GiftCode.ForeColor = color.Item2;
            setcolors = color;
        }
        private async Task AnticipateResponse(CancellationToken token)
        {
            using HttpClient client = new();
            string shinyicon = "https://raw.githubusercontent.com/zyro670/PokeTextures/2137b7024c161aad7ba832da481cff83792f5e67/icon_version/icon_";
            var square = await client.GetStreamAsync(shinyicon + "square.png", token).ConfigureAwait(false);
            ShinySquare = Image.FromStream(square);

            var star = await client.GetStreamAsync(shinyicon + "star.png", token).ConfigureAwait(false);
            ShinyStar = Image.FromStream(star);
        }
        private bool ValidateEncounter(PK9 pk, EncounterFilter filter)
        {
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

        private void button2_Click(object sender, EventArgs e)
        {
            if (cts == null)
                return;
            else if (cts.IsCancellationRequested || canceled)
            {
                MessageBox.Show(this, $"The Process has already been caceled!", "Operation Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            cts.Cancel();

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            using (cts = new CancellationTokenSource())
            {
                canceled = false;
                var token = cts.Token;
                DisableAssets();
                try
                {
                    await RecoverToOverworld(token).ConfigureAwait(false);
                    await ResetMysteryGift(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("The Process has been caceled!");
                    EnableAssets();
                    canceled = true;
                    return;
                }
                catch(Exception ex)
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
                    MessageBox.Show(this, ex.ToString(), "Exception!");
                    EnableAssets();
                    canceled = true;
                    return;
                }
                MessageBox.Show("The Process has been finished!");
                EnableAssets();
                canceled = true;
            }

        }
        private async Task ResetMysteryGift(CancellationToken token)
        {
            var mysterygiftoffset = await Executor.SwitchConnection.PointerAll(Blocks.MysteryGift.Pointer!, token).ConfigureAwait(false);
            while (mysterygiftoffset == 0)
            {
                await Task.Delay(0_100).ConfigureAwait(false);
                mysterygiftoffset = await Executor.SwitchConnection.PointerAll(Blocks.MysteryGift.Pointer!, token).ConfigureAwait(false);
            }
            var MysteryRecords = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(mysterygiftoffset, Blocks.MysteryGift.Size, token).ConfigureAwait(false);
            var inject = new byte[Blocks.MysteryGift.Size];
            if (!MysteryRecords.SequenceEqual(inject))
            {
                await WriteBlock(inject, Blocks.MysteryGift, token).ConfigureAwait(false);
                LogUtil.LogText("Reset Mystery records!");
            }
            await SaveGame(token).ConfigureAwait(false);
            await PreReOpenGame(token).ConfigureAwait(false);
            await Task.Delay(1_000).ConfigureAwait(false);
            ulong baseblockpointer = 0;
            while (baseblockpointer == 0)
            {
                await Task.Delay(0_100).ConfigureAwait(false);
                baseblockpointer = await Executor.SwitchConnection.PointerAll(ViewerOffsets.BlockKeyPointer, token).ConfigureAwait(false);
            }
            if (baseblockpointer != 0)
            {
                BaseBlockKeyPointer = baseblockpointer;
            }
            var internet = Blocks.AlreadyConnected;
            var connect = await ReadEncryptedBlockBool(internet, token).ConfigureAwait(false);
            await WriteBlock(false, internet, token, connect).ConfigureAwait(false);
            await Task.Delay(1_500).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                await Click(A, 6_000, token).ConfigureAwait(false);
            await Task.Delay(3_000).ConfigureAwait(false);
            StartFromOverworld = true;
            HasReset.Checked = true;
            FirstConnect.Checked = true;
        }
        public async Task SaveGame(CancellationToken token)
        {
            // Open the menu and save.
            await Task.Delay(0_600, token).ConfigureAwait(false);
            if(await IsOnOverworldTitle(token).ConfigureAwait(false))
                await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(R, 1_000, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);

        }
        private async Task<bool> ConnectAndEnterPortal(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointer, token).ConfigureAwait(false);
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await RecoverToOverworld(token).ConfigureAwait(false);

            LogUtil.LogText("Opening the Poké Portal.");

            // Open the X Menu.
            await Click(X, 1_000, token).ConfigureAwait(false);

            // Handle the news popping up.
            if (await Executor.SwitchConnection.IsProgramRunning(ViewerOffsets.LibAppletWeID, token).ConfigureAwait(false))
            {
                LogUtil.LogText("News detected, will close once it's loaded!");
                await Task.Delay(5_000, token).ConfigureAwait(false);
                await Click(B, 2_000, token).ConfigureAwait(false);
            }

            // Scroll to the bottom of the Main Menu so we don't need to care if Picnic is unlocked.
            if (HasReset.Checked)
            {
                await Click(DRIGHT, 0_300, token).ConfigureAwait(false);
                for (int i = 0; i < 3; i++)
                    await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                HasReset.Checked = false;
            }
            await Click(A, 1_000, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        private async Task ExitTradeToPortal(bool unexpected, CancellationToken token)
        {
            if (await IsInPokePortal(token).ConfigureAwait(false))
                return;

            if (unexpected)
                LogUtil.LogText("Unexpected behavior, recovering to Portal.");

            // Wait for the portal to load.
            LogUtil.LogText("Waiting on the portal to load...");
            var attempts = 0;
            while (!await IsInPokePortal(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                if (await IsInPokePortal(token).ConfigureAwait(false))
                    break;

                if (attempts++ > 15)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                // Didn't make it into the portal for some reason.
                if (++attempts > 40)
                {
                    LogUtil.LogText("Failed to load the portal, rebooting the game.");
                    if (!await RecoverToOverworld(token).ConfigureAwait(false))
                    {
                        await ReOpenGame(token).ConfigureAwait(false);
                        HasReset.Checked = true;
                    }
                    await ConnectAndEnterPortal(token).ConfigureAwait(false);
                    return;
                }
            }
            await Task.Delay(0_500, token).ConfigureAwait(false);
        }

        private async Task<bool> RecoverToOverworld(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointer, token).ConfigureAwait(false);
            var wait = 0;
            while (OverworldOffset == 0)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                wait++;
                OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointer, token).ConfigureAwait(false);
                if (wait >= 10)
                    break;
            }
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return true;

            LogUtil.LogText("Attempting to recover to overworld.");
            var attempts = 0;
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                attempts++;
                if (attempts >= 10)
                    break;

                await Click(B, 0_500, token).ConfigureAwait(false);
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;

                await Click(B, 0_800, token).ConfigureAwait(false);
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;

            }

            // We didn't make it for some reason.
            if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                LogUtil.LogText("Failed to recover to overworld, rebooting the game.");
                var check = 0;
                while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                {
                    await Click(B, 0_500, token).ConfigureAwait(false);
                    check++;
                    if (check >= 20)
                        break;
                }
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }

            // Force the bot to go through all the motions again on its first pass.
            return true;
        }
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointer, token).ConfigureAwait(false);
            PortalOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
            ConnectedOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.IsConnectedPointer, token).ConfigureAwait(false);
            CurrentBoxIndexOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.CurrentBoxPointer, token).ConfigureAwait(false);
            BoxStartOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.BoxStartPointer, token).ConfigureAwait(false);
            ItemBlockOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.ItemBlock, token).ConfigureAwait(false);
            if (CurrentBox != 0 || CurrentBoxSlot != 0)
                ReadSlot = true;
            else
                ReadSlot = false;
        }

        private async Task<bool> RecoverToPortal(CancellationToken token)
        {
            LogUtil.LogText("Reorienting to Poké Portal.");
            var attempts = 0;
            while (await IsInPokePortal(token).ConfigureAwait(false))
            {
                await Click(B, 1_500, token).ConfigureAwait(false);
                if (++attempts >= 30)
                {
                    LogUtil.LogText("Failed to recover to Poké Portal.");
                    return false;
                }
            }

            // Should be in the X menu hovered over Poké Portal.
            await Click(A, 1_000, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        // Waits for the Portal to load (slow) and then moves the cursor down to Link Trade.
        private async Task<bool> SetUpPortalCursor(CancellationToken token)
        {
            // Wait for the portal to load.
            var attempts = 0;
            while (!await IsInPokePortal(token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++attempts > 20)
                {
                    LogUtil.LogText("Failed to load the Poké Portal.");
                    return false;
                }
            }
            await Task.Delay(1_000 + (int)OverShoot.Value, token).ConfigureAwait(false);

            if (LinkAccount.Checked)
            {
                if (!await ConnectToOnline(token).ConfigureAwait(false))
                {
                    LogUtil.LogText("Failed to connect to online.");
                    return false; // Failed, either due to connection or softban.
                }
            }

            // Handle the news popping up.
            if (await Executor.SwitchConnection.IsProgramRunning(ViewerOffsets.LibAppletWeID, token).ConfigureAwait(false))
            {
                LogUtil.LogText("News detected, will close once it's loaded!");
                await Task.Delay(5_000, token).ConfigureAwait(false);
                await Click(B, 2_000 + (int)OverShoot.Value, token).ConfigureAwait(false);
            }

            LogUtil.LogText("Adjusting the cursor in the Portal.");
            // Move down to Link Trade.
            await Task.Delay(0_500, token).ConfigureAwait(false);
            await Click(DUP, 0_800, token).ConfigureAwait(false);            
            return true;
        }
        private async Task<bool> PerformLinkCodeTrade(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                RateBox.Text = string.Empty;
                pictureBox1.Image = null;
                pictureBox2.Image = null;
                pictureBox3.Image = null;
                await InitializeSessionOffsets(token).ConfigureAwait(false);
                if (Item.Checked)
                {
                    Itemdata = await itemStructure.ReadGiftItem(ItemBlockOffset, token).ConfigureAwait(false);
                }
                else
                {
                    textBox2.Text = string.Empty;
                    (ReadSlot, CurrentBoxOffset, CurrentBox, CurrentBoxSlot) = await ReadEmptySlot(ReadSlot, CurrentBox, CurrentBoxSlot, token).ConfigureAwait(false);
                    if (!ReadSlot)
                        return true;
                    //textBox2.Text = $"ReadSlot: {ReadSlot}{Environment.NewLine}Current Box Offset: {CurrentBoxOffset:X}{Environment.NewLine}Current Box: {CurrentBox + 1}{Environment.NewLine}Current Slot: {CurrentBoxSlot + 1}";
                }
                // StartFromOverworld can be true on first pass or if something went wrong last trade.
                var fail = await GiftSetUp(token).ConfigureAwait(false);
                if (fail)
                    return true;
                fail = await Exit(token).ConfigureAwait(false);
                if(fail) 
                    return true;
                var pk = await WaitForGift(CurrentBoxOffset, token).ConfigureAwait(false);
                bool sucess = false;
                if (filter.Checked)
                {
                    if (pk == null)
                        return false;

                    var Rate = SubForms.StopConditions.CalcRate(encounterFilters);
                    var DisplayRate = (1.00 - Math.Pow(1.00 - Rate, encounter)) * 100.00;
                    RateBox.Text = $"Target Rate: {DisplayRate:0.00}%";
                    for (int i = 0; i < encounterFilters.Count; i++)
                    {
                        if (!encounterFilters[i].Enabled)
                            continue;
                        sucess = ValidateEncounter(pk, encounterFilters[i]);
                        if (sucess)
                        {
                            WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), true, simpleTrainerInfo);
                            if (pk.IsShiny)
                            {
                                textBox2.BackColor = Color.Gold;
                                textBox2.ForeColor = Color.BlueViolet;
                            }
                            else
                            {
                                textBox2.BackColor = Color.YellowGreen;
                                textBox2.ForeColor = Color.OrangeRed;
                            }
                            break;
                        }
                    }
                    if (!sucess)
                        WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), false, simpleTrainerInfo, isGift: true, Encounter: encounter, Rate: DisplayRate);
                }
                else if(pk != null)
                    WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), false, simpleTrainerInfo, isGift: true, Encounter: encounter);
                return sucess;
            }
            return true;
        }
        private async Task<bool> RedeemMultiGifts(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                await InitializeSessionOffsets(token).ConfigureAwait(false);
                if (Item.Checked)
                    Itemdata = await itemStructure.ReadGiftItem(ItemBlockOffset, token).ConfigureAwait(false);
                else
                {
                    (ReadSlot, CurrentBoxOffset, CurrentBox, CurrentBoxSlot) = await ReadEmptySlot(ReadSlot, CurrentBox, CurrentBoxSlot, token).ConfigureAwait(false);
                    if (!ReadSlot)
                        return true;
                    //textBox2.Text = $"ReadSlot: {ReadSlot}{Environment.NewLine}Current Box Offset: {CurrentBoxOffset:X}{Environment.NewLine}Current Box: {CurrentBox + 1}{Environment.NewLine}Current Slot: {CurrentBoxSlot + 1}";
                }
                // StartFromOverworld can be true on first pass or if something went wrong last trade.
                if (FirstTry)
                {
                    FirstTry = false;
                    var fail = await GiftSetUp(token).ConfigureAwait(false);
                    if (fail)
                        return true;
                }
                else
                    await GiftSetUpForMulti(token).ConfigureAwait(false);

                pictureBox1.Image = null;
                pictureBox2.Image = null;
                pictureBox3.Image = null;
                if(textBox2.Visible)
                    textBox2.Text = string.Empty;
                var pk = await WaitForGift(CurrentBoxOffset, token).ConfigureAwait(false);
                bool sucess = false;
                if (filter.Checked)
                {
                    if (pk == null)
                        return false;

                    var Rate = SubForms.StopConditions.CalcRate(encounterFilters);
                    var DisplayRate = (1.00 - Math.Pow(1.00 - Rate, encounter)) * 100.00;
                    RateBox.Text = $"Target Rate: {DisplayRate:0.00}%";
                    for (int i = 0; i < encounterFilters.Count; i++)
                    {
                        LogUtil.LogText($"Filter: {encounterFilters[i].Name}, Enabled: {encounterFilters[i].Enabled}");
                        if (!encounterFilters[i].Enabled)
                            continue;
                        sucess = ValidateEncounter(pk, encounterFilters[i]);
                        if (sucess)
                        {
                            WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), true, simpleTrainerInfo);
                            if (pk.IsShiny)
                            {
                                textBox2.BackColor = Color.Gold;
                                textBox2.ForeColor = Color.BlueViolet;
                            }
                            else
                            {
                                textBox2.BackColor = Color.YellowGreen;
                                textBox2.ForeColor = Color.OrangeRed;
                            }
                            break;
                        }
                    }
                    if(!sucess)
                        WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), false, simpleTrainerInfo, isGift: true, Encounter:encounter, Rate: DisplayRate);
                }
                else if(pk != null)
                    WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, false), false, simpleTrainerInfo, isGift: true, Encounter: encounter);
                return sucess;
            }
            return true;
        }
        private async Task GiftSetUpForMulti(CancellationToken token)
        {
            await Click(A, 0_500, token).ConfigureAwait(false);
            var stopwatchgift = new Stopwatch();
            stopwatchgift.Restart();
            while (stopwatchgift.Elapsed <= TimeSpan.FromSeconds(3.5))
            {
                await Task.Delay(0_100, token).ConfigureAwait(false);
            }
            for (int i = 0; NonCode.Checked && i < (int)PresentNum.Value; i++)
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            await Click(A, 4_000, token).ConfigureAwait(false);
            stopwatchgift.Restart();
            while (stopwatchgift.Elapsed <= TimeSpan.FromSeconds(16.0))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
            }
            for (int i = 0; i < (Item.Checked ? 1 : 2); i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
            if (NonCode.Checked && MultiGift.Checked)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                await Click(B, 0_800, token).ConfigureAwait(false);
                await Click(A, 0_800, token).ConfigureAwait(false);
            }
        }
        private async Task<bool> GiftSetUp(CancellationToken token)
        {
            if (StartFromOverworld && !await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await RecoverToOverworld(token).ConfigureAwait(false);

            // Handles getting into the portal. Will retry this until successful.
            // if we're not starting from overworld, then ensure we're online before opening link trade -- will break the bot otherwise.
            // If we're starting from overworld, then ensure we're online before opening the portal.
            if (!StartFromOverworld && !await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
            {
                if (!await RecoverToPortal(token).ConfigureAwait(false))
                {
                    await RecoverToOverworld(token).ConfigureAwait(false);
                    StartFromOverworld = true;
                    return true;
                }
            }
            else if (StartFromOverworld && !await ConnectAndEnterPortal(token).ConfigureAwait(false))
            {
                if (!await ConnectAndEnterPortal(token).ConfigureAwait(false))
                {
                    await RecoverToOverworld(token).ConfigureAwait(false);
                    StartFromOverworld = true;
                    return true;
                }
            }

            await Click(A, 0_800, token).ConfigureAwait(false);
            while (!await IsInGiftMenu(token).ConfigureAwait(false))
                await Task.Delay(0_500, token).ConfigureAwait(false);
            if (!NonCode.Checked)
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            if (FirstConnect.Checked)
                await Click(A, (LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? (NonCode.Checked ? 0_800 : 0_500) : (NonCode.Checked ? 0_500 : 1_500), token).ConfigureAwait(false);

            // Connect online if not already.
            if (LinkAccount.Checked)
            {
                if (!await ConnectToOnline(token).ConfigureAwait(false))
                {
                    LogUtil.LogText("Failed to connect to online.");
                    return true; // Failed, either due to connection or softban.
                }
            }
            else
            {
                for (int i = 0; i <((LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 4 : 2); i++)
                    await Click(A, 0_500, token).ConfigureAwait(false);
                var stopwatch = new Stopwatch();
                stopwatch.Restart();
                while (stopwatch.Elapsed <= TimeSpan.FromSeconds(FirstConnect.Checked ? NonCode.Checked ? ((LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 5.3 : 5.5) : ((LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 5.5 : 5.0) : 6.2))
                {
                    await Task.Delay(0_100, token).ConfigureAwait(false);
                }
                if (FirstConnect.Checked)                
                    await Click(A,(LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 2_200 : 2_400, token).ConfigureAwait(false);                

                await Click(A, NonCode.Checked ? 3_000 : 1_500, token).ConfigureAwait(false);
            }
            if (!NonCode.Checked)
                await EnterCode(token).ConfigureAwait(false);

            for(int i = 0; NonCode.Checked && i < (int)PresentNum.Value; i++)            
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);            

            for (int i = 0; i < 4; i++)
                await Click(A, 4_500, token).ConfigureAwait(false);
            var stopwatchgift = new Stopwatch();
            stopwatchgift.Restart();
            while (stopwatchgift.Elapsed <= TimeSpan.FromSeconds(1.5))
            {
                await Task.Delay(0_100, token).ConfigureAwait(false);
            }
            for (int i = 0; i < (Item.Checked ? 1 : 2); i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
            if (NonCode.Checked && MultiGift.Checked)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                await Click(B, 0_800, token).ConfigureAwait(false);
                await Click(A, 0_800, token).ConfigureAwait(false);
            }
            if (!Repeatable.Checked)
            {
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 0_800, token).ConfigureAwait(false);
            }
            return false;
        }
        private async Task<bool> Exit(CancellationToken token)
        {
            if (NonCode.Checked)
            {
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 0_800, token).ConfigureAwait(false);
            }
            Stopwatch stopwatchgift = new();
            stopwatchgift.Restart();
            while (stopwatchgift.Elapsed <= TimeSpan.FromSeconds(3.0))
                await Task.Delay(0_100, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                StartFromOverworld = false;
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return true;
            }

            // Wait until we get into the box.
            await Task.Delay(1_000, token).ConfigureAwait(false);

            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return false;
        }
        private async Task PreReOpenGame(CancellationToken token)
        {
            await CloseGame(token).ConfigureAwait(false);
            await PreStartGame(token).ConfigureAwait(false);
        }

        private async Task ReOpenGame(CancellationToken token)
        {
            await CloseGame(token).ConfigureAwait(false);
            await StartGame(token).ConfigureAwait(false);
        }

        public async Task CloseGame(CancellationToken token)
        {
            // Close out of the game
            await Click(B, 0_150, token).ConfigureAwait(false);
            await Click(HOME, 2_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
        }
        private async Task DisconnectFromOnline(CancellationToken token)
        {
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 0_800, token).ConfigureAwait(false);
            Stopwatch stopwatch = new();
            stopwatch.Restart();
            while (stopwatch.Elapsed <= TimeSpan.FromSeconds(4.0))
                await Task.Delay(0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            // Wait until we get into the box.
            await Task.Delay(1_000, token).ConfigureAwait(false);

            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            StartFromOverworld = false;
        }
        private async Task<bool> ConnectToOnline(CancellationToken token)
        {
            if (await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
                return true;

            await Click(L, 1_000, token).ConfigureAwait(false);
            await Click(A, 4_000, token).ConfigureAwait(false);

            var wait = 0;
            while (!await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++wait > 30) // More than 15 seconds without a connection.
                    return false;
            }

            // There are several seconds after connection is established before we can dismiss the menu.
            await Task.Delay(3_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            return true;
        }
        private async Task PreStartGame(CancellationToken token)
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
            await Task.Delay(18_000, token).ConfigureAwait(false);

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

            var timer = 15_000;
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

            var offset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointer, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }
        private async Task<bool> PlayerNotOnMount(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }
        private async Task<bool> PlayerCannotMove(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var Data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return Data[0] == 0x00; // 0 nope else yes
        }
        private async Task<bool> IsConnectedOnline(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        private async Task<bool> IsInPokePortal(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x10;
        }
        private async Task<bool> IsInGiftMenu(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x14;
        }
        private async Task<bool> IsInBox(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var offset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 4;
        }
        private async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x11;
        }
        private void StopConditions_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new(setcolors, ref encounterFilters, filtermode);
            miniform.ShowDialog();
        }

        private void NonCode_CheckedChanged(object sender, EventArgs e)
        {
            if (NonCode.Checked)
            {
                GiftCode.Text = string.Empty; 
                MultiGift.Visible = true;
                GiftCode.Enabled = false;
                AutoPaste.Enabled = false;
            }
            else
            {
                MultiGift.Visible = false;
                GiftCode.Enabled = true;
                AutoPaste.Enabled = true;
            }
        }
        private void Filter_CheckedChanged(object sender, EventArgs e)
        {
            if (filter.Checked)
                Num.Enabled = false;
            else
                Num.Enabled = true;
        }
        private void AutoPaste_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();
            GiftCode.Text = string.Empty;
            string code = string.Empty;
            ClipBoardWatcher clipBoardWatcher = new();
            clipBoardWatcher.DrawClipBoard += (sender2, e2) =>
            {
                if(Clipboard.ContainsText())
                    code = Clipboard.GetText();
            };
            while (string.IsNullOrEmpty(code))
                Task.Run(async () => await Task.Delay(5_000, CancellationToken.None));
            clipBoardWatcher.Dispose();
            code = code.ToUpper().Trim().Replace(" ", "");
            var stroke = code.ToArray().Where(z => badVals.ToList().Contains(z.ToString())).ToList();
            if (stroke.Count > 0)
            {
                MessageBox.Show($"Serial Code Contains badchar!{Environment.NewLine}Serial Code: {code}");
                return;
            }
            if (code.Length > 16)
            {
                MessageBox.Show("Code Length is wrong!");
                return;
            }
            LogUtil.LogText($"serial code: {code}");
            GiftCode.Text = code;
            if (!string.IsNullOrEmpty(GiftCode.Text))
                RedeemButton_Click(sender, e);            
        }

        private async Task EnterCode(CancellationToken token)
        {
            var strokes = GiftCode.Text.ToUpper().Trim().ToArray();
            var number = $"NumPad";
            List<HidKeyboardKey> keystopress = new();
            foreach (var str in strokes)
            {
                if (badVals.Contains(str.ToString()))
                {
                    MessageBox.Show($"{str} is not a valid button. Stopping code entry.");
                    return;
                }
                foreach (HidKeyboardKey keypress in (HidKeyboardKey[])Enum.GetValues(typeof(HidKeyboardKey)))
                {
                    if (str.ToString().Equals(keypress.ToString()) || (number + str.ToString()).Equals(keypress.ToString()))
                        keystopress.Add(keypress);
                }
            }

            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            for (int i = 0; i < keystopress.Count; i++)
            {
                await Executor.SwitchConnection.SendAsync(SwitchCommand.TypeKey(keystopress[i], true), token).ConfigureAwait(false);
                await Task.Delay(0_550, token).ConfigureAwait(false);
            }
            //await Executor.SwitchConnection.SendAsync(SwitchCommand.TypeMultipleKeys(keystopress, Executor.UseCRLF), token).ConfigureAwait(false);
            await Click(PLUS, 0_500, token).ConfigureAwait(false);
            await Click(PLUS, 0_500, token).ConfigureAwait(false);

            await Task.Delay(5_000).ConfigureAwait(false);            
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

        private async Task<PK9?> WaitForGift(ulong Offset, CancellationToken token)
        {
            var pk = new PK9();
            var attempts = 0;
            if (!Item.Checked)
            {
                pk = await ReadGiftPokemon(Offset, token).ConfigureAwait(false);
                while (pk == null)
                {
                    await Task.Delay(0_050).ConfigureAwait(false);
                    pk = await ReadGiftPokemon(Offset, token).ConfigureAwait(false);
                    attempts++;
                    if (attempts >= 60)
                        break;
                }
                if (pk != null)
                {
                    encounter++;
                    var client = new HttpClient();
                    string output = wideViewerSV.GetRealPokemonString(pk);
                    textBox2.Text = $"Encounter #{encounter}{Environment.NewLine}" + output;
                    var sprite = PokeImg(pk, false);
                    var response = await client.GetStreamAsync(sprite, token).ConfigureAwait(false);
                    Image img = Image.FromStream(response);
                    var img2 = (Image)new Bitmap(img, new Size(img.Width, img.Height));
                    if (pk.IsShiny)
                    {
                        Image? shiny = pk.ShinyXor == 0 ? ShinySquare : ShinyStar;
                        shiny = new Bitmap(shiny!, new Size(shiny!.Width * 3 / 8, shiny!.Height * 3 / 8));
                        img2 = ImageUtil.LayerImage(img2, shiny, 105, 5);
                    }
                    pictureBox2.Image = img2;
                    pictureBox3.Image = GetGemImage((int)pk.TeraType);
                    if (HasRibbon(pk, out RibbonIndex mark))
                    {
                        string url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/ribbon{mark.ToString().ToLower()}.png";
                        pictureBox1.Load(url);
                    }
                }
            }
            else
            {
                try
                {
                    var DiffItems = await itemStructure.GetDiffItems(Itemdata, token).ConfigureAwait(false);
                    await DGV_View.Populate(DiffItems, simpleTrainerInfo.Language).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    LogUtil.LogError(ex.ToString(), "Failed to populate DGV_View with item data.");                    
                }
            }

            if (Item.Checked)
                return null;
            return pk;
        }
        private void Item_CheckedChanged(object sender, EventArgs e)
        {
            if (Item.Checked)
            {
                textBox2.Visible = false;
                DGV_View.Visible = true;
                filter.Checked = false;
            }
            else
            {
                DGV_View.Visible = false;
                textBox2.Visible = true;
            }
            filter.Enabled = !Item.Checked;
        }

        private async Task<(bool, ulong, byte, int)> ReadEmptySlot(bool ReadSlot, byte CurrentBox, int CurrentSlot, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var BoxSize = 30 * BoxSlotSize;
            var wait = 0;
            while (CurrentBoxIndexOffset == 0)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                wait++;
                CurrentBoxIndexOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.CurrentBoxPointer, token).ConfigureAwait(false);
                if (wait >= 15)
                    break;
            }
            wait = 0;
            while (BoxStartOffset == 0)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                wait++;
                BoxStartOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.BoxStartPointer, token).ConfigureAwait(false);
                if (wait >= 15)
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
        private async Task<PK9?> ReadGiftPokemon(ulong Slot, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(Slot, BoxSlotSize, token).ConfigureAwait(false);
            var pk = new PK9(data);
            bool isvalid = PersonalTable.SV.IsPresentInGame(pk.Species, pk.Form);
            if (isvalid && pk != null && pk.ChecksumValid && pk.Valid && pk.Species > 0 && (Species)pk.Species <= Species.MAX_COUNT)
                return pk;
            return null;
        }
        private async void RedeemButton_Click(object sender, EventArgs e)
        {
            using (cts = new CancellationTokenSource())
            {
                encounter = 0;
                canceled = false;
                var token = cts.Token;
                FirstTry = true;
                textBox2.BackColor = setcolors.Item1;
                textBox2.ForeColor = setcolors.Item2;
                if(RedeemButton.Enabled)
                    DisableAssets();
                try
                {
                    if (GiftCode.Text.Length > 16)
                    {
                        MessageBox.Show($"{GiftCode.Text} is not a valid code entry. Please try again.");
                        EnableAssets();
                        canceled = true;
                        return;
                    }

                    await AnticipateResponse(token).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(GiftCode.Text))
                    {
                        if (filter.Checked)
                        {
                            var success = false;
                            do
                            {
                                success = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                await RecoverToOverworld(token).ConfigureAwait(false);
                                await ResetMysteryGift(token).ConfigureAwait(false);
                            }
                            while (!success);
                        }
                        else
                        {
                            if (Num.Value <= 1)
                                await PerformLinkCodeTrade(token).ConfigureAwait(false);
                            else
                                for (int i = 0; i < Num.Value; i++)
                                {
                                    var check = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                    if (check)
                                        break;
                                    await RecoverToOverworld(token).ConfigureAwait(false);
                                    await ResetMysteryGift(token).ConfigureAwait(false);
                                }
                        }
                        await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                        canceled = true;
                    }
                    else
                    {
                        if (Repeatable.Checked)
                        {
                            RateBox.Text = string.Empty;
                            var success = false;
                            do
                            {
                                success = await RedeemMultiGifts(token).ConfigureAwait(false);
                            } while (!success);
                            await DisconnectFromOnline(token).ConfigureAwait(false);
                            await RecoverToOverworld(token).ConfigureAwait(false);
                            StartFromOverworld = true;
                        }
                        else if(filter.Checked && NonCode.Checked)
                        {
                            var success = false;
                            do
                            {
                                success = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                await RecoverToOverworld(token).ConfigureAwait(false);
                                await ResetMysteryGift(token).ConfigureAwait(false);
                            }
                            while (!success);
                        }
                        else if(NonCode.Checked)
                        {
                            if (Num.Value <= 1)
                                await PerformLinkCodeTrade(token).ConfigureAwait(false);
                            else
                                for (int i = 0; i < Num.Value; i++)
                                {
                                    var check = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                    if (check)
                                        break;
                                    await RecoverToOverworld(token).ConfigureAwait(false);
                                    await ResetMysteryGift(token).ConfigureAwait(false);
                                }                            
                        }
                        else
                        {
                            await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                            await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                            MessageBox.Show("TextBox is empty. Try again after you fill it in!");
                            canceled = true;
                            EnableAssets();
                            return;
                        }
                        await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                        canceled = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                    MessageBox.Show("The Process has been canceled!");
                    canceled = true;
                    EnableAssets();
                    return;
                }
                catch(Exception ex)
                {
                    LogUtil.LogError(ex.ToString(), "Redeem Error");
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
                        await RecoverToOverworld(token).ConfigureAwait(false);
                        if(!Repeatable.Checked)
                            await ResetMysteryGift(token).ConfigureAwait(false);
                    }
                    catch (SocketException err)
                    {
                        MessageBox.Show(this, err.ToString(), "ReConnect Sokcket Exception!");
                        EnableAssets();
                        return;
                    }
                    catch (Exception RecEx)
                    {
                        MessageBox.Show(this, RecEx.ToString(), "ReConnect Exception!");
                        EnableAssets();
                        return;
                    }
                    RedeemButton_Click(sender, e);
                    return;
                    /*MessageBox.Show(ex.Message);
                    canceled = true;
                    EnableAssets();
                    return;*/
                }
                await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                MessageBox.Show("The process has been completed!");
                EnableAssets();
            }
        }
        private new async Task Click(SwitchButton b, int delay, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            await Executor.SwitchConnection.SendAsync(SwitchCommand.Click(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        // Read, Decrypt, and Write Block tasks from Tera-Finder/RaidCrawler/sv-livemap.
        #region saveblocktasks
        private void ClearButton_Click(object sender, EventArgs e)
        {
            GiftCode.Text = string.Empty;
        }
        private ulong GetDateTime(byte[] data)
        {
            Epoch1900DateTimeValue epoch1900DateTime = new(data);
            return epoch1900DateTime.TotalSeconds;
        }
        private async Task<(ulong, ulong)> GetLastSaveTime(ulong init, CancellationToken token)
        {
            var data = await ReadEncryptedBlockObject(StaticViewer.BlocksOverworld.LastSaved, init, token).ConfigureAwait(false);
            var LastSavedTime = GetDateTime(data.Item1);
            return (data.Item2, LastSavedTime);
        }
        private static byte[] DecryptBlock(uint key, byte[] block)
        {
            var rng = new SCXorShift32(key);
            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)(block[i] ^ rng.Next());
            return block;
        }
        private async Task<(byte[], ulong)> ReadEncryptedBlockHeader(DataBlock block, ulong init, CancellationToken token)
        {
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
        private async Task<(byte, ulong)> ReadEncryptedBlockByte(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (header[1], address);
        }
        private async Task<(int, ulong)> ReadEncryptedBlockInt32(DataBlock block, ulong init, CancellationToken token)
        {
            var (header, address) = await ReadEncryptedBlockHeader(block, init, token).ConfigureAwait(false);
            return (ReadInt32LittleEndian(header.AsSpan()[1..]), address);
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
            while (init == 0)
            {
                var address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                init = address;
            }
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(init, 5 + block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data)[5..];

            return (res, init);
        }
        private async Task<bool> WriteBlock(object data, DataBlock block, CancellationToken token, object? toExpect = default)
        {
            if (block.IsEncrypted)
                return await WriteEncryptedBlockSafe(block, toExpect, data, token).ConfigureAwait(false);
            else
                return await WriteDecryptedBlock((byte[])data!, block, token).ConfigureAwait(false);
        }
        private async Task<bool> WriteDecryptedBlock(byte[] data, DataBlock block, CancellationToken token)
        {
            var Offset = await Executor.SwitchConnection.PointerAll(block.Pointer!, token).ConfigureAwait(false);
            while (Offset == 0)
            {
                await Task.Delay(0_100).ConfigureAwait(false);
                Offset = await Executor.SwitchConnection.PointerAll(block.Pointer!, token).ConfigureAwait(false);
            }
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, Offset, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockSafe(DataBlock block, object? toExpect, object toWrite, CancellationToken token)
        {
            if (toExpect == default || toWrite == default)
                return false;

            return block.Type switch
            {
                SCTypeCode.Object => await WriteEncryptedBlockObject(block, (byte[])toExpect, (byte[])toWrite, token).ConfigureAwait(false),
                SCTypeCode.Array => await WriteEncryptedBlockArray(block, (byte[])toExpect, (byte[])toWrite, token).ConfigureAwait(false),
                SCTypeCode.Bool1 or SCTypeCode.Bool2 or SCTypeCode.Bool3 => await WriteEncryptedBlockBool(block, (bool)toExpect, (bool)toWrite, token).ConfigureAwait(false),
                SCTypeCode.Byte or SCTypeCode.SByte => await WriteEncryptedBlockByte(block, (byte)toExpect, (byte)toWrite, token).ConfigureAwait(false),
                SCTypeCode.UInt32 or SCTypeCode.UInt64 => await WriteEncryptedBlockUint(block, (uint)toExpect, (uint)toWrite, token).ConfigureAwait(false),
                SCTypeCode.Int32 => await WriteEncryptedBlockInt32(block, (int)toExpect, (int)toWrite, token).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Block {block.Name} (Type {block.Type}) is currently not supported.")
            };
        }
        private async Task<bool> WriteEncryptedBlockUint(DataBlock block, uint valueToExpect, uint valueToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = ReadUInt32LittleEndian(header.AsSpan()[1..]);
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            WriteUInt32LittleEndian(header.AsSpan()[1..], valueToInject);
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockInt32(DataBlock block, int valueToExpect, int valueToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = ReadInt32LittleEndian(header.AsSpan()[1..]);
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            WriteInt32LittleEndian(header.AsSpan()[1..], valueToInject);
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockByte(DataBlock block, byte valueToExpect, byte valueToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = header[1];
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            header[1] = valueToInject;
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockArray(DataBlock block, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[6..];
            if (!ram.SequenceEqual(arrayToExpect)) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 6, block.Size);
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockBool(DataBlock block, bool valueToExpect, bool valueToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[0] == 2;
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            data[0] = valueToInject ? (byte)2 : (byte)1;
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        private async Task<bool> WriteEncryptedBlockObject(DataBlock block, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
        {
            ulong address;
            try
            {
                address = await SearchSaveKey(block.Key, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[5..];
            if (!ram.SequenceEqual(arrayToExpect))
                return false;
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 5, block.Size);
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        private static byte[] EncryptBlock(uint key, byte[] block) => DecryptBlock(key, block);
        private async Task<ulong> SearchSaveKey(uint key, CancellationToken token)
        {
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
        #endregion
        private void EnableAssets()
        {
            Item.Enabled = true;
            ResetMysteryGifts.Enabled = true;
            RedeemButton.Enabled = true;
            OverShoot.Enabled = true;
            GiftCode.Enabled = !NonCode.Checked;
            AutoPaste.Enabled = !NonCode.Checked;
            ClearButton.Enabled = true;
            HasReset.Enabled = true;
            FirstConnect.Enabled = true;
            NonCode.Enabled = true;
            LinkAccount.Enabled= true;
            filter.Enabled = true;
            Num.Enabled = !filter.Checked;
            Repeatable.Enabled = true;
            PresentNum.Enabled = true;
            if (MultiGift.Visible)
                MultiGift.Enabled = true;
        }
        private void DisableAssets()
        {
            AutoPaste.Enabled = false;
            Item.Enabled = false;
            ResetMysteryGifts.Enabled = false;
            RedeemButton.Enabled = false;
            OverShoot.Enabled = false;
            GiftCode.Enabled = false;
            ClearButton.Enabled = false;
            HasReset.Enabled = false;
            FirstConnect.Enabled = false;
            NonCode.Enabled = false;
            LinkAccount.Enabled = false;
            Num.Enabled = false;
            filter.Enabled = false;
            Repeatable.Enabled = false;
            PresentNum.Enabled = false;
            if (MultiGift.Visible)
                MultiGift.Enabled = false;
        }
    }
}
