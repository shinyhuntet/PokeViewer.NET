using PKHeX.Core;
using RaidCrawler.Core.Connection;
using SysBot.Base;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeViewer.NET.Misc;

public class ItemStructure
{
    private readonly ViewerState Executor;
    private ulong ItemBlockOffset;
    private ViewerOffsets Offsets = new();
    public ItemStructure(ViewerState executor)
    {
        Executor = executor;
    }
    private List<InventoryItem> GrabAllDiffItems(InventoryItem[] NewItems, InventoryItem[] OldItems)
    {
        List<InventoryItem> ChangedItems = [];
        var Diffitems = NewItems.Where(z => !OldItems.Contains(z) && z.Index > 0).ToArray();
        foreach (var item in Diffitems)
        {
            var CountChangeItem = OldItems.Where(z => z.Index == item.Index).FirstOrDefault();
            if (CountChangeItem == null)
                continue;
            item.Count -= CountChangeItem.Count;
            LogUtil.LogText($"Item {GameInfo.GetStrings("en").itemlist[item.Index]}({item.Index}) count is changed by {item.Count}(from {CountChangeItem.Count} to {item.Count + CountChangeItem.Count}).");
            ChangedItems.Add(item);
        }
        return ChangedItems;

    }
    private async Task<SAV9SV> GetFakeTrainerSAVSV(CancellationToken token)
    {
        if (!Executor.SwitchConnection.Connected)            
            Executor.SwitchConnection.Reset();

        var sav = new SAV9SV();
        var info = sav.MyStatus;
        var read = await Executor.SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointerSV, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;
    }
    public async Task<SAV8SWSH> GetFakeTrainerSAVSWSH(CancellationToken token)
    {
        var sav = new SAV8SWSH();
        var info = sav.MyStatus;
        var read = await Executor.SwitchConnection.ReadBytesAsync(Offsets.TrainerDataOffsetSWSH, Offsets.TrainerDataLengthSWSH, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;    
    }
    public async Task<InventoryPouch> GetPouches(CancellationToken token)
    {
        SAV9SV TrainerSAV = await GetFakeTrainerSAVSV(token).ConfigureAwait(false);
        var ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        while (ItemOffset == 0)
        {
            await Task.Delay(0_050, token).ConfigureAwait(false);
            ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        }
        var items = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(ItemOffset, TrainerSAV.Items.Data.Length, token).ConfigureAwait(false);
        items.CopyTo(TrainerSAV.Items.Data);
        var pouches = TrainerSAV.Inventory;
        var ingredients = pouches[7];
        return ingredients;
    }
    public async Task<InventoryPouch?> GetBag(InventoryType type, CancellationToken token)
    {
        SAV9SV TrainerSAV = await GetFakeTrainerSAVSV(token).ConfigureAwait(false);
        var ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        while (ItemOffset == 0)
        {
            await Task.Delay(0_050, token).ConfigureAwait(false);
            ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        }
        var items = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(ItemOffset, TrainerSAV.Items.Data.Length, token).ConfigureAwait(false);
        items.CopyTo(TrainerSAV.Items.Data);
        var pouches = TrainerSAV.Inventory;
        foreach(var pouch in pouches)
        {
            if (pouch.Type == type)
                return pouch;
        }
        return null;
    }
    public InventoryItem[] GetItems(InventoryPouch pouch) => pouch.Items.Where(z => z.Index > 0 && z.Count > 0).ToArray();
    public List<(int, int)> GrabItems(InventoryItem[] ingredients)
    {
        List<(int, int)> Ingredients = [];
        for (int i = 0; i < ingredients.Length; i++)
        {
            if (ingredients[i].Index >= 1909 && ingredients[i].Index <= 1946 && ingredients[i].Count > 0 && ingredients[i].Index != 1942 &&
               ingredients[i].Index != 1943 && ingredients[i].Index != 1944)
            {
                Ingredients.Add((ingredients[i].Index, ingredients[i].Count));
            }
        }
        return Ingredients;

    }
    public List<(int, int)> GrabCondiments(InventoryItem[] condiments)
    {
        List<(int, int)> Condiments = [];
        for (int i = 0; i < condiments.Length; i++)
        {
            if (condiments[i].Index < 1904 && condiments[i].Index > 0 && condiments[i].Index != 1888 && condiments[i].Count > 0)
                Condiments.Add((condiments[i].Index, condiments[i].Count));
        }
        for (int i = 0; i < condiments.Length; i++)
        {
            if (condiments[i].Index >= 1942 && condiments[i].Index <= 1944 && condiments[i].Count > 0)
                Condiments.Add((condiments[i].Index, condiments[i].Count));
        }
        for (int i = 0; i < condiments.Length; i++)
        {
            if (condiments[i].Index >= 1904 && condiments[i].Index <= 1908 && condiments[i].Count > 0)
                Condiments.Add((condiments[i].Index, condiments[i].Count));
        }
        return Condiments;
    }
    public bool SelectItem(List<(int, int)> Ingredients, ref List<(int, int, int, bool)> EatItem, int index, int count = 1)
    {
        for (int i = 0; i < Ingredients.Count; i++)
        {
            if (Ingredients[i].Item2 >= count && Ingredients[i].Item1 == index)
            {
                bool DUp = Ingredients.Count - i < i;
                EatItem.Add((index, count, DUp ? Ingredients.Count - i : i, DUp));
            }
        }
        if (EatItem.Count > 0)
            return true;
        return false;
    }
    public bool SelectCondiments(List<(int, int)> Condiments, ref List<(int, int, int, bool)> EatItem, int index, int count = 1)
    {
        for (int i = 0; i < Condiments.Count; i++)
        {
            if (Condiments[i].Item2 >= count && Condiments[i].Item1 == index)
            {
                bool DUp = Condiments.Count - i < i;
                EatItem.Add((index, count, DUp ? Condiments.Count - i : i, DUp));
            }
        }
        if (EatItem.Count > 0)
            return true;
        return false;
    }

    public static bool IsMaterial(InventoryItem item)
    {
        return ((item.Index >= 1956 && item.Index <= 2099) || (item.Index >= 2103 && item.Index <= 2123) || (item.Index >= 2126 && item.Index <= 2137) || (item.Index >= 2156 && item.Index <= 2159) || (item.Index >= 2438 && item.Index <= 2521) || item.Index == 10000);
    }
    public async Task<List<InventoryPouch>> ReadGiftItem(ulong ItemOffset, CancellationToken token)
    {
        SAV9SV TrainerSav = await GetFakeTrainerSAVSV(token).ConfigureAwait(false);
        while (ItemOffset == 0)
        {
            await Task.Delay(0_050, token).ConfigureAwait(false);
            ItemOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        }
        var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(ItemOffset, TrainerSav.Items.Data.Length, token).ConfigureAwait(false);
        data.CopyTo(TrainerSav.Items.Data);
        return TrainerSav.Inventory.ToList();
    }
    public async Task<List<InventoryPouch>> ReadGiftItemSWSH(CancellationToken token)
    {
        SAV8SWSH TrainerSav = await GetFakeTrainerSAVSWSH(token).ConfigureAwait(false);
        var data = await Executor.SwitchConnection.ReadBytesAsync(Offsets.ItemsOffset, TrainerSav.Items.Data.Length, token).ConfigureAwait(false);
        data.CopyTo(TrainerSav.Items.Data);
        return TrainerSav.Inventory.ToList();
    }
    public async Task<bool> HasShinyCharm(CancellationToken token)
    {
        var pouch = await GetBag(InventoryType.KeyItems, token).ConfigureAwait(false);
        if (pouch is null)
            return false;
        var bag = GetItems(pouch);
        for (int i = 0; i < bag.Length; i++)
        {
            if (bag[i].Index == 632)
                return true;
        }
        return false;
    }
    public async Task<List<InventoryItem>> GetDiffItems(List<InventoryPouch> ItemData, CancellationToken token)
    {
        if (!Executor.SwitchConnection.Connected)            
            Executor.SwitchConnection.Reset();

        ItemBlockOffset = await Executor.SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        List<InventoryPouch> ItemDataNew = await ReadGiftItem(ItemBlockOffset, token).ConfigureAwait(false);
        List<InventoryItem> DiffItems = [];
        int attempts = 0;
        while (ItemDataNew.SequenceEqual(ItemData))
        {
            LogUtil.LogText("Item sequence is same! Reloading...");
            await Task.Delay(0_050).ConfigureAwait(false);
            ItemDataNew = await ReadGiftItem(ItemBlockOffset, token).ConfigureAwait(false);
            attempts++;
            if (attempts >= 60)
                break;
        }
        if (!ItemDataNew.SequenceEqual(ItemData))
        {
            LogUtil.LogText("Gift Item Found!");
            for (int i = 0; i < Math.Min(ItemDataNew.Count, ItemData.Count); i++)
            {
                var diffItems = GrabAllDiffItems(ItemDataNew[i].Items, ItemData[i].Items);
                var success = diffItems.FirstOrDefault() != null && diffItems.Count > 0;
                LogUtil.LogText($"Found {diffItems.Count} diff items in {(InventoryType)i} pouch.");
                if (!success)
                    continue;
                DiffItems = DiffItems.Concat(diffItems).ToList();
            }
        }
        return DiffItems;
    }
    public async Task<List<InventoryItem>> GetDiffItemsSWSH(List<InventoryPouch> ItemData, CancellationToken token)
    {
        if (!Executor.SwitchConnection.Connected)
            Executor.SwitchConnection.Reset();

        List<InventoryPouch> ItemDataNew = await ReadGiftItemSWSH(token).ConfigureAwait(false);
        List<InventoryItem> DiffItems = [];
        int attempts = 0;
        while (ItemDataNew.SequenceEqual(ItemData))
        {
            LogUtil.LogText("Item sequence is same! Reloading...");
            await Task.Delay(0_050).ConfigureAwait(false);
            ItemDataNew = await ReadGiftItemSWSH(token).ConfigureAwait(false);
            attempts++;
            if (attempts >= 60)
                break;
        }
        if (!ItemDataNew.SequenceEqual(ItemData))
        {
            LogUtil.LogText("Gift Item Found!");
            for (int i = 0; i < Math.Min(ItemDataNew.Count, ItemData.Count); i++)
            {
                var diffItems = GrabAllDiffItems(ItemDataNew[i].Items, ItemData[i].Items);
                var success = diffItems.FirstOrDefault() != null && diffItems.Count > 0;
                LogUtil.LogText($"Found {diffItems.Count} diff items in {(InventoryType)i} pouch.");
                if (!success)
                    continue;
                DiffItems = DiffItems.Concat(diffItems).ToList();
            }
        }
        return DiffItems;
    }
}
