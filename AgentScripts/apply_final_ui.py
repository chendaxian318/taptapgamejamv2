from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1];src=root/'Assets/HabitatIsland/Scripts';backup=root/'ChangeBackups/ui-final-20261018';backup.mkdir(parents=True,exist_ok=True)
def edit(name,fn):
 p=src/name
 if not (backup/name).exists():shutil.copy2(p,backup/name)
 old=p.read_text(encoding='utf-8-sig');new=fn(old);assert new!=old,name;p.write_text(new,encoding='utf-8')
def delivered(s):
 a=s.index('            var art=HabitatDeliveredArt.Add(b.transform');b=s.index('            var colors=',a)
 s=s[:a]+'            HabitatFinalUi.ButtonArt(b,"R"+(5+index),"Hover"+(13+index));\n'+s[b:]
 pairs=[('HabitatDeliveredArt.Add(layout,"Delivered coral ribbon"','HabitatFinalUi.Art(layout,"R12",new Vector2(40,-736),new Vector2(1020,35));'),('var logo=HabitatDeliveredArt.Add','var logo=HabitatFinalUi.Art(layout,"R10",new Vector2(1030,-614),new Vector2(396,179));'),('HabitatDeliveredArt.Add(ribbon,"Left opening arrows"','HabitatFinalUi.Art(ribbon,"R1",new Vector2(0,-360),new Vector2(370,34));'),('HabitatDeliveredArt.Add(ribbon,"Right opening arrows"','HabitatFinalUi.Art(ribbon,"R3",new Vector2(1110,-360),new Vector2(370,34));'),('HabitatDeliveredArt.Add(banner,"Task ribbon left"','banner.GetComponent<Image>().color=Color.clear;HabitatFinalUi.Art(banner,"R19",new Vector2(-80,0),new Vector2(810,52)).transform.SetAsFirstSibling();'),('HabitatDeliveredArt.Add(banner,"Task ribbon right"',''),('var menuArt=HabitatDeliveredArt.Add','var menuArt=HabitatFinalUi.Art(menu.transform,"R18",Vector2.zero,new Vector2(160,58));menu.targetGraphic=menuArt;')]
 for prefix,replacement in pairs:
  lines=s.splitlines();s='\n'.join(('            '+replacement) if line.strip().startswith(prefix) else line for line in lines)+'\n'
 return s
edit('HabitatHud.DeliveredUI.cs',delivered)
def layout(s):
 needle='   Shape(p,"Rounded bubble body"'
 pos=s.index(needle);s=s[:pos]+'   string finalKey=key=="PauseHeading"?"R84":key=="SettingsHeading"?"R118":key=="SummaryHeading"?"R112":null;\n   if(finalKey!=null&&HabitatFinalUi.Texture(finalKey)){HabitatFinalUi.Art(p,finalKey,Vector2.zero,new Vector2(600,height),true);return p;}\n'+s[pos:]
 pos=s.index('   var tex=Resources.Load<Texture2D>',s.index('void Ribbon'))
 s=s[:pos]+'   string finalKey=key=="BagEquipment"?"R130":key=="BagCreatures"?"R131":key=="BagItems"?"R132":key=="ShopBuy"?"R85":key=="ShopSold"?"R86":key=="GuideVideo"?"R120":null;\n   if(finalKey!=null){HabitatFinalUi.Art(parent,finalKey,new Vector2(x,y+27),new Vector2(width,key=="GuideVideo"?58:width*HabitatFinalUi.Texture(finalKey).height/HabitatFinalUi.Texture(finalKey).width));return;}\n'+s[pos:]
 return s
