using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections;
using TMPro;


public class AdjustTimeScale : MonoBehaviour
{
    TextMeshProUGUI textMesh;

    private void Start()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        float scrollValue = 0f;
        if (Mouse.current != null)
        {
            scrollValue = Mouse.current.scroll.y.ReadValue();
        }
#else
        float scrollValue = Input.GetAxis("Mouse ScrollWheel");
#endif

        if (scrollValue > 0f)
        {
            if (Time.timeScale < 1.0F)
            {
                Time.timeScale += 0.1f;
            }
            Time.fixedDeltaTime = 0.02F * Time.timeScale;
            if (textMesh != null)
            {
                textMesh.text = "Time Scale : " + System.Math.Round(Time.timeScale, 2);
            }
        }
        else if (scrollValue < 0f)
        {
            if (Time.timeScale >= 0.2F)
            {
                Time.timeScale -= 0.1f;
            }
            Time.fixedDeltaTime = 0.02F * Time.timeScale;
            if (textMesh != null)
            {
                textMesh.text = "Time Scale : " + System.Math.Round(Time.timeScale, 2);
            }
        }
    }

    void OnApplicationQuit()
    {
        Time.timeScale = 1.0F;
        Time.fixedDeltaTime = 0.02F;
    }
}