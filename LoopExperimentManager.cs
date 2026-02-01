//using UnityEngine;
//using System.Collections;

//public class LoopExperimentManager : MonoBehaviour
//{
//    [Header("实验设置")]
//    [Tooltip("一共要跑多少个 Block？")]
//    public int totalBlocks = 3; // 在 Inspector 里手动设置次数

//    [Header("状态监控 (只读)")]
//    public int currentBlock = 1;

//    // 内部变量：记录“原本的起点”
//    private Vector3 _startPosition;
//    private Quaternion _startRotation;
//    private GameObject _playerCar;
//    private Rigidbody _carRb;
//    private AIController _aiController;

//    private void Start()
//    {
//        // 1. 自动找到 AutobahnManager 里的那辆车
//        if (AutobahnManager.Instance != null)
//        {
//            _playerCar = AutobahnManager.Instance.GetParticipantsCar();
//        }

//        if (_playerCar == null)
//        {
//            Debug.LogError("<color=red>[LoopManager] 严重错误：没找到 ParticipantsCar！请检查 AutobahnManager。</color>");
//            return;
//        }

//        // 2. 关键一步：把车“现在的位置”记下来作为起点
//        // 这样就不需要你手动去设置任何 StartPoint 了
//        _startPosition = _playerCar.transform.position;
//        _startRotation = _playerCar.transform.rotation;

//        // 获取组件，方便后面重置用
//        _carRb = _playerCar.GetComponent<Rigidbody>();
//        _aiController = _playerCar.GetComponent<AIController>(); // 既然是自动驾驶，通常有这个

//        Debug.Log($"[LoopManager] 起点已自动校准: {_startPosition}");
//    }

//    // 当车撞上“空气墙”时触发
//    private void OnTriggerEnter(Collider other)
//    {
//        // 只有撞上来的是玩家的车才算数
//        if (other.gameObject == _playerCar || other.transform.root.gameObject == _playerCar)
//        {
//            StartCoroutine(HandleLoop());
//        }
//    }

//    private IEnumerator HandleLoop()
//    {
//        Debug.Log($"[LoopManager] 完成第 {currentBlock} / {totalBlocks} 个 Block");

//        if (currentBlock >= totalBlocks)
//        {
//            // --- 跑完了所有次数 ---
//            Debug.Log("<color=green>[LoopManager] 实验全部结束！</color>");

//            // 通知 ExperimentManager 结束实验
//            if (ExperimentManager.Instance != null)
//            {
//                ExperimentManager.Instance.EndOfExperiment();
//            }
//        }
//        else
//        {
//            // --- 还没跑完，准备下一圈 ---
//            currentBlock++;

//            // 1. 暂时定住刚体 (防止传送后车乱飞)
//            if (_carRb != null)
//            {
//                _carRb.isKinematic = true;
//                _carRb.velocity = Vector3.zero;
//                _carRb.angularVelocity = Vector3.zero;
//            }

//            // 2. 传送回刚才记录的起点
//            _playerCar.transform.position = _startPosition;
//            _playerCar.transform.rotation = _startRotation;

//            yield return null; // 等一帧，让物理引擎缓口气

//            // 3. 恢复刚体
//            if (_carRb != null)
//            {
//                _carRb.isKinematic = false;
//            }

//            // 4. 重置 AI 的路径进度 (非常重要！)
//            // 告诉 AIController：“我现在回到起点了，请重新计算路径”
//            if (_aiController != null)
//            {
//                _aiController.SetLocalTargetAndCurveDetection();
//            }

//            Debug.Log($"[LoopManager] 车辆已复位，开始第 {currentBlock} 圈");
//        }
//    }
//}