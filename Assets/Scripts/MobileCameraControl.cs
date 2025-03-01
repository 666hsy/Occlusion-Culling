using UnityEngine;

public class MobileCameraControl : MonoBehaviour
{
    public float rotationSpeed = 0.1f; // 旋转速度
    public float movementSpeed = 0.1f; // 平移速度

    private Vector2 touchStartPos;  // 触摸起始位置
    private Vector2 touchDelta;     // 触摸偏移量

    private float pitch = 0f; // 垂直旋转
    private float yaw = 0f;   // 水平旋转

    void Update()
    {
        // 处理触摸输入
        if (Input.touchCount == 1) // 单点触摸控制旋转
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                touchDelta = touch.position - touchStartPos;
                touchStartPos = touch.position;

                // 旋转摄像机
                yaw += touchDelta.x * rotationSpeed; // 水平旋转
                pitch -= touchDelta.y * rotationSpeed; // 垂直旋转
                pitch = Mathf.Clamp(pitch, -80f, 80f); // 限制垂直旋转范围

                transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }
        }
        else if (Input.touchCount == 2) // 双点触摸控制平移
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
            Vector2 touch2PrevPos = touch2.position - touch2.deltaPosition;

            float prevTouchDistance = (touch1PrevPos - touch2PrevPos).magnitude;
            float currentTouchDistance = (touch1.position - touch2.position).magnitude;

            float touchDeltaDistance = currentTouchDistance - prevTouchDistance;

            // 根据触摸距离的变化来移动摄像机
            Vector3 movement = transform.forward * touchDeltaDistance * movementSpeed;
            transform.position += movement;
        }
    }
}
