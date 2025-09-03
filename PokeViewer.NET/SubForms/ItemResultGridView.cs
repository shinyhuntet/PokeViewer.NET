using Newtonsoft.Json.Linq;
using PKHeX.Core;
using PokeViewer.NET.Misc;
using RaidCrawler.Core.Structures;
using SysBot.Base;

namespace PokeViewer.NET.SubForms;

public partial class ItemResultGridView : UserControl
{
    public ItemResultGridView() => InitializeComponent();
    
    public async Task Populate(List<InventoryItem> itemSpan, int language)
    {
        /*var rows = DGV_View.Rows;
        rows.Clear();
        rows = rows == null ? DGV_View.Rows : rows;*/
        await SpeedClear().ConfigureAwait(false);
        Image img;
        string url = string.Empty;
        foreach (var item in itemSpan)
        {
            if (Rewards.IsTM(item.Index))
            {
                img = Properties.Resources.tm;
            }
            else if (ItemStructure.IsMaterial(item))
            {
                img = Properties.Resources.material;
            }
            else
            {
                url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.PokeSprite/Resources/img/Artwork%20Items/aitem_{item.Index}.png";
                img = await GetItemImage(url).ConfigureAwait(false);
            }
            //rows.Add(item.Count, img, GameInfo.GetStrings(language).itemlist[item.Index]);
            DGV_View.Rows.Add(item.Count, img, GameInfo.GetStrings(language).itemlist[item.Index]);
        }
    }
    public async Task<Image> GetItemImage(string url)
    {
        using HttpClient client = new();
        var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
        return Image.FromStream(stream);
    }
    public async Task Clear()
    {
        while (DGV_View.Rows.Count > 0)
        {
            DGV_View.Rows.RemoveAt(0);
            await Task.Delay(0_010).ConfigureAwait(false);
        }
    }
    public async Task SpeedClear()
    {
        await DGV_View.InvokeAsync(() =>
        {
            DGV_View.Rows.Clear();
        }).ConfigureAwait(false);
    }
}
