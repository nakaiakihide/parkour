using System.Collections.Generic;
using UnityEngine;

// 2D（XZ 平面）ポリゴンの基本演算。Web 版の pointInPoly / distToPoly と同じ意味。
// points は [x, z, x, z, ...] のフラット配列。
public static class PolyUtil
{
    public static int Count(float[] pts) => pts.Length / 2;

    public static Vector2 Pt(float[] pts, int i) => new Vector2(pts[i * 2], pts[i * 2 + 1]);

    public static float SignedArea(float[] pts)
    {
        double a = 0;
        int n = Count(pts);
        for (int i = 0; i < n; i++)
        {
            Vector2 p = Pt(pts, i), q = Pt(pts, (i + 1) % n);
            a += (double)p.x * q.y - (double)q.x * p.y;
        }
        return (float)(a * 0.5);
    }

    public static Vector2 Centroid(float[] pts)
    {
        Vector2 c = Vector2.zero;
        int n = Count(pts);
        for (int i = 0; i < n; i++) c += Pt(pts, i);
        return n > 0 ? c / n : c;
    }

    public static bool PointInPoly(float x, float z, float[] pts)
    {
        bool inside = false;
        int n = Count(pts);
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = Pt(pts, i), b = Pt(pts, j);
            if ((a.y > z) != (b.y > z) &&
                x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    // 点と線分の距離。closest に最近接点を返す。
    public static float SegDist(Vector2 p, Vector2 a, Vector2 b, out Vector2 closest)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }

    // 点とポリゴンの距離（内部なら 0）。closest に最近接の境界点を返す。
    public static float DistToPoly(float x, float z, float[] pts, out Vector2 closest)
    {
        Vector2 p = new Vector2(x, z);
        closest = p;
        if (PointInPoly(x, z, pts)) return 0f;
        float best = float.MaxValue;
        int n = Count(pts);
        for (int i = 0; i < n; i++)
        {
            float d = SegDist(p, Pt(pts, i), Pt(pts, (i + 1) % n), out Vector2 c);
            if (d < best) { best = d; closest = c; }
        }
        return best;
    }

    // 2つのポリゴンの間の最短距離（CP 配置の canGo 判定用）。
    public static float PolyGap(float[] a, float[] b)
    {
        float best = float.MaxValue;
        int na = Count(a), nb = Count(b);
        for (int i = 0; i < na; i++)
        {
            float d = DistToPoly(a[i * 2], a[i * 2 + 1], b, out _);
            if (d < best) best = d;
        }
        for (int j = 0; j < nb; j++)
        {
            float d = DistToPoly(b[j * 2], b[j * 2 + 1], a, out _);
            if (d < best) best = d;
        }
        return best;
    }

    // 耳切り法で単純ポリゴンを三角形分割する（XZ 平面、インデックスのみ返す）。
    public static List<int> Triangulate(float[] pts)
    {
        var result = new List<int>();
        int n = Count(pts);
        if (n < 3) return result;
        var idx = new List<int>();
        for (int i = 0; i < n; i++) idx.Add(i);
        // XZ を (x, z) の 2D として扱い、符号付き面積の向きに合わせて処理する
        bool ccw = SignedArea(pts) > 0;
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int i0 = idx[(i + idx.Count - 1) % idx.Count];
                int i1 = idx[i];
                int i2 = idx[(i + 1) % idx.Count];
                Vector2 a = Pt(pts, i0), b = Pt(pts, i1), c = Pt(pts, i2);
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (ccw ? cross <= 1e-9f : cross >= -1e-9f) continue; // 凸でない
                bool ear = true;
                for (int j = 0; j < idx.Count; j++)
                {
                    int k = idx[j];
                    if (k == i0 || k == i1 || k == i2) continue;
                    if (PointInTri(Pt(pts, k), a, b, c)) { ear = false; break; }
                }
                if (!ear) continue;
                result.Add(i0); result.Add(i1); result.Add(i2);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // 縮退ポリゴン: 諦めて残りはファンで埋める
        }
        if (idx.Count >= 3)
            for (int i = 1; i + 1 < idx.Count; i++)
            {
                result.Add(idx[0]); result.Add(idx[i]); result.Add(idx[i + 1]);
            }
        return result;
    }

    static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < -1e-9f || d2 < -1e-9f || d3 < -1e-9f;
        bool pos = d1 > 1e-9f || d2 > 1e-9f || d3 > 1e-9f;
        return !(neg && pos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