edit('HabitatHud.DesignLayout.cs',layout)
def batch(s):
 s=s.replace('BuildPurchase();BuildPlanInterface();','BuildPurchase();BuildPlanInterface();ApplyFinalUiDelivery();')
 s=s.replace('for(int i=0;i<labels.Length;i++)TextButton(p,labels[i],174,-146-i*86,252,actions[i]).name="Pause "+labels[i];','for(int i=0;i<labels.Length;i++){var b=TextButton(p,labels[i],174,-146-i*86,252,actions[i]);b.name="Pause "+labels[i];HabitatFinalUi.ButtonArt(b,"R"+(80+i),"Hover"+(80+i));}')
 a=s.index('   var portrait=');b=s.index('   Ribbon(p,"BagEquipment"',a)
 s=s[:a]+'   HabitatFinalUi.Art(p,"Background12",Vector2.zero,new Vector2(1440,830));\n'+s[b:]
 for line in ['   Text(p,"[装备]",16,Color.white,new Vector2(360,-54),new Vector2(140,26));','   Text(p,"[海洋生物]",16,Color.white,new Vector2(205,-308),new Vector2(170,26));','   Text(p,"[道具]",16,Color.white,new Vector2(205,-602),new Vector2(120,26));'] :s=s.replace(line,'')
 s=s.replace('   Shape(p,"Shop backdrop",0,0,1440,810,new Color32(153,165,160,245),0);\n   HabitatUiAssets.Icon(p,"Shopkeeper",new Vector2(-100,-24),new Vector2(850,850));','   HabitatFinalUi.Art(p,"Background07",Vector2.zero,new Vector2(1440,810));')
 s=s.replace('Text(p,"[买入]",17,deepSea,new Vector2(91,-30),new Vector2(120,30));','')
 s=s.replace('   Text(p,"今日\\n特供",62,Color.white,new Vector2(497,-84),new Vector2(130,410),TextAlignmentOptions.Center);','')
 s=s.replace('   Shape(p,"Shop dialogue",43,-615,515,158,blue,25);\n   Text(p,"老板  • • •",27,Color.white,new Vector2(54,-555),new Vector2(350,55),emphasis:true);','   HabitatFinalUi.Art(p,"R87",new Vector2(43,-555),new Vector2(515,220));')
 s=s.replace('new Vector2(65,-635),new Vector2(470,116)','new Vector2(65,-626),new Vector2(470,139)')
 s=s.replace('   Shape(p,"Book backdrop",0,0,1440,810,new Color32(180,188,181,247),0);','   HabitatFinalUi.Art(p,"Background05",Vector2.zero,new Vector2(1440,810));')
 s=s.replace('   Shape(p,"Guide rounded frame",50,-70,1050,600,blue,60);Heading(p,"GuideHeading","新手引导",415,-30,320);','   HabitatFinalUi.Art(p,"R122",new Vector2(15,-20),new Vector2(1120,655));')
 s=s.replace('Ribbon(p,"GuideVideo","VIDEO",805,-155,200,aqua);Accent(p,38,-210);Accent(p,1085,-490);','Ribbon(p,"GuideVideo","VIDEO",805,-155,200,aqua);')
 s=s.replace('Resources.Load<Texture2D>("UI/Delivered/Batch2/Icons/"+keys[game.tool])','HabitatUiAssets.IconTexture(keys[game.tool])')
 return s
edit('HabitatHud.Batch2.cs',batch)
def plan(s):
 # Same footprint and hit target as the already verified interaction prompt.
 start=s.find('HabitatDeliveredArt.Add(')
 lines=s.splitlines()
 for i,line in enumerate(lines):
  if 'HabitatDeliveredArt.Add' in line and 'InteractionExport' in line:
   import re
   m=re.search(r'HabitatDeliveredArt.Add\(([^,]+),',line)
   # Preserve declaration and later statements, replace only invocation.
   begin=line.index('HabitatDeliveredArt.Add(');end=line.index(';',begin)
   lines[i]=line[:begin]+'HabitatFinalUi.Art('+m[1]+',"R54",Vector2.zero,new Vector2(144,77))'+line[end:]
 return '\n'.join(lines)+'\n'
edit('HabitatHud.Plan.cs',plan)
