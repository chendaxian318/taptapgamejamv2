using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatView
    {
        void BuildHomeIsland(Transform model)
        {
            Transform root=NewRoot("Home island",model);root.gameObject.AddComponent<HabitatHomeIsland>();
            // Match the original game tiles exactly in world size, atlas and shape.
            // The harbor prefab has a .7 scale, hence the local-space conversion.
            float cell=HabitatHomeIsland.CellSize,layer=Step/.7f;
            for(int z=-11;z<=0;z++)for(int x=-11;x<=8;x++){
                if(!HabitatHomeIsland.HasCell(x,z))continue;
                Transform tile=NewRoot("Home ground "+x+","+z,root);
                tile.localPosition=new Vector3(x*cell,0,z*cell);
                bool beach=HabitatHomeIsland.EdgeCell(x,z);
                for(int k=0;k<2;k++){
                    string key=k==1&&!beach?"sandGrass":"sand";
                    AddImportedModel(key,tile,new Vector3(0,HabitatHomeIsland.Top-(2-k)*layer,0),new Vector3(.985f/.7f,layer,.985f/.7f),((x+z+k)&3)*90);
                }
            }
            for(int i=0;i<19;i++){
                float z=Mathf.Lerp(-.4f,-4.1f,i/18f);
                Box("Island access plank",root,new Vector3(HabitatHomeIsland.Gate.x,HabitatHomeIsland.RampHeight(z)-.055f,z),new Vector3(1.9f,.11f,.205f),materials[i%3==0?"wood":"woodLight"]);
            }
        }
    }
}
