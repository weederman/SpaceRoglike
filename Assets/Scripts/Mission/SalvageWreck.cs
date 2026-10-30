using UnityEngine;

/// <summary>
/// 잔해 수색 임무의 잔해 하나 (SalvageMissionDirector가 만든다). 천천히 돌고, 수색이 끝나면 어두워진다.
/// 판정 없이 거리로만 수색하므로 충돌체는 없다.
/// </summary>
public class SalvageWreck : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>들어 있는 특수 합금 수 (0이면 빈 잔해)</summary>
    public int Contents { get; set; }
    public bool Searched { get; private set; }
    /// <summary>수색 진행 (초)</summary>
    public float Progress { get; set; }
    /// <summary>눕힌 채로 세로축으로만 돈다 (큰 잔해가 굴러 함선 높이 위로 솟지 않게)</summary>
    public bool SpinFlat { get; set; }

    private Vector3 _spin;

    private void Awake()
    {
        _spin = Random.onUnitSphere * Random.Range(2f, 6f);
        Tint(0.55f);
    }

    private void Update()
    {
        if (SpinFlat)
            transform.Rotate(Vector3.up, _spin.y * Time.deltaTime, Space.World);
        else
            transform.Rotate(_spin * Time.deltaTime, Space.Self);
    }

    public void MarkSearched()
    {
        Searched = true;
        Tint(0.2f);
    }

    /// <summary>모델을 어둡게 (부서져 그을린 잔해처럼). 머티리얼은 건드리지 않고 블록으로만.</summary>
    private void Tint(float brightness)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, new Color(brightness, brightness, brightness, 1f));
            r.SetPropertyBlock(block);
        }
    }
}
