using UnityEngine;

/// <summary>
/// 씬 시작 시 플레이어 카메라 위치를 기준으로 반경 안 무작위 위치에 배경 행성들을 흩뿌린다.
/// 행성은 충돌 없는 순수 배경 장식(2D Space Kit 스프라이트를 빌보드로 표시)이다.
/// </summary>
public class PlanetSpawner : MonoBehaviour
{
    [SerializeField] private Sprite[] _planetSprites;
    [SerializeField, Min(1)] private int _planetCount = 12;
    [SerializeField] private float _minDistance = 25f;
    [SerializeField] private float _maxDistance = 100f;
    [SerializeField] private Vector2 _worldSizeRange = new Vector2(15f, 35f);

    private void Start()
    {
        if (_planetSprites == null || _planetSprites.Length == 0)
            return;

        // 카메라는 CameraController.Start()에서 플레이어 기준으로 재배치되는데
        // 스크립트 실행 순서에 따라 그 전에 이 Start()가 먼저 실행될 수 있어
        // 카메라의 초기(에디터 배치) 위치를 기준으로 삼으면 위치가 어긋난다.
        // 플레이어 함선 위치를 기준으로 삼으면 카메라 시점과 사실상 동일하면서도
        // 실행 순서에 안전하다.
        var player = GameObject.FindGameObjectWithTag("Player");
        Transform center = player != null ? player.transform :
            (Camera.main != null ? Camera.main.transform : transform);

        for (var i = 0; i < _planetCount; i++)
        {
            Vector3 offset = Random.onUnitSphere * Random.Range(_minDistance, _maxDistance);
            SpawnPlanet(center.position + offset, i);
        }
    }

    private void SpawnPlanet(Vector3 position, int index)
    {
        var go = new GameObject("Planet_" + index);
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _planetSprites[Random.Range(0, _planetSprites.Length)];

        // 스프라이트마다 원본 픽셀 크기가 달라도 원하는 월드 크기 범위로 통일한다.
        float targetSize = Random.Range(_worldSizeRange.x, _worldSizeRange.y);
        float spriteSize = renderer.sprite.bounds.size.x;
        float scale = spriteSize > 0f ? targetSize / spriteSize : 1f;
        go.transform.localScale = Vector3.one * scale;

        go.AddComponent<Planet>();
    }
}
