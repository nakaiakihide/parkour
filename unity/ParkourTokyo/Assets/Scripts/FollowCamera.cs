using UnityEngine;

// 三人称フォローカメラ。ドラッグで視点回転（Web 版と同じ感度 0.0052）。
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 6.5f;
    public float yawDeg = 180f;
    public float pitchDeg = 14f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector2 d = TouchControls.ConsumeLook();
        if (d.sqrMagnitude > 0f)
        {
            yawDeg += d.x * 0.30f;    // 0.0052 rad/px ≒ 0.30 deg/px
            pitchDeg += d.y * 0.30f;
            pitchDeg = Mathf.Clamp(pitchDeg, -6f, 63f);
        }
        Quaternion rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
        Vector3 focus = target.position + new Vector3(0, 1.6f, 0);
        transform.position = focus + rot * new Vector3(0, 0, -distance);
        transform.LookAt(focus);
    }
}
