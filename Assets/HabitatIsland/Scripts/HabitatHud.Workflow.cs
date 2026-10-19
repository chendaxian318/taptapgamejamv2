using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HabitatIsland
{
    public partial class HabitatHud
    {
        GameObject startPanel,pausePanel,inventoryPanel,confirmPanel,utilityPanel;
        TMP_Text workflowFeedback,inventorySummary,inventoryFeedback,confirmText,utilityText;
        Button continueSession,resumeSession,utilityConfirm,cargoUnload,cargoLoad;
        Button[] inventoryTools=new Button[8],storedItems=new Button[6];
        Button[] utilityDirections=new Button[4];
        int bagPage,previewX,previewZ,directionX,directionZ;
        Action confirmAction;GameObject confirmationOrigin;
        GameObject previewModel;
        LineRenderer previewLine;
        Material previewMaterial;
        public bool SessionMenuOpen=>ModalOpen&&startPanel&&startPanel.activeSelf&&!(confirmPanel&&confirmPanel.activeSelf);
        public bool InventoryOpen=>ModalOpen&&inventoryPanel&&inventoryPanel.activeSelf;
        public bool PauseMenuOpen=>ModalOpen&&pausePanel&&pausePanel.activeSelf;
        public bool UtilityPreviewOpen=>ModalOpen&&utilityPanel&&utilityPanel.activeSelf;
        void BuildWorkflowPanels()
        {
            BuildDeliveredStartMenu();
            pausePanel=ModalCard("Pause menu",new Vector2(640,570),"游戏暂停","地图与生态已暂停，关闭菜单后恢复原来的观察状态。").gameObject;
            Button(pausePanel.transform,"继续游戏",new Vector2(48,-134),new Vector2(544,46),ClosePanelsFromInput,primary:true);
            Button(pausePanel.transform,"保存进度",new Vector2(48,-192),new Vector2(260,44),()=>{game.SaveSession();});
            Button(pausePanel.transform,"读取进度",new Vector2(332,-192),new Vector2(260,44),()=>{if(!game.LoadSession())game.State.message="读取失败，当前进度未替换。";});
            Button(pausePanel.transform,"背包 I",new Vector2(48,-250),new Vector2(260,44),ToggleInventory);
            Button(pausePanel.transform,"声音设置",new Vector2(332,-250),new Vector2(260,44),()=>Open(audioPanel));
            Button(pausePanel.transform,"保存并回主菜单",new Vector2(48,-308),new Vector2(544,44),()=>{if(game.SaveSession())ShowStartMenu();});
            Button(pausePanel.transform,"退出游戏",new Vector2(48,-366),new Vector2(544,44),RequestQuit);
            pauseFeedback=Text(pausePanel.transform,"",15,muted,new Vector2(48,-438),new Vector2(544,100));
            confirmPanel=ModalCard("Confirm action",new Vector2(660,350),"确认操作","未确认前不会丢弃进度或退出。").gameObject;
            confirmText=Text(confirmPanel.transform,"",19,ink,new Vector2(28,-130),new Vector2(604,110));
            Button(confirmPanel.transform,"取消",new Vector2(28,-272),new Vector2(280,48),CancelConfirmation);
            Button(confirmPanel.transform,"确认",new Vector2(332,-272),new Vector2(300,48),()=>{var action=confirmAction;confirmAction=null;action?.Invoke();},primary:true);
            inventoryPanel=ModalCard("Inventory",new Vector2(840,750),"背包与船舱","选择工具；箱子由船运输，只有在船旁才能装卸。").gameObject;
            inventorySummary=Text(inventoryPanel.transform,"",17,ink,new Vector2(28,-124),new Vector2(784,56));
            for(int i=0;i<8;i++)
            {
                int id=i;
                inventoryTools[i]=Button(inventoryPanel.transform,HabitatGame.ToolNames[i],new Vector2(28+(i%4)*198,-192-(i/4)*60),new Vector2(186,48),()=>game.SelectInventoryTool(id));
                inventoryTools[i].name="Inventory tool "+i;
            }
            Button(inventoryPanel.transform,"收起手持道具",new Vector2(28,-326),new Vector2(246,42),()=>game.StoreInventoryItem());
            cargoLoad=Button(inventoryPanel.transform,"装入船舱",new Vector2(292,-326),new Vector2(246,42),()=>game.TransferCargo(false));
            cargoUnload=Button(inventoryPanel.transform,"从船舱取一箱",new Vector2(556,-326),new Vector2(246,42),()=>game.TransferCargo(true));
            Text(inventoryPanel.transform,"已收纳道具 · 点击取出",16,muted,new Vector2(28,-390),new Vector2(550,30));
            for(int i=0;i<6;i++)
            {int index=i;storedItems[i]=Button(inventoryPanel.transform,"",new Vector2(28+(i%3)*264,-430-(i/3)*54),new Vector2(250,44),()=>game.TakeInventoryItem(bagPage*6+index));}
            Button(inventoryPanel.transform,"上一页",new Vector2(28,-548),new Vector2(122,34),()=>bagPage=Mathf.Max(0,bagPage-1));
            Button(inventoryPanel.transform,"下一页",new Vector2(164,-548),new Vector2(122,34),()=>{if((bagPage+1)*6<game.State.bagProps.Count)bagPage++;});
            inventoryFeedback=Text(inventoryPanel.transform,"",16,ink,new Vector2(28,-605),new Vector2(784,56));
            Button(inventoryPanel.transform,"返回游戏",new Vector2(590,-680),new Vector2(212,42),ClosePanelsFromInput,primary:true);
            utilityPanel=ModalCard("Utility preview",new Vector2(470,445),"道具预览","确认后执行；取消不消耗库存。").gameObject;
            var ur=utilityPanel.GetComponent<RectTransform>();ur.anchorMin=ur.anchorMax=ur.pivot=new Vector2(1,0);ur.anchoredPosition=new Vector2(-24,24);
            utilityText=Text(ur,"",17,ink,new Vector2(28,-128),new Vector2(414,115));
            for(int i=0;i<4;i++)
            {
                int id=i;utilityDirections[i]=Button(ur,new[]{"北 +Z","东 +X","南 −Z","西 −X"}[i],new Vector2(28+i*104,-263),new Vector2(96,40),()=>{
                    directionX=id==1?1:id==3?-1:0;directionZ=id==0?1:id==2?-1:0;UpdateUtilityPreview();
                });
            }
            Button(ur,"取消",new Vector2(28,-365),new Vector2(192,48),ClosePanelsFromInput);
            utilityConfirm=Button(ur,"确认使用",new Vector2(240,-365),new Vector2(202,48),()=>game.ConfirmUtility(previewX,previewZ,directionX,directionZ),primary:true);
            foreach(var panel in new[]{startPanel,pausePanel,inventoryPanel,confirmPanel,utilityPanel})panel.SetActive(false);
            var bagButton=Button(gameplayInterface.transform,"背包 I",Vector2.zero,new Vector2(110,42),ToggleInventory);
            bagButton.name="Open inventory";var bagRect=bagButton.GetComponent<RectTransform>();
            bagRect.anchorMin=bagRect.anchorMax=bagRect.pivot=Vector2.zero;bagRect.anchoredPosition=new Vector2(24,24);
        }
        TMP_Text pauseFeedback;
        void SetWorkflowPanel(GameObject panel)
        {
            // A title-screen confirmation stays over the cover until the player accepts.
            bool overStart=panel==confirmPanel&&confirmationOrigin==startPanel;
            foreach(var p in new[]{startPanel,pausePanel,inventoryPanel,confirmPanel,utilityPanel})if(p)p.SetActive(p==panel||(p==startPanel&&overStart));
            var startInput=startPanel.GetComponent<CanvasGroup>();
            startInput.interactable=startInput.blocksRaycasts=!overStart;
            if(overStart)confirmPanel.transform.SetAsLastSibling();
            if(panel!=utilityPanel)ClearUtilityPreview();
        }
        void RequestNewSession()
        {
            if(game.Busy||game.StoryPlaying)return;
            if(game.HasSavedSession)AskConfirmation("开始新旅程？当前未保存进度将丢弃；已有磁盘存档会保留，直到你主动保存新进度。",()=>game.NewSession());
            else game.NewSession();
        }
        public void ShowStartMenu(){Open(startPanel);workflowFeedback.text=game.HasSavedSession?"继续会读取最近一次保存的进度。":"还没有存档，选择新游戏开始。";RefreshWorkflow();}
        public void ToggleInventory(){selectedBagTool=selectedBagIndex=-1;Open(inventoryPanel);RefreshWorkflow();}
        public void HandleEscape()
        {if(SessionMenuOpen&&menuEntrance&&menuEntrance.IsPlaying){menuEntrance.Skip();return;}if(SessionMenuOpen&&!game.SessionStarted)return;if(ModalOpen)ClosePanelsFromInput();else Open(pausePanel);}
        void AskConfirmation(string message,Action action){confirmationOrigin=ActiveModalPage();Open(confirmPanel);confirmText.text=message;confirmAction=action;}
        void CancelConfirmation(){confirmAction=null;var origin=confirmationOrigin;confirmationOrigin=null;if(origin)Open(origin);else ClosePanels();}
        void RequestQuit()
        {
            AskConfirmation("退出游戏？未保存的进度不会保留。你可以取消，回到暂停菜单先保存。",()=>{
                #if UNITY_EDITOR
                game.State.message="编辑器中已确认退出；独立游戏会关闭窗口。";ClosePanels();
                #else
                Application.Quit();
                #endif
            });
        }
        void RefreshWorkflow()
        {
            if(!startPanel)return;
            if(SessionMenuOpen){continueSession.interactable=game.HasSavedSession;resumeSession.interactable=game.SessionStarted;}
            if(PauseMenuOpen)pauseFeedback.text=game.State.message;
            if(!InventoryOpen)return;
            var s=game.State;
            inventorySummary.text=$"贝币 {s.coins} · 普通鱼 {s.fish} · 虾 {s.shrimp} · 琉璃鱼 {s.glassFish} · 蟹壳 {s.shells} · 水母 {s.jellyfish}\n船舱木箱 {s.boatCrates}/3 · 土块 {s.cargo} · 铲子 {s.shovels} · 磁铁 {s.magnets} · 发射器 {s.launchers}";
            for(int i=0;i<inventoryTools.Length;i++)if(inventoryTools[i])inventoryTools[i].interactable=game.HasTool(i);
            int count=s.bagProps?.Count??0;bagPage=Mathf.Clamp(bagPage,0,Mathf.Max(0,(count-1)/storedItems.Length));
            for(int i=0;i<storedItems.Length;i++)
            {int n=bagPage*storedItems.Length+i;storedItems[i].gameObject.SetActive(n<count);if(n<count)storedItems[i].GetComponentInChildren<TMP_Text>().text=PropName(s.bagProps[n]);}
            cargoUnload.interactable=game.NearCargoBoat&&s.boatCrates>0&&s.carryingAnimal<0&&s.carryingProp==PropKind.None;
            cargoLoad.interactable=game.NearCargoBoat&&s.boatCrates<3&&s.carryingAnimal<0;
            inventoryFeedback.text=s.message;
        }
        public void OpenUtilityPreview()
        {
            if(game.Busy||game.State.onHarbor||game.tool<5)return;
            game.GetTarget(out previewX,out previewZ);directionX=game.State.facingX;directionZ=game.State.facingZ;
            if(game.tool==5||game.tool==7){previewX=game.State.playerX+directionX;previewZ=game.State.playerZ+directionZ;}
            Open(utilityPanel);UpdateUtilityPreview();
        }
        void UpdateUtilityPreview()
        {
            ClearUtilityPreview();var s=game.State;
            var candidate=game.PreviewUtility(previewX,previewZ,directionX,directionZ,out bool valid);
            utilityText.text=HabitatGame.ToolNames[game.tool]+"\n"+candidate.message+"\n"+(game.tool==6?"方向按地图坐标选择；绿色线表示落点。":"发射/挖土沿人物面朝方向。选错请取消后重新面向。");
            utilityConfirm.interactable=valid;
            foreach(var button in utilityDirections)button.interactable=game.tool==6;
            string resource=game.tool==7?"Models/Cannon/Cannon":null;
            if(resource!=null)
            {
                var prefab=Resources.Load<GameObject>(resource);
                if(prefab){previewModel=Instantiate(prefab);previewModel.name="Utility model preview";previewModel.transform.position=new Vector3(s.playerX,HabitatRules.Height(s,s.playerX,s.playerZ)*.7f+.1f,s.playerZ)+new Vector3(s.facingX,0,s.facingZ)*.45f;
                    previewModel.transform.localScale=Vector3.one*.65f;previewModel.transform.rotation=Quaternion.LookRotation(new Vector3(s.facingX,0,s.facingZ));
                    if(game.tool==6){previewModel.transform.position+=Vector3.up*.7f;previewModel.transform.rotation*=Quaternion.Euler(-90,0,0);}
                    var animation=previewModel.GetComponent<HabitatEquipmentAnimation>();if(animation&&game.tool==7)animation.Sample("Aim_Up",animation.Clips.Count>0?1:0);
                }
            }
            if(!valid)return;
            int endX=previewX,endZ=previewZ;
            if(game.tool!=5)for(int i=0;i<s.tiles.Length;i++)
                if((candidate.tiles[i].prop==PropKind.Crate||candidate.tiles[i].prop==PropKind.Float)&&s.tiles[i].prop!=PropKind.Crate&&s.tiles[i].prop!=PropKind.Float){endX=i%s.width;endZ=i/s.width;break;}
            var line=new GameObject("Utility landing preview");previewLine=line.AddComponent<LineRenderer>();
            previewMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));previewMaterial.color=new Color(.25f,1f,.55f);
            previewLine.sharedMaterial=previewMaterial;previewLine.startWidth=previewLine.endWidth=.055f;previewLine.positionCount=3;
            Vector3 start=new Vector3(previewX,Mathf.Max(HabitatRules.WaterSurfaceY(s.waterLevel),HabitatRules.Height(s,previewX,previewZ)*.7f)+.2f,previewZ);
            Vector3 end=new Vector3(endX,Mathf.Max(HabitatRules.WaterSurfaceY(candidate.waterLevel),HabitatRules.Height(candidate,endX,endZ)*.7f)+.2f,endZ);
            previewLine.SetPositions(new[]{start,(start+end)*.5f+Vector3.up*(game.tool==7?.65f:.1f),end});
        }
        void ClearUtilityPreview()
        {if(previewModel)Destroy(previewModel);if(previewLine)Destroy(previewLine.gameObject);if(previewMaterial)Destroy(previewMaterial);previewModel=null;previewLine=null;previewMaterial=null;}
    }
}
