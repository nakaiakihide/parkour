using System;
using System.Collections.Generic;
using UnityEngine;

// コース進行（スポーン選定・CP チェーン・タイマー・ゴール判定）。
// CP 配置は Web 版 placeCheckpoints のルールを移植:
// 到達できる屋根だけを鎖状に5つ選ぶ。canGo: rise<=0 なら gap<=6.5（rise<=-4.5 なら 8）、
// gap<=4.2 は無条件、rise<=1.8 なら gap<=6.5、rise<=5.8 なら gap<=1.8。
public class CourseManager: MonoBehaviour
{
public event Action<float> OnGoal;

CityBuilder city;
PlayerController player;
string courseKey;
string courseName;

class Cp
{
public RoofInfo roof;
public Vector3 pos;
public GameObject obj;
}

readonly List<Cp> cps = new List<Cp>();
Vector3 spawnPos;
int cpIndex;
float timer;
bool running, finished;
readonly List<float> splits = new List<float>();

public bool Finished => finished;
public float CurrentTime => timer;

public void Init(CityBuilder cityBuilder, PlayerController playerCtrl, CourseData data)
{
city = cityBuilder;
player = playerCtrl;
courseKey = data.key;
courseName = data.name;
BuildCourseLayout();
}

static bool CanGo(RoofInfo a, RoofInfo b)
{
float gap = PolyUtil.PolyGap(a.points, b.points);
float rise = b.top - a.top;
if (rise <= 0f) return gap <= (rise <= -4.5f? 8f: 6.5f);
if (gap <= 4.2f) return true;
if (rise <= 1.8f) return gap <= 6.5f;
if (rise <= 5.8f) return gap <= 1.8f;
return false;
}

// 屋根の内側にある安全な点（重心が外なら頂点寄りで探す）
static Vector2 SafePoint(RoofInfo roof)
{
Vector2 c = roof.center;
if (PolyUtil.PointInPoly(c.x, c.y, roof.points)) return c;
Vector2 v0 = PolyUtil.Pt(roof.points, 0);
for (float t = 0.25f; t < 1f; t += 0.25f)
{
Vector2 p = Vector2.Lerp(v0, c, t);
if (PolyUtil.PointInPoly(p.x, p.y, roof.points)) return p;
}
return v0;
}

void BuildCourseLayout()
{
foreach (var cp in cps) if (cp.obj!= null) Destroy(cp.obj);
cps.Clear();
splits.Clear();

var cand = new List<RoofInfo>();
foreach (var r in city.Roofs)
if (r.area >= 12f && r.top <= 45f) cand.Add(r);
// 計算量対策: 多すぎる場合は間引く
if (cand.Count > 220)
{
var sampled = new List<RoofInfo>();
float step = cand.Count / 220f;
for (float i = 0; i < cand.Count; i += step) sampled.Add(cand[(int)i]);
cand = sampled;
}
if (cand.Count == 0) { spawnPos = new Vector3(0, 1, 40); return;}

// スポーン: 到達できる屋根が最も多い候補
RoofInfo spawnRoof = cand[0];
int bestReach = -1;
foreach (var r in cand)
{
int reach = 0;
foreach (var o in cand)
{
if (o == r) continue;
if (Vector2.Distance(r.center, o.center) > 170f) continue;
if (CanGo(r, o)) reach++;
}
if (reach > bestReach) { bestReach = reach; spawnRoof = r;}
}
Vector2 sp = SafePoint(spawnRoof);
spawnPos = new Vector3(sp.x, spawnRoof.top, sp.y);

// CP チェーン: 到達可能な中から、既選出への最小距離が最大になるものを貪欲に選ぶ
var chosen = new List<RoofInfo> { spawnRoof};
RoofInfo current = spawnRoof;
for (int k = 0; k < 5; k++)
{
RoofInfo best = null;
float bestScore = -1f;
foreach (var r in cand)
{
if (chosen.Contains(r)) continue;
if (!CanGo(current, r)) continue;
float minD = float.MaxValue;
foreach (var ch in chosen) minD = Mathf.Min(minD, Vector2.Distance(r.center, ch.center));
if (minD < 18f) continue;
if (minD > bestScore) { bestScore = minD; best = r;}
}
if (best == null) break;
chosen.Add(best);
current = best;
Vector2 p = SafePoint(best);
var obj = MakeCpMarker(new Vector3(p.x, best.top, p.y), k == 4);
cps.Add(new Cp { roof = best, pos = new Vector3(p.x, best.top, p.y), obj = obj});
}
}

GameObject MakeCpMarker(Vector3 topPos, bool isGoal)
{
var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
go.name = isGoal? "Goal": "Checkpoint";
go.transform.SetParent(transform);
go.transform.position = topPos + new Vector3(0, 3.5f, 0);
go.transform.localScale = new Vector3(2.4f, 3.5f, 2.4f);
var col = go.GetComponent<Collider>();
if (col!= null) Destroy(col);
var mat = new Material(Shader.Find("Standard"));
Color c = isGoal? new Color(1f, 0.35f, 0.2f): new Color(1f, 0.8f, 0.15f);
mat.color = c;
mat.SetColor("_EmissionColor", c * 0.7f);
mat.EnableKeyword("_EMISSION");
go.GetComponent<MeshRenderer>().sharedMaterial = mat;
return go;
}

public void StartRun()
{
cpIndex = 0;
timer = 0f;
running = false;
finished = false;
splits.Clear();
foreach (var cp in cps) if (cp.obj!= null) cp.obj.SetActive(true);
player.Teleport(spawnPos);
player.ArmMoveTracking();
}

void Update()
{
if (player == null) return;
if (finished)
{
if (Input.GetKeyDown(KeyCode.R)) StartRun();
return;
}
if (!running)
{
if (player.HasMoved) running = true;
else return;
}
timer += Time.deltaTime;
if (cpIndex >= cps.Count) return;
var cp = cps[cpIndex];
Vector3 pp = player.transform.position;
float dx = pp.x - cp.pos.x, dz = pp.z - cp.pos.z;
if (dx * dx + dz * dz < 2.6f * 2.6f && Mathf.Abs(pp.y - cp.pos.y) < 3.2f)
{
if (cp.obj!= null) cp.obj.SetActive(false);
splits.Add(timer);
cpIndex++;
if (cpIndex >= cps.Count)
{
finished = true;
float best = PlayerPrefs.GetFloat("best_" + courseKey, float.MaxValue);
if (timer < best) PlayerPrefs.SetFloat("best_" + courseKey, timer);
OnGoal?.Invoke(timer);
}
}
}

void OnGUI()
{
var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold};
style.normal.textColor = Color.white;
string bestTxt = "";
float best = PlayerPrefs.GetFloat("best_" + courseKey, float.MaxValue);
if (best < float.MaxValue) bestTxt = " ベスト " + best.ToString("F2") + "秒";
GUI.Label(new Rect(16, 12, 700, 30),
" " + timer.ToString("F2") + "秒 CP " + cpIndex + " / " + cps.Count + bestTxt, style);
if (finished)
{
var big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter};
big.normal.textColor = Color.white;
GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), "🏁 ゴール！ " + timer.ToString("F2") + "秒", big);
var sub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter};
sub.normal.textColor = Color.white;
GUI.Label(new Rect(0, Screen.height * 0.3f + 64, Screen.width, 30), "R キーでもう一度", sub);
}
}
}
