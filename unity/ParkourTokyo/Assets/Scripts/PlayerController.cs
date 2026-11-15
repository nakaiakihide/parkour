using UnityEngine;

// プレイヤーの移動・物理。Web 版の確定スペック（現実×1.5倍盛り）を移植したもの。
// 走行10m/s、ダッシュ13m/s(0.85秒/CD1.4秒)、重力20、ジャンプ初速 立ち6.3/助走8.0、
// コヨーテ0.12秒、先行入力0.15秒、壁走り最大約1.3秒、縁つかみ→ぶら下がり0.18秒→クライムアップ。
// 原点は足元（CharacterController の center を +0.9 にしてある前提）。
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float runSpeed = 10f;
    public float sprintSpeed = 13f;
    public float gravity = 20f;
    public float maxFall = 26f;
    public float jumpVelStand = 6.3f;
    public float jumpVelRun = 8.0f;

    public CityBuilder city;
    public Transform cam;
    public bool InputEnabled = true;
    public bool HasMoved { get; private set; }

    CharacterController cc;
    Vector3 vel;
    Vector3 facing = new Vector3(0, 0, 1);
    bool grounded;
    float coyote, jumpBuf, rollBuf;
    float dashT, dashCd;
    Vector3 dashDir;
    float slideT, wallRunT, wallRunCd, recoverT;
    Vector3 wallNormal;
    bool jumpCutDone = true, jumpHeldPrev;

    enum State { Normal, Hang, Climb }
    State state = State.Normal;
    LedgeSegment hangLedge;
    Vector2 hangClosest, hangOutward;
    float hangTimer, climbTimer, climbDur;
    Vector3 climbFrom, climbTo;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.55f; // Web 版の段差アシスト（55cm）相当
        cc.slopeLimit = 50f;
    }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
        vel = Vector3.zero;
        state = State.Normal;
    }

    public void ArmMoveTracking() => HasMoved = false;

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        // ---- 入力 ----
        Vector2 moveIn = Vector2.zero;
        bool jumpHeld = false;
        if (InputEnabled)
        {
            moveIn = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (moveIn.sqrMagnitude < 0.01f) moveIn = TouchControls.Move;
            moveIn = Vector2.ClampMagnitude(moveIn, 1f);
            jumpHeld = Input.GetKey(KeyCode.Space) || TouchControls.JumpHeld;
            if (Input.GetKeyDown(KeyCode.Space) || TouchControls.PollJump()) jumpBuf = 0.15f;
            if (Input.GetKeyDown(KeyCode.LeftShift) || TouchControls.PollDash()) TryDash();
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.LeftControl) || TouchControls.PollSlide())
                slideT = 0.55f;
            if (Input.GetKeyDown(KeyCode.C) || TouchControls.PollRoll()) rollBuf = 0.25f;
        }
        if (jumpBuf > 0) jumpBuf -= dt;
        if (rollBuf > 0) rollBuf -= dt;
        if (dashCd > 0) dashCd -= dt;
        if (wallRunCd > 0) wallRunCd -= dt;
        if (recoverT > 0) recoverT -= dt;

        // カメラ基準の移動方向
        Vector3 camF = cam != null ? cam.forward : Vector3.forward;
        camF.y = 0; camF.Normalize();
        Vector3 camR = new Vector3(camF.z, 0, -camF.x);
        Vector3 moveDir = camF * moveIn.y + camR * moveIn.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        if (moveIn.sqrMagnitude > 0.09f) HasMoved = true;

        if (state == State.Hang) { UpdateHang(dt); return; }
        if (state == State.Climb) { UpdateClimb(dt); return; }

        // ---- 壁走り判定 ----
        bool wallRunning = false;
        if (!grounded && wallRunCd <= 0f && moveDir.sqrMagnitude > 0.1f && vel.y < 3f)
        {
            Vector3 origin = transform.position + new Vector3(0, 1.2f, 0);
            Vector3 side = new Vector3(moveDir.z, 0, -moveDir.x);
            RaycastHit hit;
            if ((Physics.Raycast(origin, side, out hit, 1.05f) || Physics.Raycast(origin, -side, out hit, 1.05f))
                && hit.collider.GetComponent<MeshCollider>() != null)
            {
                wallRunning = wallRunT < 1.3f;
                if (wallRunning) { wallNormal = hit.normal; wallRunT += dt; }
            }
        }
        if (!wallRunning) wallRunT = 0f;

        // ---- 水平速度 ----
        float speedNow = new Vector3(vel.x, 0, vel.z).magnitude;
        Vector3 target;
        if (dashT > 0f)
        {
            dashT -= dt;
            target = dashDir * sprintSpeed;
        }
        else if (slideT > 0f && grounded)
        {
            slideT -= dt;
            target = facing * Mathf.Max(speedNow, 11f);
        }
        else
        {
            target = moveDir * (runSpeed * Mathf.Clamp01(moveIn.magnitude + 0.001f));
            if (recoverT > 0f) target = Vector3.zero;
        }
        float accel = grounded ? 14f : 3.5f;
        vel.x = Mathf.Lerp(vel.x, target.x, Mathf.Clamp01(accel * dt));
        vel.z = Mathf.Lerp(vel.z, target.z, Mathf.Clamp01(accel * dt));
        if (moveDir.sqrMagnitude > 0.05f && dashT <= 0f) facing = moveDir;

        // ---- 垂直速度 ----
        if (grounded) { coyote = 0.12f; if (vel.y < -1f) vel.y = -1f; }
        else if (coyote > 0) coyote -= dt;
        if (wallRunning)
        {
            vel.y += 2.0f * dt;               // ゆっくり上昇
            if (vel.y > 2.4f) vel.y = 2.4f;
        }
        else
        {
            vel.y -= gravity * dt;
            if (dashT > 0f && vel.y < -2f) vel.y = -2f;
            if (vel.y < -maxFall) vel.y = -maxFall;
        }

        // ---- ジャンプ ----
        if (jumpBuf > 0f)
        {
            if (wallRunning)
            {
                vel.y = 7.5f;
                Vector3 away = wallNormal; away.y = 0; away.Normalize();
                vel.x = away.x * 5f + facing.x * 2f;
                vel.z = away.z * 5f + facing.z * 2f;
                jumpBuf = 0; wallRunT = 0; wallRunCd = 0.45f; jumpCutDone = false;
            }
            else if (grounded || coyote > 0f)
            {
                vel.y = speedNow > 3f ? jumpVelRun : jumpVelStand;
                jumpBuf = 0; coyote = 0; jumpCutDone = false;
                grounded = false;
            }
        }
        // 可変ジャンプ（離すと上昇を切る）
        if (!jumpHeld && jumpHeldPrev && !jumpCutDone && vel.y > 3.2f)
        {
            vel.y = 3.2f;
            jumpCutDone = true;
        }
        jumpHeldPrev = jumpHeld;

        // ---- 縁つかみ ----
        float impact = -vel.y;
        if (!grounded && vel.y <= -1f && city != null && TryGrabLedge())
        {
            return;
        }

        // ---- 移動と着地 ----
        CollisionFlags flags = cc.Move(vel * dt);
        bool wasGrounded = grounded;
        grounded = (flags & CollisionFlags.Below) != 0;
        if (grounded && !wasGrounded)
        {
            OnLand(impact);
        }
        if (transform.position.y < -30f) Teleport(new Vector3(0, 60f, 0)); // 落下リカバリ（仮）
    }

    void OnLand(float impact)
    {
        if (rollBuf > 0f && impact >= 11f)
        {
            rollBuf = 0; // タイミング受け身成功: 硬直なし
            return;
        }
        if (impact >= 18.2f) recoverT = 1.3f;
        else if (impact >= 15.5f) recoverT = 0.8f;
    }

    void TryDash()
    {
        if (dashCd > 0f || state != State.Normal) return;
        dashT = 0.85f;
        dashCd = 1.4f;
        Vector3 d = new Vector3(vel.x, 0, vel.z);
        dashDir = d.sqrMagnitude > 1f ? d.normalized : facing;
    }

    bool TryGrabLedge()
    {
        Vector2 p = new Vector2(transform.position.x, transform.position.z);
        float feet = transform.position.y;
        LedgeSegment best = null;
        Vector2 bestC = Vector2.zero, bestOut = Vector2.zero;
        float bestD = 0.95f;
        foreach (var ledge in city.Ledges)
        {
            if (ledge.top < feet + 0.6f || ledge.top > feet + 4.2f) continue;
            float d = PolyUtil.SegDist(p, ledge.a, ledge.b, out Vector2 c);
            if (d >= bestD) continue;
            Vector2 outward = c - ledge.center;
            if (outward.sqrMagnitude < 1e-6f) continue;
            outward.Normalize();
            if (Vector2.Dot(p - c, outward) < -0.05f) continue; // 屋根の内側にいる場合は掴まない
            best = ledge; bestC = c; bestOut = outward; bestD = d;
        }
        if (best == null) return false;
        hangLedge = best;
        hangClosest = bestC;
        hangOutward = bestOut;
        hangTimer = 0f;
        state = State.Hang;
        vel = Vector3.zero;
        transform.position = new Vector3(
            bestC.x + bestOut.x * 0.34f, best.top - 1.72f, bestC.y + bestOut.y * 0.34f);
        facing = new Vector3(-bestOut.x, 0, -bestOut.y);
        return true;
    }

    void UpdateHang(float dt)
    {
        hangTimer += dt;
        if (hangTimer < 0.18f) return;
        state = State.Climb;
        climbTimer = 0f;
        float rise = hangLedge.top - transform.position.y;
        climbDur = 0.45f + rise * 0.12f;
        climbFrom = transform.position;
        climbTo = new Vector3(
            hangClosest.x - hangOutward.x * 0.5f, hangLedge.top, hangClosest.y - hangOutward.y * 0.5f);
    }

    void UpdateClimb(float dt)
    {
        climbTimer += dt;
        float t = Mathf.Clamp01(climbTimer / climbDur);
        t = t * t * (3f - 2f * t);
        Vector3 pos = Vector3.Lerp(climbFrom, climbTo, t);
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
        if (t >= 1f)
        {
            state = State.Normal;
            vel = Vector3.zero;
            grounded = true;
        }
    }
}
