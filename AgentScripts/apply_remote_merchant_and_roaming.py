from pathlib import Path
import shutil
r=Path('Assets/HabitatIsland/Scripts');b=Path('ChangeBackups/20261018_IndependentHarbor')
def rep(s,a,c):
 assert a in s,a[:80]
 return s.replace(a,c)
def edit(n,fn):
 p=r/n
 if not (b/n).exists():shutil.copy2(p,b/n)
 p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8-sig')
edit('HabitatView.cs',lambda s:rep(rep(rep(s,'int oldWidth, oldDepth,','int oldDockLevel = -999;\n        int oldWidth, oldDepth,'),'if (oldDockX != s.dockX','if (oldDockLevel != s.levelIndex || oldDockX != s.dockX'),'oldDockX = s.dockX;','oldDockLevel=s.levelIndex; oldDockX = s.dockX;'))
edit('HabitatMapHarbor.cs',lambda s:rep(s,'return dock.TransformPoint(new Vector3(s.harborX,0f,s.harborZ)) + Vector3.up * (model.position.y-dock.position.y+FootOffset);','{ TryGround(dock.TransformPoint(new Vector3(s.harborX,0f,s.harborZ)),out Vector3 recovered);return recovered; }'))
edit('HabitatGame.Chapter.cs',lambda s:rep(s,'if (!State.onHarbor || !View.Harbor.NearShop(State))\n            { State.message', 'if(RemoteMerchantAvailable){Hud.OpenRemoteShop();State.message="已接通商人。工具放入背包，木箱送入船舱；交付和出售请返航。";return;}\n            if (!State.onHarbor || !View.Harbor.NearShop(State))\n            { State.message'))
edit('HabitatGame.Purchase.cs',lambda s:rep(rep(s,'  public HabitatState PreviewPurchase','  public bool RemoteMerchantAvailable => State!=null && State.authoredLevel && HabitatLevels.IsFormal(State.levelIndex);\n  public bool CanPurchaseHere => RemoteMerchantAvailable || View.Harbor.NearShop(State);\n  public HabitatState PreviewPurchase'),'!View.Harbor.NearShop(State)','!CanPurchaseHere || item=="sell"&&!View.Harbor.NearShop(State)'))
edit('HabitatGame.cs',lambda s:rep(s,'if (!View.Harbor.NearShop(State)) { State.message','if (!CanPurchaseHere || item=="sell"&&!View.Harbor.NearShop(State)) { State.message'))
edit('HabitatHud.cs',lambda s:rep(rep(s,'        public void ToggleShop()','        public void OpenRemoteShop(){shopSelling=false;ToggleShop();}\n        public void ToggleShop()'),'if(!game.View.Harbor.NearShop(game.State))','if(!game.CanPurchaseHere)'))
edit('HabitatHud.Harbor.cs',lambda s:rep(rep(s,'bool nearShop=game.View!=null&&game.View.Harbor.NearShop(s);','bool nearShop=game.View!=null&&(shopSelling?game.View.Harbor.NearShop(s):game.CanPurchaseHere);'),'string feedback=!nearShop?"请走到码头商店旁交易。":','string feedback=game.RemoteMerchantAvailable?(shopSelling?"通话只提供补给购买；出售渔获请返航。":"商人通话中：工具入背包，木箱入船舱，到岛屿渡口卸货。"):!nearShop?"请走到码头商店旁交易。":'))
def model(s):
 # Explicit purchase whitelist: calls never open remote quest delivery or sell paths.
 anchor='        public static bool Shop(HabitatState s,string item)'
 s=rep(s,anchor,'''        public static bool RemotePurchase(HabitatState s,string item)
            => s!=null&&s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex)
                &&(item=="crate"||item=="float"||item=="fan"||item=="shovel"||item=="magnet"||item=="launcher"||item=="trap"||item=="rod");
'''+anchor)
 a='''            if(!AtDock(s)){Log(s,"请回到码头旁进行交易");return false;}
            item=(item??"").ToLowerInvariant();'''
 s=rep(s,a,'''            item=(item??"").ToLowerInvariant();
            if(!AtDock(s)&&!RemotePurchase(s,item)){Log(s,"请回到码头旁进行交易");return false;}''')
 # Purchased bellows used to occupy the hand; buying from harbor would block departure.
 s=rep(s,'            if(!EmptyHand(s)){Log(s,"手中只能携带一件，请先放下当前物品");return false;}\n            PropKind p;int cost;', '''            if(item=="fan"&&HabitatChapterRules.IsCampaignShop(s))
            {
                int price=ShopPrice(s,item);if(s.coins<price){Log(s,"贝币不足");return false;}
                s.coins-=price;if(s.bagProps==null)s.bagProps=new List<PropKind>();s.bagProps.Add(PropKind.Fan);
                Log(s,"购买吹风机，已放入背包。");return true;
            }
            if(!EmptyHand(s)){Log(s,"手中只能携带一件，请先放下当前物品");return false;}
            PropKind p;int cost;''')
 s=rep(s,'public bool catchClaimed;','public bool catchClaimed;\n        public bool roamAnchored;public int roamX,roamZ;')
 s=rep(s,'            if(a.carried)return;Tile q=TileAt(s,a.x,a.z);','''            if(UsesConfirmedPlan(s))
            {
                if(!s.won||a.carried)return;
                MoveSettledAnimal(s,a,t);return;
            }
            if(a.carried)return;Tile q=TileAt(s,a.x,a.z);''')
 anchor='        static void ScareAfterShrimpStep('
 idx=s.index(anchor)
 s=s[:idx]+'''        static void MoveSettledAnimal(HabitatState s,Animal a,Tuning t)
        {
            if(TileAt(s,a.x,a.z).trap&&a.species==Species.Shrimp)return;
            if(!a.roamAnchored){a.roamAnchored=true;a.roamX=a.x;a.roamZ=a.z;}
            // Never wander away from the solved neighborhood or spoil another resident's habitat.
            int ox=a.x,oz=a.z,start=Random(s,4);
            for(int i=0;i<4;i++)
            {
                int d=(start+i)%4,nx=ox+DX[d],nz=oz+DZ[d];
                if(Math.Abs(nx-a.roamX)+Math.Abs(nz-a.roamZ)>1||!AnimalCanStep(s,a,nx,nz))continue;
                if(!s.onHarbor&&nx==s.playerX&&nz==s.playerZ)continue;
                a.x=nx;a.z=nz;bool suitable=true;
                foreach(var other in s.animals)if(!Evaluate(s,other,t).suitable){suitable=false;break;}
                if(suitable)return;
                a.x=ox;a.z=oz;
            }
        }
''' + s[idx:]
 s=rep(s,'catchClaimed=a.catchClaimed});','catchClaimed=a.catchClaimed,roamAnchored=a.roamAnchored,roamX=a.roamX,roamZ=a.roamZ});')
 # External deliberate animal motion establishes a fresh local idle origin.
 s=rep(s,'other.moveClock=0;other.progress=0;','other.moveClock=0;other.progress=0;other.roamAnchored=false;')
 s=rep(s,'a.x+=DX[best];a.z+=DZ[best];a.moveClock=0;','a.x+=DX[best];a.z+=DZ[best];a.moveClock=0;a.roamAnchored=false;')
 return s
edit('HabitatModel.cs',model)
print('Remote buying and bounded post-win movement applied')
