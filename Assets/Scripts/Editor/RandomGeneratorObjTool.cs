using UnityEngine;
using UnityEditor;
using log4net.Util;
using UnityEngine.UIElements;

public class RandomGeneratorObjTool
{
    // 在 Unity 菜单中添加入口
    [MenuItem("Tools/生成工具/随机创建1000个球")]
    public static void RandomGenerateSphere()
    {
        GameObject SphereParent = GameObject.Find("SphereParent");
        if (SphereParent == null)
            SphereParent = new GameObject("SphereParent");

        int sphereCount = 2000;
        Vector2 xRange = new Vector2(-250f, 250f);
        Vector2 yRange = new Vector2(0f, 30f);
        Vector2 zRange = new Vector2(-250f, 250f);
        Vector2 scaleRange = new Vector2(0.5f, 3f);
        for (int i = 0; i < sphereCount; i++)
        {
            // 随机生成位置
            Vector3 pos = new Vector3(
                Random.Range(xRange.x, xRange.y),
                Random.Range(yRange.x, yRange.y),
                Random.Range(zRange.x, zRange.y)
            );

            // 随机生成均匀缩放
            float scale = Random.Range(scaleRange.x, scaleRange.y);

            // 创建球体
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = pos;
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            sphere.transform.parent = SphereParent.transform;

        }
        Debug.Log($"生成了 {sphereCount} 个球体");
    }

    [MenuItem("Tools/生成工具/删除所有球")]
    public static void DeleteAllSphere()
    {
        GameObject SphereParent = GameObject.Find("Instance/SphereParent");
        GameObject.DestroyImmediate(SphereParent);

    }

}
