using UnityEngine;

/// <summary>
/// 함선에 터렛을 장착/교체할 수 있는 위치.
/// 비어 있으면 홀로그램 프리뷰(이 오브젝트의 메시)만 보이다가, 인벤토리에서 터렛 아이템을
/// 드래그해 이 위치에 놓으면 실제로 작동하는 터렛 프리팹으로 교체된다.
/// 이미 터렛이 장착되어 있어도(처음부터 장착된 기본 터렛 포함) 다시 드래그해서
/// 놓으면 무기 타입만 새 아이템 것으로 바뀐다(같은 Turret1 프리팹을 재사용하므로
/// 오브젝트를 새로 만들 필요 없이 F3DFXController.DefaultFXType만 바꾸면 된다).
/// </summary>
public class TurretMountPoint : MonoBehaviour
{
    [Tooltip("처음부터 장착되어 있는 터렛(기본 장비). 비워두면 빈 홀로그램 마운트가 된다.")]
    [SerializeField] private GameObject _initialTurret;

    private static readonly int InvFadeId = Shader.PropertyToID("_InvFade");

    private Renderer[] _hologramRenderers;
    private GameObject _mountedTurret;
    private Renderer[] _mountedTurretRenderers;
    private MaterialPropertyBlock _propertyBlock;

    public bool IsOccupied => _mountedTurret != null;

    private void Awake()
    {
        _hologramRenderers = GetComponentsInChildren<Renderer>(true);
        _propertyBlock = new MaterialPropertyBlock();

        // 홀로그램 셰이더(Holographic_New)는 주변 오브젝트와 깊이 차이가 작으면
        // 자동으로 페이드아웃되는 "소프트 인터섹션" 효과가 있다(_InvFade로 조절).
        // 공유 머티리얼은 건드리지 않고 렌더러별로 _InvFade를 낮춰서 페이드아웃을 사실상 꺼둔다.
        foreach (var r in _hologramRenderers)
        {
            if (r == null)
                continue;

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(InvFadeId, 0.05f);
            r.SetPropertyBlock(_propertyBlock);
        }

        if (_initialTurret != null)
        {
            _mountedTurret = _initialTurret;
            _mountedTurretRenderers = _mountedTurret.GetComponentsInChildren<Renderer>(true);

            // 홀로그램의 transform을 실제 터렛과 똑같이 맞춘다(위치/회전은 맞아도
            // localScale이 어긋나 있으면 부모 함선 스케일 보정이 달라져서 크기가 달라 보인다).
            transform.localPosition = _initialTurret.transform.localPosition;
            transform.localRotation = _initialTurret.transform.localRotation;
            transform.localScale = _initialTurret.transform.localScale;

            HideHologram();
        }
    }

    /// <summary>
    /// item의 무기 타입을 이 위치에 적용한다. 비어 있으면 터렛 프리팹을 새로 생성하고,
    /// 이미 장착되어 있으면 기존 터렛의 무기 타입만 바꾼다. 어느 쪽이든 이후에도 계속 재장착 가능.
    /// </summary>
    public bool Equip(TurretItemData item)
    {
        if (item == null || item.turretPrefab == null)
            return false;

        if (IsOccupied)
        {
            var existingFx = _mountedTurret.GetComponentInChildren<FORGE3D.F3DFXController>();
            if (existingFx == null)
                return false;

            existingFx.DefaultFXType = item.weaponType;
            return true;
        }

        _mountedTurret = Instantiate(item.turretPrefab, transform.position, transform.rotation, transform.parent);
        _mountedTurretRenderers = _mountedTurret.GetComponentsInChildren<Renderer>(true);

        var fx = _mountedTurret.GetComponentInChildren<FORGE3D.F3DFXController>();
        if (fx != null)
            fx.DefaultFXType = item.weaponType;

        HideHologram();

        return true;
    }

    /// <summary>
    /// 인벤토리 아이템을 드래그하는 동안 장착 가능 위치를 알려주기 위해 홀로그램을 강제로 보여준다.
    /// 이미 터렛이 장착된 자리라면, 실제 터렛 메시가 홀로그램을 가리지 않도록 잠시 꺼둔다.
    /// </summary>
    public void ShowHologramPreview()
    {
        SetMountedTurretVisible(false);

        foreach (var r in _hologramRenderers)
            if (r != null)
                r.enabled = true;
    }

    /// <summary>드래그가 끝나면 원래 상태(장착되어 있으면 터렛 표시+홀로그램 숨김, 비어 있으면 홀로그램 표시)로 되돌린다.</summary>
    public void RefreshHologramVisibility()
    {
        if (IsOccupied)
        {
            SetMountedTurretVisible(true);
            HideHologram();
        }
        else
        {
            ShowHologramPreview();
        }
    }

    private void SetMountedTurretVisible(bool visible)
    {
        if (_mountedTurretRenderers == null)
            return;

        foreach (var r in _mountedTurretRenderers)
            if (r != null)
                r.enabled = visible;
    }

    private void HideHologram()
    {
        foreach (var r in _hologramRenderers)
            if (r != null)
                r.enabled = false;
    }
}
