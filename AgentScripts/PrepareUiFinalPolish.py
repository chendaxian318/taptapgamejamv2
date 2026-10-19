from pathlib import Path
import sys,difflib
root=Path(__file__).resolve().parents[1]
changes={}
def prepare(rel):
 p=root/rel;s=p.read_text(encoding='utf-8-sig');return p,s
p,s=prepare('Assets/HabitatIsland/Scripts/HabitatHud.Batch2.cs');old=s
s=s.replace('bool shopSelling,purchaseSelling;Species purchaseSpecies;TMP_Text purchaseHeading;','bool shopSelling,purchaseSelling;Species purchaseSpecies;TMP_Text purchaseHeading,shopModeLabel,shopSwitchLabel;\n  RectTransform bookPageProgress;readonly Graphic[] bookPageDots=new Graphic[6];')
s=s.replace('Ribbon(p,"ShopBuy","BUY",88,-58,258,aqua);Text(p,"[买入]",17,deepSea,new Vector2(91,-30),new Vector2(120,30));','Ribbon(p,"ShopBuy","BUY",88,-58,258,aqua);shopModeLabel=Text(p,"[买入]",17,deepSea,new Vector2(91,-30),new Vector2(120,30));')
s=s.replace('TextButton(p,"切换为出售",215,-380,240,()=>SetShopSelling(true),19).name="Sell delivered catch";','var tradeToggle=TextButton(p,"切换为出售",215,-380,240,()=>SetShopSelling(!shopSelling),19);tradeToggle.name="Sell delivered catch";shopSwitchLabel=tradeToggle.GetComponentInChildren<TMP_Text>();')
s=s.replace('HabitatUiMotif.Add(handle,new Vector2(6,-7),new Vector2(22,23),red);','var marker=HabitatUiMotif.Add(handle,Vector2.zero,new Vector2(22,23),red);marker.anchorMin=marker.anchorMax=marker.pivot=new Vector2(.5f,.5f);marker.anchoredPosition=new Vector2(0,-1);')
a='for(int i=0;i<6;i++){int id=i;var b=TextButton(p,new[]{"水母","龙虾","金鱼","寄居蟹","陆龟","海星"}[i],145+i*139,-735,130,()=>{bookIndex=id;RefreshBook();},18);b.GetComponentInChildren<TMP_Text>().color=deepSea;}'
b='''Shape(p,"Book paging rail",280,-730,480,22,new Color32(220,238,208,255),11);
   bookPageProgress=Shape(p,"Book paging progress",280,-730,80,22,aqua,11);
   Shape(p,"Book pager left cap",267,-724,32,34,new Color32(239,227,192,255),9);Shape(p,"Book pager right cap",741,-724,32,34,new Color32(239,227,192,255),9);
   var prevBook=TextButton(p,"‹",310,-707,60,()=>{bookIndex=(bookIndex+5)%6;RefreshBook();},40);prevBook.name="Previous creature page";prevBook.GetComponentInChildren<TMP_Text>().color=blue;
   var nextBook=TextButton(p,"›",678,-707,60,()=>{bookIndex=(bookIndex+1)%6;RefreshBook();},40);nextBook.name="Next creature page";nextBook.GetComponentInChildren<TMP_Text>().color=blue;
   for(int i=0;i<6;i++){int id=i;var dot=TextButton(p,"",396+i*44,-720,32,()=>{bookIndex=id;RefreshBook();},1);dot.name="Creature page "+i;bookPageDots[i]=HabitatUiMotif.Add(dot.transform,new Vector2(10,-16),new Vector2(10,10),blue,true).GetComponent<HabitatUiMotif>();}
   Shape(p,"Book bottom hint",170,-776,900,24,new Color32(80,122,140,245),12);Text(p,"点击生物查看资料，或用左右箭头翻页",14,Color.white,new Vector2(190,-774),new Vector2(860,27),TextAlignmentOptions.Center);'''
assert a in s;s=s.replace(a,b)
s=s.replace('Species species=(Species)bookIndex;bookName.text=', 'bookIndex=Mathf.Clamp(bookIndex,0,5);bookPageProgress.sizeDelta=new Vector2(480*(bookIndex+1)/6f,22);\n   for(int i=0;i<bookPageDots.Length;i++)bookPageDots[i].color=i==bookIndex?deepSea:turquoise;\n   Species species=(Species)bookIndex;bookName.text=')
assert s!=old;changes[p]=(old,s)
p,s=prepare('Assets/HabitatIsland/Scripts/HabitatHud.Harbor.cs');old=s
s=s.replace('int price=HabitatRules.CatchSaleValue(s);','int price=HabitatRules.CatchSaleValue(s);\n            if(shopModeLabel)shopModeLabel.text=shopSelling?"[出售]":"[买入]";\n            if(shopSwitchLabel)shopSwitchLabel.text=shopSelling?"切换为买入":"切换为出售";')
assert s!=old;changes[p]=(old,s)
p,s=prepare('Assets/HabitatIsland/Scripts/HabitatMapSelectionPanel.cs');old=s
s=s.replace('confirmButton.GetComponent<Image>().color=new Color32(237,93,103,255);var goLabel=', 'confirmButton.GetComponent<Image>().color=Color.clear;\n            var goRibbon=NewRect("Departure capsule",confirmButton.transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(300,76));goRibbon.SetAsFirstSibling();var goSurface=goRibbon.gameObject.AddComponent<HabitatUiSurface>();goSurface.color=new Color32(237,93,103,255);goSurface.Radius=38;goSurface.raycastTarget=false;confirmButton.targetGraphic=goSurface;\n            var goLabel=')
s=s.replace('Label(confirmButton.transform,"Go",19,Color.white,new Vector2(48,10),new Vector2(50,25),true);','HabitatUiMotif.Add(confirmButton.transform,new Vector2(43,11),new Vector2(38,38),new Color32(172,74,53,255),true);Label(confirmButton.transform,"Go",19,Color.white,new Vector2(49,8),new Vector2(31,32),true);')
assert s!=old;changes[p]=(old,s)
p,s=prepare('AgentScripts/PlanUiChecks.cs');old=s
s=s.replace('B("Shop sell tab").onClick.Invoke();Check(', 'B("Shop sell tab").onClick.Invoke();Check(B("Sell delivered catch").GetComponentInChildren<TMP_Text>().text=="切换为买入","sale mode offers the opposite buy action");Check(',1)
s=s.replace('Set("bookIndex",0);Call("RefreshBook");await Capture("book-updated");','Set("bookIndex",0);Call("RefreshBook");B("Previous creature page").onClick.Invoke();Check(Field<int>("bookIndex")==5,"creature pager wraps backward to sixth species");B("Next creature page").onClick.Invoke();Check(Field<int>("bookIndex")==0,"creature pager wraps forward to first species");B("Creature page 3").onClick.Invoke();Check(Field<int>("bookIndex")==3,"creature page dots retain direct six-species access");Set("bookIndex",0);Call("RefreshBook");await Capture("book-updated");')
assert s!=old;changes[p]=(old,s)
patch=''.join(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile=str(p),tofile=str(p))) for p,(old,new) in changes.items())
(root/'AgentScripts/UI_final_polish.patch').write_text(patch,encoding='utf-8')
if '--apply' in sys.argv:
 for p,(_,new) in changes.items():p.write_text(new,encoding='utf-8')
 print('Applied four UI polish changes to '+str(len(changes))+' production files.')
else:print('Prepared UI_final_polish.patch; production files unchanged. Run with --apply after shared Unity tests are finished.')
