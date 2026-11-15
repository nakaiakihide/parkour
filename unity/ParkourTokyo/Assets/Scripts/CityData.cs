using System;

// Unity 用に組み替えたコースデータ（rebuild/export_unity.py が Web 版 CITY_DATA から生成）。
// 座標は実数メートル。x = 東(+), z = 南(+), 高さは m。JsonUtility で読めるよう
// ジャグ配列を避け、フラットな float[] にしてある。
[Serializable]
public class CourseData
{
    public string key;
    public string name;
    public BuildingData[] buildings;
    public RoadData[] roads;
    public float[] railPath;      // 本線パスの [x, z, x, z, ...]（描画は Phase 2）
    public string stationName;
    public float stationX;
    public float stationZ;
    public NameData[] names;      // 実在 POI 名（看板用、Phase 2）
}

[Serializable]
public class BuildingData
{
    public float[] points;        // フットプリント [x, z, x, z, ...]
    public float height;          // LOD1 高さ（m）
    public SurfaceData[] surfaces; // LOD2 サーフェス（Phase 2 で使用）
}

[Serializable]
public class SurfaceData
{
    public int type;              // 0 = 壁 / 1 = 屋根系（抽出時の分類）
    public float[] ring;          // [x, y, z, x, y, z, ...]
}

[Serializable]
public class RoadData
{
    public float[] points;        // ポリライン [x, z, x, z, ...]
    public float width;
    public int cls;               // 0 = 車道 / 1 = 歩行者系 / 2 = 横断歩道
}

[Serializable]
public class NameData
{
    public string name;
    public float x;
    public float z;
}

[Serializable]
public class CourseManifest
{
    public CourseEntry[] courses;
}

[Serializable]
public class CourseEntry
{
    public string key;
    public string name;
}
