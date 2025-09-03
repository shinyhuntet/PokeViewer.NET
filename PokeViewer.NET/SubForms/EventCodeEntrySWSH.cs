using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System;
using static SysBot.Base.SwitchButton;
using static System.Buffers.Binary.BinaryPrimitives;
using static PokeViewer.NET.RoutineExecutor;
using static PokeViewer.NET.ViewerUtil;
using Microsoft.VisualBasic;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using System.Security.Policy;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics;
using RaidCrawler.Core.Structures;
using Newtonsoft.Json.Linq;
using PokeViewer.NET.Misc;
using PokeViewer.NET.Util;
using System.Net.Sockets;

namespace PokeViewer.NET.SubForms
{
    public partial class EventCodeEntrySWSH : Form
    {
        private readonly ViewerState Executor;
        private readonly WideViewerSV wideViewerSV;
        private readonly SimpleTrainerInfo simpleTrainerInfo;
        private readonly ItemStructure ItemStructure;
        private List<InventoryPouch> ItemData = [];
        protected ViewerOffsets ViewerOffsets { get; } = new();
        public EventCodeEntrySWSH(int GameType, ref ViewerState executor, (Color, Color) color, SimpleTrainerInfo trainerInfo)
        {
            wideViewerSV = new(GameType, ref executor, color, trainerInfo);
            simpleTrainerInfo = trainerInfo;
            InitializeComponent();
            Executor = executor;
            ItemStructure = new(Executor);
            filtermode = FilterMode.MysteryGift_SWSH;
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
        private uint CurrentBoxOffset = 0;
        private ulong OverworldOffset = 0;
        private int BoxSlotSize = 0x158;
        private bool ReadSlot = false;
        private byte CurrentBox = 0;
        private int CurrentBoxSlot = 0;
        private CancellationTokenSource? cts = null;
        private bool canceled = false;
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
        private bool ValidateEncounter(PK8 pk, EncounterFilter filter)
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

            if (filter.Scale && (pk.HeightScalar > 0 || pk.HeightScalar < 255))
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
                MessageBox.Show($"The Process has already been caceled!");
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
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var MysteryRecords = await Executor.SwitchConnection.ReadBytesAsync(ViewerOffsets.MytsteryGiftOffset, ViewerOffsets.MysteryGiftArraySize, token).ConfigureAwait(false);
            var inject = new byte[ViewerOffsets.MysteryGiftArraySize];
            if (!MysteryRecords.SequenceEqual(inject))
            {
                await Executor.SwitchConnection.WriteBytesAsync(inject, ViewerOffsets.MytsteryGiftOffset, token).ConfigureAwait(false);
                LogUtil.LogText("Reset Mystery records!");
            }
            await SaveGame(token).ConfigureAwait(false);
        }
        public async Task SaveGame(CancellationToken token)
        {
            // Open the menu and save.
            await Click(X, 1_500, token).ConfigureAwait(false);
            await Click(R, 2_000, token).ConfigureAwait(false);
            await Click(A, 3_500, token).ConfigureAwait(false);

        }
        private async Task<bool> ConnectAndEnterPortal(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointerSWSH, token).ConfigureAwait(false);
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

