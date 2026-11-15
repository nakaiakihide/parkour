using UnityEngine;

// タッチ入力（左スティック＋右ボタン）とドラッグ視点を、Web 版と同じ配置で提供する土台実装。
// 本番 UI は uGUI 化する前提で、ここでは IMGUI で最小限に実装している。
// 状態は static で持ち、PlayerController / FollowCamera から読む。
public class TouchControls : MonoBehaviour
{
    public static Vector2 Move { get; private set; }
    public static bool JumpHeld { get; private set; }

    static bool jumpQ, dashQ, slideQ, rollQ;
    static Vector2 lookDelta;

    int joyId = -1;
    Vector2 joyOrigin;
    int lookId = -1;
    Vector2 lookPrev;
    bool mouseJoy, mouseLook;
    Vector2 mouseJoyOrigin, mousePrev;

    static Texture2D circleTex;

    public static bool PollJump() { bool v = jumpQ; jumpQ = false; return v; }
    public static bool PollDash() { bool v = dashQ; dashQ = false; return v; }
    public static bool PollSlide() { bool v = slideQ; slideQ = false; return v; }
    public static bool PollRoll() { bool v = rollQ; rollQ = false; return v; }
    public static Vector2 ConsumeLook() { Vector2 v = lookDelta; lookDelta = Vector2.zero; return v; }

    // ボタンの矩形（GUI 座標: 左上原点）
    static Rect JumpRect => new Rect(Screen.width - 200, Screen.height - 200, 160, 160);
    static Rect SlideRect => new Rect(Screen.width - 380, Screen.height - 160, 120, 120);
    static Rect DashRect => new Rect(Screen.width - 350, Screen.height - 320, 120, 120);
    static Rect RollRect => new Rect(Screen.width - 530, Screen.height - 160, 120, 120);

    static bool InButtons(Vector2 guiPos)
        => JumpRect.Contains(guiPos) || SlideRect.Contains(guiPos) || DashRect.Contains(guiPos) || RollRect.Contains(guiPos);

    static Vector2 ToGui(Vector2 screenPos) => new Vector2(screenPos.x, Screen.height - screenPos.y);

    void Update()
    {
        JumpHeld = false;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 gui = ToGui(t.position);
                if (JumpRect.Contains(gui) && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                    JumpHeld = true;
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (InButtons(gui)) break;
                        if (t.position.x < Screen.width * 0.45f && joyId < 0)
                        {
                            joyId = t.fingerId; joyOrigin = t.position;
                        }
                        else if (lookId < 0)
                        {
                            lookId = t.fingerId; lookPrev = t.position;
                        }
                        break;
                    case TouchPhase.Moved:
                        if (t.fingerId == joyId)
                        {
                            Vector2 d = (t.position - joyOrigin) / 52f;
                            Move = Vector2.ClampMagnitude(new Vector2(d.x, d.y), 1f);
                        }
                        else if (t.fingerId == lookId)
                        {
                            lookDelta += t.position - lookPrev;
                            lookPrev = t.position;
                        }
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (t.fingerId == joyId) { joyId = -1; Move = Vector2.zero; }
                        if (t.fingerId == lookId) lookId = -1;
                        break;
                }
            }
        }
        else
        {
            // マウス（エディタ確認用）: 左半分ドラッグ = スティック、それ以外 = 視点
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mp = Input.mousePosition;
                if (!InButtons(ToGui(mp)))
                {
                    if (mp.x < Screen.width * 0.45f) { mouseJoy = true; mouseJoyOrigin = mp; }
                    else { mouseLook = true; mousePrev = mp; }
                }
            }
            if (Input.GetMouseButton(0))
            {
                Vector2 mp = Input.mousePosition;
                if (JumpRect.Contains(ToGui(mp))) JumpHeld = true;
                if (mouseJoy)
                {
                    Vector2 d = (mp - mouseJoyOrigin) / 52f;
                    Move = Vector2.ClampMagnitude(new Vector2(d.x, d.y), 1f);
                }
                else if (mouseLook)
                {
                    lookDelta += mp - mousePrev;
                    mousePrev = mp;
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                mouseJoy = false; mouseLook = false; Move = Vector2.zero;
            }
        }
    }

    void OnGUI()
    {
        if (circleTex == null) circleTex = MakeCircleTex();
        var prevColor = GUI.color;
        // スティック
        if (joyId >= 0 || mouseJoy)
        {
            Vector2 origin = joyId >= 0 ? joyOrigin : mouseJoyOrigin;
            Vector2 knob = origin + new Vector2(Move.x, -Move.y) * 52f;
            GUI.color = new Color(1, 1, 1, 0.25f);
            DrawCircle(new Vector2(origin.x, Screen.height - origin.y), 62);
            GUI.color = new Color(1, 1, 1, 0.55f);
            DrawCircle(new Vector2(knob.x, Screen.height - knob.y), 30);
        }
        GUI.color = prevColor;
        var style = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        if (GUI.Button(JumpRect, "ジャンプ", style)) jumpQ = true;
        if (GUI.Button(SlideRect, "スライド", style)) slideQ = true;
        if (GUI.Button(DashRect, "ダッシュ", style)) dashQ = true;
        if (GUI.Button(RollRect, "ロール", style)) rollQ = true;
    }

    static void DrawCircle(Vector2 centerTopLeft, float r)
    {
        GUI.DrawTexture(new Rect(centerTopLeft.x - r, centerTopLeft.y - r, r * 2, r * 2), circleTex);
    }

    static Texture2D MakeCircleTex()
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new Vector2(s / 2f, s / 2f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                byte a = (byte)(d < s / 2f ? 255 : 0);
                px[y * s + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
