using UnityEngine;

/// <summary>
/// 배경 행성 하나. 2D 스프라이트를 매 프레임 카메라를 향해 회전시켜(빌보드)
/// 카메라가 플레이어를 따라가며 줌/각도가 조금씩 바뀌어도 항상 평평하게 보이도록 한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Planet : MonoBehaviour
{
    private Transform _camera;

    private void Start()
    {
        _camera = Camera.main != null ? Camera.main.transform : null;
    }

    private void LateUpdate()
    {
        if (_camera == null)
            return;

        transform.rotation = Quaternion.LookRotation(transform.position - _camera.position);
    }
}
