using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class RandomGeneratorObjTool
{
    static int StaticSphereCount = 2000;
    
    static int DynamicCubeCount = 100;
    
    
    static Vector2 xRange = new Vector2(-500f, 500);
    static Vector2 yRange = new Vector2(0f, 40f);
    static Vector2 zRange = new Vector2(-500f, 500f);

    [MenuItem("Tools/生成工具/生成相机移动路点",false,1)]
    public static void GenerateCameraMovePoint()
    {
        GameObject CameraPointParent = GameObject.Find("CameraPointParent");
        if (CameraPointParent == null)
            CameraPointParent = new GameObject("CameraPointParent");
        for (int i = 0; i < CommonData.CameraMovePointCount; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(xRange.x, xRange.y),
                Random.Range(yRange.x, yRange.y),
                Random.Range(zRange.x, zRange.y)
            );
            GameObject point = new GameObject("CameraPoint");
            point.transform.position = pos;
            point.transform.parent = CameraPointParent.transform;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
    
    [MenuItem("Tools/生成工具/删除相机移动路点",false,2)]
    public static void DeleteCameraMovePoint()
    {
        GameObject CameraPointParent = GameObject.Find("CameraPointParent");
        if(CameraPointParent!=null)
            GameObject.DestroyImmediate(CameraPointParent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
    
    // 在 Unity 菜单中添加入口
    [MenuItem("Tools/生成工具/随机创建静态小球")]
    public static void RandomGenerateSphere()
    {
        GameObject SphereParent = GameObject.Find("SphereParent");
        if (SphereParent == null)
            SphereParent = new GameObject("SphereParent");
        
        Vector2 xRange = new Vector2(-500f, 500);
        Vector2 yRange = new Vector2(0f, 40f);
        Vector2 zRange = new Vector2(-500f, 500f);
        Vector2 scaleRange = new Vector2(0.5f, 3f);
        for (int i = 0; i < StaticSphereCount; i++)
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
            var mesh = sphere.AddComponent<OCMesh>();
            mesh.isStaticMesh = true;

        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"生成了 {StaticSphereCount} 个静态球体");
    }

    [MenuItem("Tools/生成工具/删除所有球")]
    public static void DeleteAllSphere()
    {
        GameObject SphereParent = GameObject.Find("SphereParent");
        if(SphereParent!=null)
            GameObject.DestroyImmediate(SphereParent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
    
    [MenuItem("Tools/生成工具/生成动态立方体")]
    public static void RandomGenerateCube()
    {
        GameObject CubeParent = GameObject.Find("CubeParent");
        if (CubeParent == null)
            CubeParent = new GameObject("CubeParent");
        
        Vector2 xRange = new Vector2(-500f, 500);
        Vector2 yRange = new Vector2(0f, 30f);
        Vector2 zRange = new Vector2(-500f, 500f);
        Vector2 scaleRange = new Vector2(0.5f, 3f);
        for (int i = 0; i < DynamicCubeCount; i++)
        {
            // 随机生成位置
            Vector3 pos = new Vector3(
                Random.Range(xRange.x, xRange.y),
                Random.Range(yRange.x, yRange.y),
                Random.Range(zRange.x, zRange.y)
            );
            Quaternion rot = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));

            // 随机生成均匀缩放
            float scale = Random.Range(scaleRange.x, scaleRange.y);

            // 创建球体
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sphere.transform.position = pos;
            sphere.transform.rotation = rot;
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            sphere.transform.parent = CubeParent.transform;
            var mesh = sphere.AddComponent<OCMesh>();
            mesh.isStaticMesh = false;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"生成了 {DynamicCubeCount} 个动态立方体");
    }
    
    [MenuItem("Tools/生成工具/删除所有立方体")]
    public static void DeleteAllCube()
    {
        GameObject CubeParent = GameObject.Find("CubeParent");
        GameObject.DestroyImmediate(CubeParent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

}
