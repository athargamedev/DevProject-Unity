using UnityEngine;
using UnityEditor;

public static class QuickScreenshot
{
    [MenuItem("Tools/Andre/Take Screenshot")]
    public static void Take()
    {
        ScreenCapture.CaptureScreenshot("D:/DevProject/screenshot_temp.png", 1);
        Debug.Log("[Screenshot] Captured to D:/DevProject/screenshot_temp.png");
    }
}
