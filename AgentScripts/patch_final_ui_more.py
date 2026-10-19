exec((__import__('pathlib').Path(__file__).parent/'apply_final_ui.py').read_text(encoding='utf-8').split("edit('HabitatHud.DeliveredUI.cs'")[0].split('def delivered')[0])
def harbor(s):
 return s.replace('shopPrices[i].enableAutoSizing=true;','RefreshFinalShopCard(i);\n                shopPrices[i].enableAutoSizing=true;')
edit('HabitatHud.Harbor.cs',harbor)
def batch(s):
 return s.replace('   purchaseCount.text=purchaseQuantity.ToString();','   if(finalQuantityArt)finalQuantityArt.texture=HabitatFinalUi.Texture(purchaseSelling?"R105":"R104");\n   purchaseCount.text=purchaseQuantity.ToString();')
edit('HabitatHud.Batch2.cs',batch)
def maps(s):
 s=s.replace('        RawImage preview;','        RawImage preview,finalTitle;RawImage[] finalCards;\n        static int CardArt(int index){switch(index){case 5:return 40;case 6:return 37;case 7:return 38;case 8:return 43;case 9:return 41;case 10:return 39;default:return 0;}}\n        static int TitleArt(int index){switch(index){case 5:return 36;case 6:return 47;case 7:return 48;case 8:return 49;case 9:return 50;case 10:return 51;default:return 0;}}')
 s=s.replace('Resources.Load<Texture2D>("UI/Delivered/MapBeach")','HabitatFinalUi.Texture("Background03")')
 s=s.replace('new Color(1,1,1,.64f)','new Color(1,1,1,0)')
 a=s.index('            Fill(header,new Color32');b=s.index('            destination=Label',a)
 s=s[:a]+'            HabitatFinalUi.Art(header,"R33",Vector2.zero,new Vector2(1440,244));\n            finalTitle=HabitatFinalUi.Art(header,"R36",new Vector2(38,-35),new Vector2(680,126),true);\n'+s[b:]
 s=s.replace('rows=new Image[destinations.Length];','finalCards=new RawImage[destinations.Length];rows=new Image[destinations.Length];')
 a=s.index('                int col=index==6');b=s.index('                var locked=',a)
 s=s[:a]+'''                int artIndex=CardArt(index);
                if(artIndex>0){finalCards[i]=HabitatFinalUi.Art(button.transform,"R"+artIndex,Vector2.zero,new Vector2(400,172),true);rowLabels[i].enabled=false;}
                else {rowLabels[i].enabled=true;rowLabels[i].color=mint;}
                completionBadges[i]=Label(button.transform,"CLEAR",19,new Color32(237,107,112,255),new Vector2(100,25),new Vector2(220,30),true).gameObject;
                completionBadges[i].SetActive(false);
'''+s[b:]
 # Delivered art 39 says FREE LEVEL, but this is the authored fifth main island: cover only that caption with real classification.
 old='                Label(button.transform,index==10?'
 a=s.index(old);b=s.index('\n',a)
 s=s[:a]+'''                if(index==10||index==4){var caption=NewRect("Actual destination category",button.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-125),new Vector2(400,44));Fill(caption,new Color32(236,247,244,255),false);Label(caption,index==10?"正式关卡 05":"自由探索 · 荒岛",17,ink,Vector2.zero,new Vector2(400,44));}
'''+s[b:]
 a=s.index('            confirmButton.GetComponent<Image>().color=');b=s.index('            var previous=',a)
 s=s[:a]+'            HabitatFinalUi.ButtonArt(confirmButton,"R34");\n'+s[b:]
 s=s.replace('rowLabels[i].color = level==4?Color.white:allowed?mint:muted;','rowLabels[i].color = allowed?mint:muted;\n                if(finalCards[i]){bool clear=completed!=null&&completed(level);finalCards[i].texture=HabitatFinalUi.Texture((clear?"Clear":"R")+CardArt(level));if(completionBadges[i])completionBadges[i].SetActive(false);}')
 s=s.replace('            destination.text=DestinationName(index);','            destination.text=DestinationName(index);int titleId=TitleArt(index);destination.enabled=titleId==0;finalTitle.enabled=titleId>0;if(titleId>0)finalTitle.texture=HabitatFinalUi.Texture("R"+titleId);')
 return s
edit('HabitatMapSelectionPanel.cs',maps)
def fishing(s):
 s=s.replace('            Label(card,"● MEEE",27,Color.white,new Vector2(162,-44),new Vector2(150,52),true);\n            Label(card,"FISH ◀",27,Color.white,new Vector2(1135,-44),new Vector2(150,52),true);','            var deliveredBar=HabitatFinalUi.Art(card,"R106",new Vector2(135,-45),new Vector2(1170,59));deliveredBar.transform.SetAsFirstSibling();')
 s=s.replace('            Label(card,"上钩 •••",30,Color.white,new Vector2(260,-418),new Vector2(220,60),true);','            HabitatFinalUi.Art(card,"R108",new Vector2(260,-418),new Vector2(220,66));')
 return s
edit('HabitatFishing.cs',fishing)
