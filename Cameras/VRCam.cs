using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VrcftInputProvider : MonoBehaviour
{
    // ... (保持你之前的 VrcftInputProvider 代码不变，不要动它)
}

// 请只覆盖 VRCam 类的部分
public class VRCam : MonoBehaviour
{
    #region Fields

    private bool _seatActivated;
    private GameObject _seatPosition;
    private Vector3 _formerPosition;
    private Transform _cameraRig;

    // 新增：记录头显的初始偏差（用于抵消身高和房间偏移）
    private Vector3 _initialHeadOffset;
    private bool _isCalibrated = false;

    #endregion

    #region PrivateMethods

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (transform.parent != null)
        {
            _cameraRig = transform.parent;
        }
        else
        {
            // 如果没有父物体，为了防止报错，临时创建一个父物体包裹相机
            // 这是一个保险措施，防止用户忘记设置 Offset 结构
            Debug.LogWarning("VRCam: 创建临时 Rig 以修复偏移问题");
            GameObject rig = new GameObject("AutoCreated_Rig");
            rig.transform.position = transform.position;
            rig.transform.rotation = transform.rotation;
            transform.SetParent(rig.transform);
            _cameraRig = rig.transform;
        }
    }

    private void Start()
    {
        if (CameraManager.Instance != null && CameraManager.Instance.GetSeatPosition() != null)
        {
            _seatPosition = CameraManager.Instance.GetSeatPosition();
        }

        _formerPosition = new Vector3();

        // 启动时延迟一帧进行校准（等待 SteamVR 初始化数据）
        StartCoroutine(AutoRecenterRoutine());
    }

    private IEnumerator AutoRecenterRoutine()
    {
        // 等待两帧，确保 TrackedPoseDriver 已经把头显位置更新了
        yield return null;
        yield return null;
        Recenter();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载后也重新校准一下
        StartCoroutine(AutoRecenterRoutine());
    }

    private void LateUpdate()
    {
        // 只有当需要入座、座位存在、且 Rig 存在时才执行
        if (_seatActivated && _seatPosition != null && _cameraRig != null)
        {
            // 1. 先让身体（Rig）的朝向和车座一致
            _cameraRig.rotation = _seatPosition.transform.rotation;

            // 2. 计算位置：
            // 目标位置 = 车座位置 - (当前的头显偏差)
            // 这样做的结果是：Rig 会移动到合适的地方，使得 Camera (头) 刚好落在 车座位置 上

            // 获取当前头显相对于 Rig 的本地位置 (这就是你的身高/坐姿偏移)
            Vector3 currentHeadOffset = transform.localPosition;

            // 将这个本地偏移 旋转到 世界空间
            Vector3 worldOffset = _cameraRig.rotation * currentHeadOffset;

            // 修正 Rig 位置
            _cameraRig.position = _seatPosition.transform.position - worldOffset;
        }
    }

    #endregion

    #region PublicMethods

    // 手动重置中心（如果你觉得位置偏了，可以调用这个方法，或者按个键调用它）
    public void Recenter()
    {
        if (_seatActivated && _seatPosition != null)
        {
            // 这里的 Recenter 逻辑其实在 LateUpdate 里是实时进行的 (6DOF)
            // 如果你想要“锁定”初始位置（比如不希望头前后动），可以修改逻辑
            // 但目前的 LateUpdate 逻辑是最好的：它允许你转头、探身，但始终以车座为基准中心。
            Debug.Log("VRCam: 位置已基于当前 HMD 偏移进行校准。");
        }
    }

    public void Seat()
    {
        _seatActivated = true;
        // 入座时立刻校准一次
        StartCoroutine(AutoRecenterRoutine());
    }

    public void UnSeat()
    {
        if (_cameraRig != null)
        {
            _cameraRig.position = _formerPosition;
        }
        _seatActivated = false;
    }

    public void SetPosition(Vector3 position)
    {
        if (_cameraRig != null)
        {
            _cameraRig.position = position;
        }
        _formerPosition = position;
        _seatActivated = false;
    }

    public void SetSeatPosition(GameObject seatPosition)
    {
        _seatPosition = seatPosition;
    }

    #endregion
}