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
        if (isMoving)
        {
            // 移动摄像机到目标点
            MoveToTargetPoint();
        }
        else
        {
            // 摄像机到达目标点后进行旋转
            RotateAtTargetPoint();
        }
        
        // if (Input.touchCount > 0)
        // {
        //     Touch touch = Input.GetTouch(0);
        //
        //     switch (touch.phase)
        //     {
        //         case TouchPhase.Began:
        //             lastTouchPosition = touch.position;
        //             break;
        //         case TouchPhase.Moved:
        //             Vector2 deltaPosition = touch.position - lastTouchPosition;
        //
        //             // 控制摄像机移动
        //             float horizontalMove = deltaPosition.x * moveSpeed * Time.deltaTime;
        //             float verticalMove = deltaPosition.y * moveSpeed * Time.deltaTime;
        //             transform.Translate(horizontalMove, 0f, verticalMove);
        //
        //             // 控制摄像机旋转
        //             float horizontalRotate = -deltaPosition.x * rotateSpeed * Time.deltaTime;
        //             float verticalRotate = -deltaPosition.y * rotateSpeed * Time.deltaTime;
        //             transform.Rotate(verticalRotate, horizontalRotate, 0f);
        //
        //             lastTouchPosition = touch.position;
        //             break;
        //     }
        // }
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