            await Click(A, 0_800, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        private async Task ExitTradeToPortal(bool unexpected, CancellationToken token)
        {
            if (await IsInMenu(token).ConfigureAwait(false))
                return;

            if (unexpected)
                LogUtil.LogText("Unexpected behavior, recovering to Portal.");

            // Wait for the portal to load.
            LogUtil.LogText("Waiting on the portal to load...");
            var attempts = 0;
            while (!await IsInMenu(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                if (await IsInMenu(token).ConfigureAwait(false))
                    break;

                if (attempts++ > 15)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                // Didn't make it into the portal for some reason.
                if (++attempts > 40)
                {
                    LogUtil.LogText("Failed to load the portal, rebooting the game.");
                    if (!await RecoverToOverworld(token).ConfigureAwait(false))                    
                        await ReOpenGame(token).ConfigureAwait(false);
                        
                    await ConnectAndEnterPortal(token).ConfigureAwait(false);
                    return;
                }
            }
            await Task.Delay(0_500, token).ConfigureAwait(false);
        }
        private async Task<bool> IsInMenu(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = BitConverter.ToUInt32(await Executor.SwitchConnection.ReadBytesAsync(ViewerOffsets.CurrentScreenOffset, 4, token).ConfigureAwait(false));
            return data == ViewerOffsets.MenuScreen;
        }
        private async Task<bool> IsInMysteryGift(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = BitConverter.ToUInt32(await Executor.SwitchConnection.ReadBytesAsync(ViewerOffsets.CurrentScreenOffset, 4, token).ConfigureAwait(false));
            return data == ViewerOffsets.MysteryGiftScreen;
        }
        private async Task<bool> IsInOverwoldScreen(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = BitConverter.ToUInt32(await Executor.SwitchConnection.ReadBytesAsync(ViewerOffsets.CurrentScreenOffset, 4, token).ConfigureAwait(false));
            return data == ViewerOffsets.OverworldScreen;
        }
        private async Task<bool> RecoverToOverworld(CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointerSWSH, token).ConfigureAwait(false);
            var wait = 0;
            while (OverworldOffset == 0)
            {
                await Task.Delay(0_300, token).ConfigureAwait(false);
                wait++;
                OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointerSWSH, token).ConfigureAwait(false);
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

            OverworldOffset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointerSWSH, token).ConfigureAwait(false);
            if (ItemGift.Checked)
                return;
            if (CurrentBox != 0 || CurrentBoxSlot != 0)
                ReadSlot = true;
            else
                ReadSlot = false;
        }

        private async Task<bool> RecoverToPortal(CancellationToken token)
        {
            LogUtil.LogText("Reorienting to Poké Portal.");
            var attempts = 0;
            while (!await IsInMenu(token).ConfigureAwait(false))
            {
                await Click(B, 1_500, token).ConfigureAwait(false);
                if (++attempts >= 30)
                {
                    LogUtil.LogText("Failed to recover to Poké Portal.");
                    return false;
                }
            }

            // Should be in the X menu hovered over Poké Portal.
            await Click(A, 0_800, token).ConfigureAwait(false);

            return await SetUpPortalCursor(token).ConfigureAwait(false);
        }

