using UnityEngine;

public static class CommonData
{
    //摄像机路点数量
    public static int CameraMovePointCount = 30;
    
    //循环缓冲大小
    public static int HZBInfoCount = 8;
    
    //静态小球数量
    public static int StaticSphereCount = 10000;
    
    //动态立方体的数量
    public static int DynamicCubeCount = 200;
    
    public static Vector2 xRange = new Vector2(-500f, 500);
    public static Vector2 yRange = new Vector2(0f, 50f);
    public static Vector2 zRange = new Vector2(-500f, 500f);
}
