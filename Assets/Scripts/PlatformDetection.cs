using UnityEngine;
using System.Runtime.InteropServices;

public class PlatformDetection : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool IsAndroid();
#endif

    public static bool IsRunningOnAndroid()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return IsAndroid();
#elif UNITY_ANDROID
        return true;
#else
        return false;
#endif
    }
}
