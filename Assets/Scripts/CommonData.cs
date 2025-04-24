using UnityEngine;

public static class CommonData
{
    //摄像机路点数量
    public static int CameraMovePointCount = 10;
    
    //循环缓冲大小
    public static int HZBInfoCount = 4;
    
    //静态小球数量
    public static int StaticSphereCount = 20000;
    
    //动态立方体的数量
    public static int DynamicCubeCount = 1000;
    
    public static Vector2 xRange = new Vector2(-800f, 800);
    public static Vector2 yRange = new Vector2(0f, 80f);
    public static Vector2 zRange = new Vector2(-800f, 800f);
}
