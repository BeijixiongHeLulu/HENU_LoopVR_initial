using UnityEngine;

namespace LoopAr.Connector
{
    // --- 修复：补上缺失的枚举定义 ---
    public enum TimeStampType
    {
        RealTime,
        UnityTime
    }

    public class EyeTrackingConnector
    {
        // 核心：LoopVR 获取注视数据的入口
        public static Ray RequestCombinedGazeRay()
        {
            Camera cam = Camera.main;
            // 如果相机没准备好，或者数据接收器没挂载，就返回默认前方
            if (cam == null) return new Ray(Vector3.zero, Vector3.forward);

            // 如果 VrcftDataHandler 还没准备好（比如游戏刚开始），就先返回相机正前方，防止报错
            if (VrcftDataHandler.Instance == null) return new Ray(cam.transform.position, cam.transform.forward);

            // 1. 从你的 VrcftDataHandler 获取当前帧的数据
            // (取双眼平均值)
            float x = (VrcftDataHandler.Instance.LeftEyeX + VrcftDataHandler.Instance.RightEyeX) / 2.0f;
            float y = (VrcftDataHandler.Instance.LeftEyeY + VrcftDataHandler.Instance.RightEyeY) / 2.0f;

            // 2. 将 VRCFT 的屏幕空间参数 (-1 到 1) 转化为 3D 方向
            // z = 1.0f 表示前方。
            Vector3 localGazeDirection = new Vector3(x, y, 1.0f).normalized;

            // 3. 将相对方向转化为世界射线 (基于 VR 相机的当前朝向)
            Vector3 worldGazeDirection = cam.transform.TransformDirection(localGazeDirection);

            return new Ray(cam.transform.position, worldGazeDirection);
        }

        // --- 获取睁眼/闭眼数据 ---
        public static void ShowEyeOpenness(out float leftOpenness, out float rightOpenness)
        {
            if (VrcftDataHandler.Instance != null)
            {
                leftOpenness = VrcftDataHandler.Instance.LeftOpenness;
                rightOpenness = VrcftDataHandler.Instance.RightOpenness;
            }
            else
            {
                leftOpenness = 1.0f; // 默认睁眼
                rightOpenness = 1.0f;
            }
        }

        // --- 下面这些是为了防报错的“空壳”方法 ---

        // 这里的 TimeStampType 之前报错，现在因为上面定义了 enum，所以不会报错了
        public static void InitiateEyeTracker(string storePath = null, string prefix = null, TimeStampType timeStampType = TimeStampType.RealTime)
        {
            // 在这里强制确保 Handler 存在，作为一种保险措施
            if (VrcftDataHandler.Instance == null)
            {
                // 尝试在场景里找一下，防止是挂了脚本但 Instance 还没赋值
                var existing = GameObject.FindObjectOfType<VrcftDataHandler>();
                if (existing == null)
                {
                    // 如果真没有，这只是个保险，通常你应该手动挂载
                    Debug.LogWarning("EyeTrackingConnector: 场景中未找到 VrcftDataHandler，正在自动创建...");
                    GameObject go = new GameObject("VRCFT_Handler_Auto");
                    go.AddComponent<VrcftDataHandler>();
                }
            }
        }

        public static void StartCalibrateEyeTracker()
        {
            Debug.Log("Pico VRCFT 不需要应用内校准");
        }

        // 保持频率为 90Hz (或 72Hz，取决于你的设置)
        public static float EyeTrackerFrequency() { return 90f; }

        public static string GetEyeTrackingTimeStamp() { return Time.realtimeSinceStartup.ToString(); }

        public static void PauseEyeTracker() { }
        public static void ContinueEyeTracker() { }
        public static void StartValidationEyeTracker() { }
        public static void StoreDataComplete(string storePath = null, string prefix = null) { }
        public static void ResetEyeTrackingData() { }

        public static void RequestLastEyePosition(out Ray combinedEyeGazeVector, out Vector3 leftEyePosition, out Ray leftEyeGazeVector, out Vector3 rightEyePosition, out Ray rightEyeGazeVector)
        {
            // 简单实现，防止调用报错
            combinedEyeGazeVector = RequestCombinedGazeRay();
            leftEyePosition = Camera.main ? Camera.main.transform.position : Vector3.zero;
            leftEyeGazeVector = combinedEyeGazeVector;
            rightEyePosition = leftEyePosition;
            rightEyeGazeVector = combinedEyeGazeVector;
        }
    }
}