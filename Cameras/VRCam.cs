using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            Debug.LogWarning("VRCam: 创建临时 Rig 以修复偏移问题");
            GameObject rig = new GameObject("AutoCreated_Rig");
            rig.transform.position = transform.position;
            rig.transform.rotation = transform.rotation;
            transform.SetParent(rig.transform);
            _cameraRig = rig.transform;
        }
    }

    // 【关键修复】注销事件，防止场景切换时报错 "MissingReferenceException"
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (CameraManager.Instance != null && CameraManager.Instance.GetSeatPosition() != null)
        {
            _seatPosition = CameraManager.Instance.GetSeatPosition();
        }

        _formerPosition = new Vector3();
        StartCoroutine(AutoRecenterRoutine());
    }

    private IEnumerator AutoRecenterRoutine()
    {
        yield return null;
        yield return null;
        Recenter();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(AutoRecenterRoutine());
    }

    private void LateUpdate()
    {
        if (_seatActivated && _seatPosition != null && _cameraRig != null)
        {
            _cameraRig.rotation = _seatPosition.transform.rotation;
            Vector3 currentHeadOffset = transform.localPosition;
            Vector3 worldOffset = _cameraRig.rotation * currentHeadOffset;
            _cameraRig.position = _seatPosition.transform.position - worldOffset;
        }
    }

    #endregion

    #region PublicMethods

    public void Recenter()
    {
        if (_seatActivated && _seatPosition != null)
        {
            Debug.Log("VRCam: 位置已基于当前 HMD 偏移进行校准。");
        }
    }

    public void Seat()
    {
        _seatActivated = true;
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