        // Waits for the Portal to load (slow) and then moves the cursor down to Link Trade.
        private async Task<bool> SetUpPortalCursor(CancellationToken token)
        {
            // Wait for the portal to load.
            var attempts = 0;
            while (!await IsInMysteryGift(token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
                if (++attempts > 20)
                {
                    LogUtil.LogText("Failed to load the Poké Portal.");
                    return false;
                }
            }
            await Task.Delay(0_500 + (int)OverShoot.Value, token).ConfigureAwait(false);

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
                textBox2.Text = string.Empty;
                await InitializeSessionOffsets(token).ConfigureAwait(false);
                if (ItemGift.Checked)
                {
                    ItemData = await ItemStructure.ReadGiftItemSWSH(token).ConfigureAwait(false);
                }
                else
                {
                    (ReadSlot, CurrentBoxOffset, CurrentBox, CurrentBoxSlot) = await ReadEmptySlot(ReadSlot, CurrentBox, CurrentBoxSlot, token).ConfigureAwait(false);
                    if (!ReadSlot)
                        return true;

                    textBox2.Text = $"ReadSlot: {ReadSlot}{Environment.NewLine}Current Box Offset: {CurrentBoxOffset:X}{Environment.NewLine}Current Box: {CurrentBox + 1}{Environment.NewLine}Current Slot: {CurrentBoxSlot + 1}";
                }
                // StartFromOverworld can be true on first pass or if something went wrong last trade.
                var fail = await GiftSetUp(token).ConfigureAwait(false);
                if (fail)
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
                            WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), true, simpleTrainerInfo);
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
                        WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), false, simpleTrainerInfo, isGift: true, Encounter: encounter, Rate: DisplayRate);
                }
                else if (pk != null)
                    WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), false, simpleTrainerInfo, isGift: true, Encounter: encounter);
                return sucess;
            }
            return true;
        }
        private async Task<bool> RedeemMultiGifts(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                await InitializeSessionOffsets(token).ConfigureAwait(false);
                if(ItemGift.Checked)
                {
                    ItemData = await ItemStructure.ReadGiftItemSWSH(token).ConfigureAwait(false);
                }
                else
                {
                    (ReadSlot, CurrentBoxOffset, CurrentBox, CurrentBoxSlot) = await ReadEmptySlot(ReadSlot, CurrentBox, CurrentBoxSlot, token).ConfigureAwait(false);
                    if (!ReadSlot)
                        return true;
                    //textBox2.Text = $"ReadSlot: {ReadSlot}{Environment.NewLine}Current Box Offset: {CurrentBoxOffset:X}{Environment.NewLine}Current Box: {CurrentBox + 1}{Environment.NewLine}Current Slot: {CurrentBoxSlot + 1}";
                }
                    // StartFromOverworld can be true on first pass or if something went wrong last trade.
                    var fail = await GiftSetUp(token).ConfigureAwait(false);
                if (fail)
                    return true;
                StartFromOverworld = false;

                pictureBox1.Image = null;
                pictureBox2.Image = null;
                pictureBox3.Image = null;
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
                            WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), true, simpleTrainerInfo);
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
                        WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), false, simpleTrainerInfo, isGift: true, Encounter: encounter, Rate:DisplayRate);
                }
                else if(pk != null)
                    WebHookUtil.SendDetailNotifications(pk, PokeImg(pk, pk.CanGigantamax), false, simpleTrainerInfo, isGift: true, Encounter: encounter);
                return sucess;
            }
            return true;
        }
        private async Task<bool> GiftSetUp(CancellationToken token)
        {
            if (StartFromOverworld && !await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await RecoverToOverworld(token).ConfigureAwait(false);

            // Handles getting into the portal. Will retry this until successful.
            // if we're not starting from overworld, then ensure we're online before opening link trade -- will break the bot otherwise.
            // If we're starting from overworld, then ensure we're online before opening the portal.
            if (!StartFromOverworld && !await IsConnectedOnline(ViewerOffsets.IsConnectedOffset, token).ConfigureAwait(false))
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
            await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            if (!NonCode.Checked)
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            if (!LinkAccount.Checked)
            {
                await Task.Delay((LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 3_000 : 4_000, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
            }
            await Task.Delay((LanguageID)simpleTrainerInfo.Language == LanguageID.Japanese ? 4_500 : 5_500, token).ConfigureAwait(false);
            for (int i = 1; i < GiftIndex.Value; i++)
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
            while (await IsInMysteryGift(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
            while (!await IsInMysteryGift(token).ConfigureAwait(false))
            {
                await Task.Delay(0_500, token).ConfigureAwait(false);
            }
            await Task.Delay(0_500, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_800, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            while (!await IsInMenu(token).ConfigureAwait(false))
                await Task.Delay(0_500, token).ConfigureAwait(false);
            return false;
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
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            StartFromOverworld = false;
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

            var offset = await Executor.SwitchConnection.PointerAll(ViewerOffsets.OverworldPointerSWSH, token).ConfigureAwait(false);
            if (offset == 0)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }
        private async Task<bool> IsConnectedOnline(uint offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }
        private async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }
        private void StopConditions_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new(setcolors, ref encounterFilters, filtermode);
            miniform.ShowDialog();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            GiftCode.Text = string.Empty;
        }
        private void NonCode_CheckedChanged(object sender, EventArgs e)
        {
            if (NonCode.Checked)
            {
                GiftCode.Enabled = false;
                AutoPaste.Enabled = false;
            }
            else
            {
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
                if (Clipboard.ContainsText())
                    code = Clipboard.GetText();
            };
            while (string.IsNullOrEmpty(code))            
                Task.Run(async() => await Task.Delay(5_000, CancellationToken.None));
            clipBoardWatcher.Dispose();
            code = code.ToUpper().Trim().Replace(" ", "");
            var stroke = code.ToArray().Where(z => badVals.ToList().Contains(z.ToString())).ToList();
            if(stroke.Count > 0)
            {
                MessageBox.Show($"Serial Code Contains badchar!{Environment.NewLine}Serial Code: {code}");
                return;
            }
            if(code.Length > 16)
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
                await Task.Delay(0_700, token).ConfigureAwait(false);
            }
            //await Executor.SwitchConnection.SendAsync(SwitchCommand.TypeMultipleKeys(keystopress, Executor.UseCRLF), token).ConfigureAwait(false);
            await Click(PLUS, 0_500, token).ConfigureAwait(false);
            await Click(PLUS, 0_500, token).ConfigureAwait(false);

            await Task.Delay(6_500).ConfigureAwait(false);
        }
        private async Task<Image?> GetGemImage(bool isGmax, CancellationToken token)
        {
            Image img = null!;
            if (isGmax)
            {
                var url = $"https://raw.githubusercontent.com/zyro670/PokeTextures/main/OriginMarks/icon_daimax.png";
                using (HttpClient client = new())
                {
                    using var response = await client.GetStreamAsync(url, token).ConfigureAwait(false);
                    img = Image.FromStream(response);
                }
            }
            return img;
        }

        private async Task<PK8?> WaitForGift(uint Offset, CancellationToken token)
        {
            var pk = new PK8();
            if (!ItemGift.Checked)
            {
                var attempts = 0;
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
                    var sprite = PokeImg(pk, pk.CanGigantamax);
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
                    pictureBox3.Image = await GetGemImage(pk.CanGigantamax, token).ConfigureAwait(false);
                    if (HasRibbon(pk, out RibbonIndex mark))
                    {
                        string url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/ribbon{mark.ToString().ToLower()}.png";
                        pictureBox1.Load(url);
                    }
                }
                else
                {
                    var DiffItems = await ItemStructure.GetDiffItemsSWSH(ItemData, token).ConfigureAwait(false);
                    await DGV_View.Populate(DiffItems, simpleTrainerInfo.Language).ConfigureAwait(false);
                }
            }

            return pk;
        }
        private void ItemGift_CheckedChanged(object sender, EventArgs e)
        {
            if (ItemGift.Checked)
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
            filter.Enabled = !ItemGift.Checked;
        }
        private async Task<(bool, uint, byte, int)> ReadEmptySlot(bool ReadSlot, byte CurrentBox, int CurrentSlot, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var BoxSize = 30 * BoxSlotSize;
            if (!ReadSlot)
            {
                var data = await Executor.SwitchConnection.ReadBytesAsync(ViewerOffsets.CurrentBoxOffset, 1, token).ConfigureAwait(false);
                CurrentBox = data[0];
                CurrentSlot = 0;
            }
            while (CurrentBox <= 31)
            {
                var boxstart = ViewerOffsets.BoxStartOffset + (uint)(CurrentBox * BoxSize) + (uint)(CurrentSlot * BoxSlotSize);
                var pokedata = await Executor.SwitchConnection.ReadBytesAsync(boxstart, BoxSlotSize, token).ConfigureAwait(false);
                var pkm = new PK9(pokedata);
                bool isValid = PersonalTable.SWSH.IsPresentInGame(pkm.Species, pkm.Form);
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
        private async Task<PK8?> ReadGiftPokemon(uint Slot, CancellationToken token)
        {
            if (!Executor.SwitchConnection.Connected)            
                Executor.SwitchConnection.Reset();

            var data = await Executor.SwitchConnection.ReadBytesAsync(Slot, BoxSlotSize, token).ConfigureAwait(false);
            var pk = new PK8(data);
            bool isvalid = PersonalTable.SWSH.IsPresentInGame(pk.Species, pk.Form);
            if (isvalid && pk != null && pk.ChecksumValid && pk.Valid && pk.Species > 0 && pk.Species <= PersonalTable.SWSH.MaxSpeciesID)
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
                StartFromOverworld = true;
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
                            StartFromOverworld = true;
                        }
                        else
                        {
                            if (Num.Value <= 1)
                                await PerformLinkCodeTrade(token).ConfigureAwait(false);
                            else
                                for (int i = 0; i < Num.Value; i++)
                                {
                                    var check = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                    await RecoverToOverworld(token).ConfigureAwait(false);
                                    await ResetMysteryGift(token).ConfigureAwait(false);
                                    StartFromOverworld = true;
                                    if (check)
                                        break;
                                }
                        }
                        await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        if (Repeatable.Checked)
                        {
                            var success = false;
                            var count = 0;
                            RateBox.Text = string.Empty;
                            do
                            {
                                success = await RedeemMultiGifts(token).ConfigureAwait(false);
                                count++;
                                if (ItemGift.Checked && count >= Num.Value)
                                    break;
                            } while (!success);
                            await DisconnectFromOnline(token).ConfigureAwait(false);
                            await RecoverToOverworld(token).ConfigureAwait(false);
                        }
                        else if(filter.Checked)
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
                            for (int i = 0; i < Num.Value; i++)
                            {
                                var check = await PerformLinkCodeTrade(token).ConfigureAwait(false);
                                await RecoverToOverworld(token).ConfigureAwait(false);
                                await ResetMysteryGift(token).ConfigureAwait(false);
                                if (check)
                                    break;
                            }
                        }
                        await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                        StartFromOverworld = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                    MessageBox.Show("The Process has been canceled!");
                    canceled = true;
                }
                catch (Exception)
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
                    try
                    {
                        await Task.Delay(300_000, token).ConfigureAwait(false);
                        Executor.SwitchConnection.Reset();
                        if (!Executor.SwitchConnection.Connected)
                            throw new Exception("SwitchConnection can't reconnect!");
                        await RecoverToOverworld(token).ConfigureAwait(false);
                        if (!Repeatable.Checked)
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
                    /*MessageBox.Show(this, ex.ToString());
                    canceled = true;*/
                }
                if(Executor.SwitchConnection.Connected)
                    await Executor.DetachController(CancellationToken.None).ConfigureAwait(false);
                if (!canceled)
                {
                    MessageBox.Show("The process has been completed!");
                    canceled = true;
                }
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

        private void EnableAssets()
        {
            ResetMysteryGifts.Enabled = true;
            RedeemButton.Enabled = true;
            OverShoot.Enabled = true;
            GiftCode.Enabled = !NonCode.Checked;
            AutoPaste.Enabled = !NonCode.Checked;
            ClearButton.Enabled = true;
            NonCode.Enabled = true;
            ItemGift.Enabled = true;
            LinkAccount.Enabled= true;
            filter.Enabled = !ItemGift.Checked;
            Num.Enabled = !filter.Checked;
            Repeatable.Enabled = true;
            GiftIndex.Enabled = true;
        }
        private void DisableAssets()
        {
            AutoPaste.Enabled = false;
            ResetMysteryGifts.Enabled = false;
            RedeemButton.Enabled = false;
            OverShoot.Enabled = false;
            GiftCode.Enabled = false;
            ClearButton.Enabled = false;
            NonCode.Enabled = false;
            LinkAccount.Enabled = false;
            Num.Enabled = false;
            ItemGift.Enabled = false;
            filter.Enabled = false;
            Repeatable.Enabled = false;
            GiftIndex.Enabled = false;
        }
    }
}
