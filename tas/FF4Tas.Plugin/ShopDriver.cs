using FF4Tas.Core;
using Last.Management;
using Last.UI.KeyInput;
using UnityEngine;

namespace FF4Tas;

// "menu" steps with a shop order: talk to the shopkeeper the party faces,
// sell every unequipped copy of the listed old gear, then buy the list in
// order (each only while affordable and owned+equipped is under its count).
// Keys only, like the field menu: reads Last.UI.KeyInput.ShopController's
// state machine, command menu, product list and cursors.
internal sealed class ShopDriver
{
    // ShopController.State and Last.Defaine.ShopCommandId.
    private const int SelectCommand=1,SelectProduct=2,SelectSellItem=3,ConfirmBuy=6,ConfirmSell=7;
    private const int BuyCommand=1,SellCommand=2,MaxActions=40,AfterTrade=30;
    private ShopOrder order=new();
    private string facing="Up";
    private bool finished,opened,oneColumn;
    private int actions,wait,stuck,lastIndex=-1;
    private string? lastKey;
    // Sell entries the sell list no longer shows (sold out or equipped).
    private readonly HashSet<int> soldOut=new();
    private ShopController? shop;
    internal void Reset(ShopOrder next,string? face)
    {
        order=next;facing=face??"Up";finished=opened=oneColumn=false;actions=wait=stuck=0;lastIndex=-1;lastKey=null;soldOut.Clear();shop=null;
    }
    internal void Read(Snapshot s,int frame)
    {
        if(finished && s.Phase=="Player") {s.MenuDone=true;s.Detail="Shop: done";return;}
        if(wait>0) {wait--;s.Detail="Shop: trading";return;}
        if(s.Phase=="Player")
        {
            // Turn to the counter, then talk; the shop opens as phase Shop.
            if(opened) {finished=true;s.MenuDone=true;return;}
            Pulse(s,frame,frame%24<12?facing:"Confirm","Talk to the shopkeeper");return;
        }
        // The shop screen itself may hold the popup filter: only confirm
        // stray popups outside it.
        if(s.Phase=="Message" || (s.Modal && s.Phase!="Shop")) {Pulse(s,frame,"Confirm","Shop message");return;}
        if(s.Phase!="Shop")return;
        opened=true;
        if(shop==null || !shop)shop=UnityEngine.Object.FindObjectOfType<ShopController>();
        if(shop?.stateMachine==null)return;
        int sell=NextSell(),buy=NextBuy(out int price);
        if(actions>=MaxActions)sell=buy=0;
        int state=(int)shop.stateMachine.Current;
        s.Detail=$"Shop: state {state}, gil {Gil()}, sell {sell}, buy {buy} ({price})";
        switch(state)
        {
            case SelectCommand:
            {
                if(sell==0 && buy==0) {finished=true;Pulse(s,frame,"Back","Leave the shop");return;}
                var menu=shop.infoController?.commandMenuController;
                if(menu?.contentList==null || menu.selectCursor==null)return;
                int want=-1,command=sell!=0?SellCommand:BuyCommand;
                for(int i=0;i<menu.contentList.Count;i++)if((int)(menu.contentList[i]?.CommandId??0)==command) {want=i;break;}
                if(want<0) {finished=true;Pulse(s,frame,"Back","No Buy/Sell command");return;}
                Move(s,frame,menu.selectCursor.Index,want,true,command==SellCommand?"Sell":"Buy");return;
            }
            case SelectProduct:
            {
                if(buy==0) {Pulse(s,frame,"Back","Nothing more to buy ("+string.Join(", ",System.Linq.Enumerable.Select(order.Buy,b=>$"{b.Content}: {Owned(b.Content)} bag + {Equipped(b.Content)} worn / {b.Count}"))+$"; {Gil()} gil)");return;}
                var list=shop.listController?.listController;
                if(list?.productContentList==null || list.selectCursor==null)return;
                int want=-1;
                for(int i=0;i<list.productContentList.Count;i++)if(list.productContentList[i]?.ContentId==buy) {want=i;break;}
                if(want<0) {Pulse(s,frame,"Back","Not sold here");return;}
                Move(s,frame,list.selectCursor.Index,want,false,"Buy "+buy);return;
            }
            case SelectSellItem:
            {
                if(sell==0) {Pulse(s,frame,"Back","Nothing more to sell");return;}
                // The sell list is its own list window; take the open one that
                // shows owned items.
                ShopListMainContentController? list=null;
                foreach(var c in UnityEngine.Object.FindObjectsOfType<ShopListMainContentController>())
                    if(c!=null && c.gameObject.activeInHierarchy && c.productContentList!=null && c.selectCursor!=null) {list=c;if(Has(c,sell))break;}
                if(list==null)return;
                int want=-1;
                for(int i=0;i<list.productContentList.Count;i++)if(list.productContentList[i]?.ContentId==sell) {want=i;break;}
                if(want<0) {soldOut.Add(sell);return;}
                Move(s,frame,list.selectCursor.Index,want,true,"Sell "+sell);return;
            }
            case ConfirmBuy:
            case ConfirmSell:
                // One copy per trade (the popup opens at 1); the loop repeats.
                if(frame%12==0) {actions++;wait=AfterTrade;}
                Pulse(s,frame,"Confirm",state==ConfirmBuy?"Confirm purchase":"Confirm sale");return;
            default:Pulse(s,frame,"Back","Back to the shop commands");return;
        }
    }
    private static bool Has(ShopListMainContentController list,int content)
    {
        for(int i=0;i<list.productContentList.Count;i++)if(list.productContentList[i]?.ContentId==content)return true;
        return false;
    }
    private int NextSell()
    {
        foreach(var c in order.Sell)if(!soldOut.Contains(c) && Owned(c)>0)return c;
        return 0;
    }
    private int NextBuy(out int price)
    {
        price=0;
        var products=shop?.productDataList;
        foreach(var b in order.Buy)
        {
            if(Owned(b.Content)+Equipped(b.Content)>=b.Count)continue;
            int cost=-1;
            for(int i=0;products!=null && i<products.Count;i++)if(products[i]?.ContentId==b.Content) {cost=products[i].Buy;break;}
            if(cost<0 || cost>Gil())continue;
            price=cost;return b.Content;
        }
        return 0;
    }
    private static int Gil()=>UserDataManager.Instance()?.OwendGil??0;
    private static int Owned(int content)
    {
        var owned=UserDataManager.Instance()?.normalOwnedItems;
        return owned!=null && owned.TryGetValue(content,out var entry) && entry!=null?entry.Count:0;
    }
    // Copies worn by party members (OwnedCharacterData.EquipmentData).
    private static int Equipped(int content)
    {
        var user=UserDataManager.Instance();var corps=user?.corpsList;var owned=user?.OwnedCharacterList;
        if(corps==null || owned==null)return 0;
        var bag=user?.normalOwnedItems;
        int n=0;
        for(int c=0;c<corps.Count;c++)
        {
            int id=corps[c]?.CharacterId??0;if(id==0)continue;
            for(int o=0;o<owned.Count;o++)
            {
                if(owned[o]?.Id!=id)continue;
                // A worn item can be the bag's own entry for it (the bag count
                // already includes it) - counting it again skipped the second
                // Thunder Claw live. Count only worn entries that aren't the
                // bag's, once each.
                var gear=owned[o].EquipmentData;
                var seen=new HashSet<IntPtr>();
                if(gear!=null)foreach(var slot in gear)
                {
                    var item=slot.Value;if(item==null || item.ContentId!=content || !seen.Add(item.Pointer))continue;
                    if(bag!=null && bag.TryGetValue(content,out var mine) && mine!=null && mine.Pointer==item.Pointer)continue;
                    n++;
                }
                break;
            }
        }
        return n;
    }
    // Up/Down first; where the layout may be sideways or a grid (command
    // menu, sell list) a key that leaves the cursor in place three times
    // switches to Left/Right.
    private void Move(Snapshot s,int frame,int at,int want,bool canFlip,string what)
    {
        bool sideways=canFlip && oneColumn;
        string key=at==want?"Confirm":sideways?(want<at?"Left":"Right"):(want<at?"Up":"Down");
        if(frame%12==0 && key!="Confirm")
        {
            if(key==lastKey && at==lastIndex && ++stuck>=3) {oneColumn=!oneColumn;stuck=0;}
            else if(at!=lastIndex)stuck=0;
            lastKey=key;lastIndex=at;
        }
        Pulse(s,frame,key,what);
    }
    private static void Pulse(Snapshot s,int frame,string key,string detail)
    {
        s.Detail="Shop: "+detail;
        if(frame%12==0)s.MenuInput=key;
    }
}
