using UnityEngine;

public class SafeAreaHandler : MonoBehaviour
{
    [Header("Fallback Settings")]
    [SerializeField] private float androidBottomPadding = 100f; // Pixels
    [SerializeField] private bool alwaysUseFallback = false;

    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalAnchorMin = rectTransform.anchorMin;
        originalAnchorMax = rectTransform.anchorMax;
        ApplySafeArea();
    }

    void Update()
    {
        if (Screen.safeArea != lastSafeArea)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        bool isAndroid = PlatformDetection.IsRunningOnAndroid();
        bool hasSystemUI = safeArea.height < Screen.height || safeArea.width < Screen.width;

        // If safe area works and we're not forcing fallback, use it
        if (!alwaysUseFallback && hasSystemUI)
        {
            ApplySafeAreaAnchors(safeArea);
            Debug.Log($"SafeArea: {safeArea} | Screen: {Screen.width}x{Screen.height}");
        }
        // Otherwise use fallback padding on Android
        else if (isAndroid || alwaysUseFallback)
        {
            ApplyFallbackPadding();
            Debug.Log($"Android fallback: {androidBottomPadding}px bottom padding");
        }
        // Desktop/non-Android - use original anchors
        else
        {
            rectTransform.anchorMin = originalAnchorMin;
            rectTransform.anchorMax = originalAnchorMax;
        }
    }

    void ApplySafeAreaAnchors(Rect safeArea)
    {
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
    }

    void ApplyFallbackPadding()
    {
        // Keep original horizontal anchors
        float bottomAnchor = androidBottomPadding / Screen.height;
        rectTransform.anchorMin = new Vector2(originalAnchorMin.x, bottomAnchor);
        rectTransform.anchorMax = originalAnchorMax;
    }
}
