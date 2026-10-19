using UnityEngine;
using System.Collections.Generic;

namespace HabitatIsland
{
    // This hub uses the same terrain blocks as the puzzle islands, while retaining
    // continuous harbor movement and existing harborX/Z save coordinates.
    public sealed class HabitatHomeIsland : MonoBehaviour
    {
        public const float CellSize=1f/.7f;
        public const float Top=-.12f;
        public static readonly Vector3 Gate=new Vector3(7.2f,0f,-.65f);
        public static readonly Vector3 Clearing=new Vector3(5.8f,Top,-6.5f);
        struct WallSegment {public Vector2 a,b;public WallSegment(Vector3 a,Vector3 b){this.a=new Vector2(a.x,a.z);this.b=new Vector2(b.x,b.z);}}
        readonly List<WallSegment>[] walls={new List<WallSegment>(),new List<WallSegment>()};
        // Slice the actual imported walls/furniture at leg and body height once
        // per harbor build. This replaces the oversized rectangular exclusion.
        public void BindObstacles(MeshCollider[] colliders)
        {
            foreach(var slice in walls)slice.Clear();
            float[] heights={.35f,1f};
            foreach(var collider in colliders){
                if(!collider||!collider.sharedMesh)continue;
                var matrix=transform.worldToLocalMatrix*collider.transform.localToWorldMatrix;
                var vertices=collider.sharedMesh.vertices;var triangles=collider.sharedMesh.triangles;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                for(int level=0;level<heights.Length;level++)for(int i=0;i<triangles.Length;i+=3){
                    Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                    float height=heights[level];int n=0;Vector3 start=default,end=default;
                    void Edge(Vector3 from,Vector3 to){
                        if((from.y>height)==(to.y>height))return;
                        Vector3 p=Vector3.Lerp(from,to,(height-from.y)/(to.y-from.y));
                        if(n++==0)start=p;else end=p;
                    }
                    Edge(a,b);Edge(b,c);Edge(c,a);
                    if(n==2&&(start-end).sqrMagnitude>.0000001f)walls[level].Add(new WallSegment(start,end));
                }
            }
        }
        public bool Obstructed(Vector2 point,float radius)
        {
            foreach(var slice in walls){
                bool inside=false;
                foreach(var wall in slice){
                    Vector2 d=wall.b-wall.a;float t=Mathf.Clamp01(Vector2.Dot(point-wall.a,d)/d.sqrMagnitude);
                    if((point-wall.a-d*t).sqrMagnitude<radius*radius)return true;
                    if((wall.a.y>point.y)!=(wall.b.y>point.y)&&point.x<(wall.b.x-wall.a.x)*(point.y-wall.a.y)/(wall.b.y-wall.a.y)+wall.a.x)inside=!inside;
                }
                if(inside)return true;
            }
            return false;
        }
        public static bool HasCell(int x,int z)
        {
            Vector2 p=new Vector2(x*CellSize,z*CellSize);
            float dx=(p.x+2f)/13f,dz=(p.y+7.6f)/7.6f;
            return z<=0&&dx*dx+dz*dz<=1f;
        }
        public static bool EdgeCell(int x,int z)=>!HasCell(x-1,z)||!HasCell(x+1,z)||!HasCell(x,z-1)||!HasCell(x,z+1);
        static bool HasGround(Vector2 p)=>HasCell(Mathf.RoundToInt(p.x/CellSize),Mathf.RoundToInt(p.y/CellSize));
        public static bool OnRamp(Vector3 p,float radius)=>Mathf.Abs(p.x-Gate.x)<=.95f-radius&&p.z>=-4.1f&&p.z<=-.35f;
        public static float RampHeight(float z)=>Mathf.Lerp(.025f,Top,Mathf.InverseLerp(-.65f,-3.9f,z));
        public bool TryGround(Vector3 world,float playerRadius,out Vector3 position)
        {
            position=world;Vector3 p=transform.InverseTransformPoint(world);
            float radius=playerRadius/Mathf.Min(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
            if(OnRamp(p,radius)){p.y=RampHeight(p.z);position=transform.TransformPoint(p);return true;}
            Vector2 point=new Vector2(p.x,p.z);
            if(!HasGround(point)||Obstructed(point,radius))return false;
            for(int i=0;i<12;i++){
                float a=i*Mathf.PI*2/12;
                if(!HasGround(point+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius+.035f)))return false;
            }
            p.y=Top;position=transform.TransformPoint(p);return true;
        }
    }
}
