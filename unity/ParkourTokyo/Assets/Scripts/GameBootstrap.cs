using System.IO;
using UnityEngine;

// 起動役: コース JSON を読み、街・プレイヤー・カメラ・コース進行を組み立てる。
// 空のシーンに空の GameObject を置いてこのコンポーネントを付ければ動く（README 参照）。
// キー 1/2/3 でコース切替（渋谷/池袋/新宿）。
public class GameBootstrap : MonoBehaviour
{
    public string startCourse = "shibuya";

    GameObject cityGO;
    CityBuilder city;
    GameObject playerGO;
    PlayerController player;
    CourseManager courseManager;
    CourseData currentData;

    void Start()
    {
        SetupEnvironment();
        SetupPlayerAndCamera();
        courseManager = gameObject.AddComponent<CourseManager>();
        gameObject.AddComponent<TouchControls>();
        LoadCourse(startCourse);
    }

    void SetupEnvironment()
    {
        RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.66f);
        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        sun.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        if (Camera.main != null) Camera.main.backgroundColor = new Color(0.62f, 0.76f, 0.90f);
    }

    void SetupPlayerAndCamera()
    {
        playerGO = new GameObject("Player");
        playerGO.AddComponent<CharacterController>();
        player = playerGO.AddComponent<PlayerController>();
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(playerGO.transform);
        body.transform.localPosition = new Vector3(0, 0.95f, 0);
        var bc = body.GetComponent<Collider>();
        if (bc != null) Destroy(bc);
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.93f, 0.42f, 0.18f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var camGO = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
        if (Camera.main == null) camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        if (cam == null) cam = camGO.AddComponent<Camera>();
        cam.backgroundColor = new Color(0.62f, 0.76f, 0.90f);
        var follow = camGO.GetComponent<FollowCamera>();
        if (follow == null) follow = camGO.AddComponent<FollowCamera>();
        follow.target = playerGO.transform;
        player.cam = camGO.transform;
    }

    public void LoadCourse(string key)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "city", key + ".json");
        if (!File.Exists(path))
        {
            Debug.LogError("コースデータが見つかりません: " + path);
            return;
        }
        // NOTE: Android 実機では StreamingAssets が jar 内のため File 直接読みは不可。
        // UnityWebRequest での読み込みに差し替えること（Phase 2）。
        currentData = JsonUtility.FromJson<CourseData>(File.ReadAllText(path));
        if (cityGO != null) Destroy(cityGO);
        cityGO = new GameObject("City");
        city = cityGO.AddComponent<CityBuilder>();
        city.Build(currentData);
        player.city = city;
        courseManager.Init(city, player, currentData);
        courseManager.StartRun();
        Debug.Log("コース読み込み: " + currentData.name + " 建物 " + currentData.buildings.Length + " 棟");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) LoadCourse("shibuya");
        if (Input.GetKeyDown(KeyCode.Alpha2)) LoadCourse("ikebukuro");
        if (Input.GetKeyDown(KeyCode.Alpha3)) LoadCourse("shinjuku");
    }
}
