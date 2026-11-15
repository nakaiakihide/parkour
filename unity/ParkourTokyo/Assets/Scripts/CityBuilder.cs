using System.Collections.Generic;
using UnityEngine;

// 屋根情報（スポーン/CP 配置用）
public class RoofInfo
{
    public float[] points;
    public float top;
    public Vector2 center;
    public float area;
}

// 掴める縁（レッジ）。Web 版の「屋根の縁つかみ」に対応し、移行メモの
// 「レッジ線分の事前計算」をそのまま実装したもの。center は所属する屋根の中心（外向き判定用）。
public class LedgeSegment
{
    public Vector2 a, b, center;
    public float top;
}

// CourseData から街を組み立てる。建物はフットプリントを高さまで押し出した柱、
// 道路は地面のリボン。LOD2 サーフェスや看板・電車は Phase 2（データは読み込み済み）。
public class CityBuilder : MonoBehaviour
{
    public List<RoofInfo> Roofs { get; } = new List<RoofInfo>();
    public List<LedgeSegment> Ledges { get; } = new List<LedgeSegment>();

    public void Build(CourseData data)
    {
        Roofs.Clear();
        Ledges.Clear();
        BuildGround();
        BuildRoads(data);
        BuildBuildings(data);
    }

    void BuildGround()
    {
        var go = new GameObject("Ground");
        go.transform.SetParent(transform);
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(1000f, 1f, 1000f);
        col.center = new Vector3(0f, -0.5f, 0f);
        var mesh = new Mesh();
        float h = 500f;
        mesh.vertices = new[]
        {
            new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h),
            new Vector3(-h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h),
        };
        mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
        mesh.RecalculateNormals();
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = FlatMat(new Color(0.47f, 0.51f, 0.44f));
    }

    void BuildRoads(CourseData data)
    {
        var colors = new[]
        {
            new Color(0.20f, 0.22f, 0.24f), // 車道
            new Color(0.56f, 0.56f, 0.53f), // 歩行者系
            new Color(0.81f, 0.83f, 0.85f), // 横断歩道
        };
        for (int cls = 0; cls < 3; cls++)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var road in data.roads)
            {
                if (road.cls != cls) continue;
                int n = PolyUtil.Count(road.points);
                float half = road.width * 0.5f;
                float y = 0.02f + cls * 0.006f;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 a = PolyUtil.Pt(road.points, i);
                    Vector2 b = PolyUtil.Pt(road.points, i + 1);
                    Vector2 dir = (b - a);
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    dir.Normalize();
                    Vector2 perp = new Vector2(-dir.y, dir.x) * half;
                    int s = verts.Count;
                    verts.Add(new Vector3(a.x - perp.x, y, a.y - perp.y));
                    verts.Add(new Vector3(a.x + perp.x, y, a.y + perp.y));
                    verts.Add(new Vector3(b.x + perp.x, y, b.y + perp.y));
                    verts.Add(new Vector3(b.x - perp.x, y, b.y - perp.y));
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                    tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
                    tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                    tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
                }
            }
            if (verts.Count == 0) continue;
            var go = new GameObject("Roads" + cls);
            go.transform.SetParent(transform);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FlatMat(colors[cls]);
        }
    }

    void BuildBuildings(CourseData data)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var b in data.buildings)
        {
            int n = PolyUtil.Count(b.points);
            if (n < 3 || b.height <= 0f) continue;
            float h = b.height;
            // 壁（両面で出す: 巻き方向に依存せず衝突・描画が成立するように）
            for (int i = 0; i < n; i++)
            {
                Vector2 a = PolyUtil.Pt(b.points, i);
                Vector2 c = PolyUtil.Pt(b.points, (i + 1) % n);
                int s = verts.Count;
                verts.Add(new Vector3(a.x, 0, a.y));
                verts.Add(new Vector3(c.x, 0, c.y));
                verts.Add(new Vector3(c.x, h, c.y));
                verts.Add(new Vector3(a.x, h, a.y));
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }
            // 屋根（耳切り法で三角形分割、両面）
            var roof = PolyUtil.Triangulate(b.points);
            int rs = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = PolyUtil.Pt(b.points, i);
                verts.Add(new Vector3(p.x, h, p.y));
            }
            foreach (int t in roof)
            {
                tris.Add(rs + t);
            }
            for (int i = roof.Count - 1; i >= 0; i--) tris.Add(rs + roof[i]);
            // 屋根情報とレッジ
            var center = PolyUtil.Centroid(b.points);
            Roofs.Add(new RoofInfo
            {
                points = b.points,
                top = h,
                center = center,
                area = Mathf.Abs(PolyUtil.SignedArea(b.points)),
            });
            for (int i = 0; i < n; i++)
            {
                Ledges.Add(new LedgeSegment
                {
                    a = PolyUtil.Pt(b.points, i),
                    b = PolyUtil.Pt(b.points, (i + 1) % n),
                    center = center,
                    top = h,
                });
            }
        }
        var go = new GameObject("Buildings");
        go.transform.SetParent(transform);
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = FlatMat(new Color(0.72f, 0.76f, 0.80f));
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    static Material FlatMat(Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.roughness = 1f;
        m.metalness = 0f;
        return m;
    }
}
