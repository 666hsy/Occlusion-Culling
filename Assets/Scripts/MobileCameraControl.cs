using System;
using UnityEngine;

public class MobileCameraControl : MonoBehaviour
{
    public float moveSpeed = 20f; // 摄像机移动速度
    public float rotateSpeed = 90f; // 摄像机旋转速度

    private Vector2 lastTouchPosition;
    
    private Vector3[] targetPoints;
    
    private bool isMoving = true;
    private int currentPointIndex = 0;
    private float currentRotation = 0f;
    private float rotationAngle = 360f;
    
    public float rotationSpeed = 0.1f; // 旋转速度
    public float movementSpeed = 0.1f; // 平移速度

    private Vector2 touchStartPos;  // 触摸起始位置
    private Vector2 touchDelta;     // 触摸偏移量

    private float pitch = 0f; // 垂直旋转
    private float yaw = 0f;   // 水平旋转

    private void Start()
    {
        targetPoints = new Vector3[CommonData.CameraMovePointCount];
        GameObject CameraPointParent = GameObject.Find("CameraPointParent");
        if (CameraPointParent == null)
        {
            Debug.LogError("CameraPointParent is null");
            return;
        }
        for(int i = 0; i < CameraPointParent.transform.childCount; i++)
        {
            targetPoints[i] = CameraPointParent.transform.GetChild(i).position;
        }
    }

    void Update()
    {
        // if (isMoving)
        // {
        //     // 移动摄像机到目标点
        //     MoveToTargetPoint();
        // }
        // else
        // {
        //     // 摄像机到达目标点后进行旋转
        //     RotateAtTargetPoint();
        // }
        
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
    void MoveToTargetPoint()
    {
        Vector3 targetPoint = targetPoints[currentPointIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.deltaTime);

        if (transform.position == targetPoint)
        {
            isMoving = false;
        }
    }

    void RotateAtTargetPoint()
    {
        float rotationThisFrame = rotateSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationThisFrame);
        currentRotation += rotationThisFrame;

        if (currentRotation >= rotationAngle)
        {
            currentRotation = 0f;
            currentPointIndex++;
            if (currentPointIndex >= CommonData.CameraMovePointCount)
            {
                currentPointIndex = 0; // 循环回到第一个点
                var hizInit = FindObjectOfType<HizInit>();
                hizInit.OnDestroy();
                Application.Quit();
            }
            isMoving = true;
        }
    }
}