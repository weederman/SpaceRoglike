using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickMarkerEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 1.5f;

    private float timer;

    private void Start()
    {
        // 처음에는 작게
        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float progress = timer / duration;

        // 0 ~ 1 사이로 제한
        progress = Mathf.Clamp01(progress);

        // 부드럽게 커지도록
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

        float currentScale = Mathf.Lerp(
            startScale,
            endScale,
            smoothProgress
        );

        transform.localScale =
            Vector3.one * currentScale;

        // 시간이 끝나면 삭제
        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}