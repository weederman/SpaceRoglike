using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항성계 지도 창 (M 키). 항성을 가운데 두고 궤도(행성 궤도 = 선, 소행성 벨트 = 띠), 행성, 정거장, 워프 비콘(레벨 색), 내 위치를 그린다.
/// 장소를 클릭해 고르면 오른쪽에 정보가 나오고, "워프"를 누르면 StarSystemWarp로 그곳에 워프한다.
/// 마우스 휠로 커서 위치를 기준으로 확대/축소하고, 끌어서 지도를 옮긴다(표시 아이콘 크기는 그대로, 위치만 벌어진다).
/// 지도를 열어 둔 동안은 조작이 잠긴다. 스토리 재생 중에는 열 수만 있고(조작하며 듣는 장면) 워프는 할 수 없다.
/// 지도를 닫아 둔 동안에는 화면 위쪽에 지금 있는 지역 이름을 보여준다.
/// 모양은 Assets/Resources/UI/StarMap.prefab (SCI-FI UI Pack Window010_Cyan 기반)이 정하고, StarSystem 프리팹이 생성한다.
/// </summary>
public class StarMapUI : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.M;

    [Header("창")]
    [SerializeField] private GameObject _window;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Button _closeButton;

    [Header("지도")]
    [SerializeField] private RectTransform _mapArea;
    [Tooltip("가장 바깥 궤도가 그려지는 반지름(픽셀)")]
    [SerializeField, Min(10f)] private float _mapRadius = 380f;
    [SerializeField] private OrbitRingsGraphic _rings;
    [SerializeField] private Image _star;
    [SerializeField] private Image _planetTemplate;
    [SerializeField] private StarMapMarker _stationTemplate;
    [SerializeField] private StarMapMarker _beaconTemplate;
    [SerializeField] private Sprite _homeIcon;
    [SerializeField] private RectTransform _playerMarker;

    [Header("확대 / 이동")]
    [Tooltip("확대 배율 범위 (1 = 항성계 전체가 보이는 크기)")]
    [SerializeField] private Vector2 _zoomRange = new Vector2(1f, 8f);
    [Tooltip("휠 한 칸에 확대되는 배율")]
    [SerializeField, Min(1.01f)] private float _zoomStep = 1.25f;

    [Header("색")]
    [SerializeField] private Color _planetOrbitColor = new Color(0.45f, 0.85f, 1f, 0.35f);
    [SerializeField] private Color _beltColor = new Color(0.75f, 0.6f, 0.45f, 0.16f);
    [SerializeField] private Color _stationColor = new Color(0.55f, 0.95f, 1f, 1f);

    [Header("정보")]
    [SerializeField] private TMP_Text _infoName;
    [SerializeField] private TMP_Text _infoKind;
    [SerializeField] private TMP_Text _infoDetail;
    [SerializeField] private Button _warpButton;
    [SerializeField] private TMP_Text _warpButtonLabel;
    [SerializeField] private TMP_Text _hint;

    [Header("지도 밖")]
    [SerializeField] private GameObject _areaLabelRoot;
    [SerializeField] private TMP_Text _areaLabel;
    [Tooltip("워프 섬광 (화면 전체)")]
    [SerializeField] private Image _flash;

    private StarSystem _system;
    private StarSystemWarp _warp;
    private readonly List<StarMapMarker> _markers = new List<StarMapMarker>();
    private StarMapMarker _selected;
    private float _scale;

    // 확대/이동: 지도 위의 것들은 항성계 좌표를 기억해 두었다가 배율과 이동량이 바뀔 때마다 자리를 다시 잡는다
    private readonly List<(RectTransform rect, Vector2 system)> _placed = new List<(RectTransform, Vector2)>();
    private float _zoom = 1f;
    private Vector2 _pan;

    public bool IsOpen => _window.activeSelf;

    /// <summary>임무 목적지 (해적 소탕 등). 지도를 열면 이곳이 골라져 있고 정보에 "임무 목적지"로 표시된다. 없으면 None.</summary>
    public static StarSystem.Location MissionTarget = StarSystem.Location.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MissionTarget = StarSystem.Location.None;
    }

    public void Init(StarSystem system, StarSystemWarp warp)
    {
        _system = system;
        _warp = warp;
        _warp.Flash = _flash;
    }

    private void Start()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            GameFonts.ApplyKorean(text);

        _planetTemplate.gameObject.SetActive(false);
        _stationTemplate.gameObject.SetActive(false);
        _beaconTemplate.gameObject.SetActive(false);
        _flash.gameObject.SetActive(false);
        _window.SetActive(false);

        _closeButton.onClick.AddListener(Close);
        _warpButton.onClick.AddListener(WarpToSelected);

        if (_system == null || _system.Data == null)
        {
            enabled = false;
            _areaLabelRoot.SetActive(false);
            return;
        }

        _title.text = $"항성계 지도 · {_system.Data.systemName}";
        _hint.text = $"휠: 확대·축소    드래그: 이동    {_toggleKey}: 닫기";
        SetupViewport();
        BuildMap();

        StarSystem.OnAreaChanged += HandleAreaChanged;
        HandleAreaChanged(_system.CurrentArea);
    }

    private void OnDestroy()
    {
        StarSystem.OnAreaChanged -= HandleAreaChanged;
        GameInputLock.Unlock(this);
    }

    // ── 열고 닫기 ─────────────────────────────────────

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(KeyCode.Escape))
                Close();
            else
                UpdateOpenMap();
        }
        else if (Input.GetKeyDown(_toggleKey) && CanOpen())
        {
            Open();
        }

        // 지역 이름은 지도가 닫혀 있고 대사·워프가 없을 때만
        _areaLabelRoot.SetActive(!IsOpen && !StarSystemWarp.IsWarping && !StoryPlayer.IsPlaying
                                 && !_system.CurrentArea.IsNone);
    }

    private bool CanOpen()
    {
        return !StarSystemWarp.IsWarping && !GameInputLock.IsLockedExcept(this);
    }

    public void Open()
    {
        _window.SetActive(true);
        GameInputLock.Lock(this);

        // 임무 목적지가 있으면 그곳을, 없으면 지금 있는 곳(없으면 모항)을 골라 둔다
        StarSystem.Location start = !MissionTarget.IsNone ? MissionTarget
            : _system.CurrentArea.IsNone
                ? new StarSystem.Location(StarSystem.LocationKind.Station, _system.Data.homeStation)
                : _system.CurrentArea;
        Select(FindMarker(start));
        UpdateOpenMap();
    }

    public void Close()
    {
        _window.SetActive(false);
        GameInputLock.Unlock(this);
    }

    // ── 지도 ──────────────────────────────────────────

    /// <summary>지도 영역이 휠/드래그를 받고, 확대해서 영역 밖으로 나간 부분은 잘리게 한다</summary>
    private void SetupViewport()
    {
        if (_mapArea.GetComponent<RectMask2D>() == null)
            _mapArea.gameObject.AddComponent<RectMask2D>();

        // 빈 곳에서도 휠/드래그를 받도록 보이지 않는 판을 깐다
        if (_mapArea.GetComponent<Graphic>() == null)
        {
            var catcher = _mapArea.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
        }

        StarMapViewport viewport = _mapArea.GetComponent<StarMapViewport>();
        if (viewport == null)
            viewport = _mapArea.gameObject.AddComponent<StarMapViewport>();
        viewport.Scrolled += HandleScroll;
        viewport.Dragged += HandleDrag;
    }

    private void BuildMap()
    {
        StarSystemData data = _system.Data;
        _scale = _mapRadius / Mathf.Max(1f, data.OuterRadius);

        if (data.starSprite != null)
            _star.sprite = data.starSprite;

        for (int i = 0; i < data.orbits.Length; i++)
        {
            StarSystemData.Orbit o = data.orbits[i];
            if (o.kind != StarSystemData.OrbitKind.Planet)
                continue;

            Image planet = Instantiate(_planetTemplate, _planetTemplate.transform.parent);
            planet.gameObject.SetActive(true);
            planet.name = $"Planet_{o.name}";
            planet.sprite = o.planetSprite;
            planet.rectTransform.sizeDelta *= Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(0.7f, 1.6f, o.planetScale));
            _placed.Add((planet.rectTransform, data.PlanetPosition(i)));
        }

        for (int i = 0; i < data.stations.Length; i++)
        {
            StarMapMarker marker = CreateMarker(_stationTemplate, new StarSystem.Location(StarSystem.LocationKind.Station, i),
                data.StationPosition(i), _stationColor, "");
            if (i == data.homeStation && _homeIcon != null)
                marker.GetComponent<Image>().sprite = _homeIcon;
        }

        for (int i = 0; i < data.beacons.Length; i++)
        {
            int level = data.beacons[i].level;
            CreateMarker(_beaconTemplate, new StarSystem.Location(StarSystem.LocationKind.Beacon, i),
                data.BeaconPosition(i), StarSystemData.LevelColor(level), level.ToString());
        }

        _playerMarker.SetAsLastSibling();
        ApplyView();
    }

    /// <summary>궤도 고리와 지도 위의 모든 것을 지금 배율·이동량에 맞춰 다시 놓는다</summary>
    private void ApplyView()
    {
        StarSystemData data = _system.Data;
        float s = _scale * _zoom;

        _rings.Clear();
        _rings.Offset = _pan;
        foreach (StarSystemData.Orbit o in data.orbits)
        {
            if (o.kind == StarSystemData.OrbitKind.Planet)
                _rings.AddRing(o.radius * s, 1.5f, _planetOrbitColor);
            else
                _rings.AddRing(o.radius * s, Mathf.Max(4f, o.beltWidth * s), _beltColor);
        }

        _star.rectTransform.anchoredPosition = _pan;
        foreach ((RectTransform rect, Vector2 system) in _placed)
            rect.anchoredPosition = ToMap(system);
    }

    private void HandleScroll(Vector2 screenPosition, float amount)
    {
        if (Mathf.Abs(amount) < 0.01f)
            return;

        float zoom = Mathf.Clamp(_zoom * Mathf.Pow(_zoomStep, Mathf.Sign(amount)), _zoomRange.x, _zoomRange.y);
        if (Mathf.Approximately(zoom, _zoom))
            return;

        // 커서 아래에 있던 항성계 지점이 확대 뒤에도 커서 아래에 남도록 이동량을 맞춘다
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_mapArea, screenPosition, null, out Vector2 local))
        {
            Vector2 under = (local - _pan) / (_scale * _zoom);
            _pan = local - under * (_scale * zoom);
        }

        _zoom = zoom;
        ClampPan();
        ApplyView();
    }

    private void HandleDrag(Vector2 screenDelta)
    {
        // 화면 픽셀 → 캔버스 단위 (캔버스 배율)
        Canvas canvas = _mapArea.GetComponentInParent<Canvas>();
        float factor = canvas != null ? canvas.scaleFactor : 1f;
        _pan += screenDelta / Mathf.Max(0.01f, factor);
        ClampPan();
        ApplyView();
    }

    /// <summary>항성계가 지도 영역 밖으로 완전히 벗어나지 않게 한다</summary>
    private void ClampPan()
    {
        float limit = _mapRadius * _zoom;
        _pan = Vector2.ClampMagnitude(_pan, limit);
        if (_zoom <= _zoomRange.x + 0.001f)
            _pan = Vector2.zero; // 전체 보기에서는 가운데로
    }

    private StarMapMarker CreateMarker(StarMapMarker template, StarSystem.Location location, Vector2 position, Color color, string label)
    {
        StarMapMarker marker = Instantiate(template, template.transform.parent);
        marker.gameObject.SetActive(true);
        marker.name = $"Marker_{_system.LocationName(location)}";
        _placed.Add(((RectTransform)marker.transform, position));
        marker.Bind(location, color, label);
        marker.Clicked += Select;
        _markers.Add(marker);
        return marker;
    }

    private Vector2 ToMap(Vector2 system)
    {
        return system * (_scale * _zoom) + _pan;
    }

    private StarMapMarker FindMarker(StarSystem.Location location)
    {
        foreach (StarMapMarker marker in _markers)
        {
            if (marker.Location == location)
                return marker;
        }

        return null;
    }

    private void UpdateOpenMap()
    {
        Transform player = _system.Player;
        bool inside = player != null && _system.IsInsideSystem(player.position);
        _playerMarker.gameObject.SetActive(inside);
        if (inside)
            _playerMarker.anchoredPosition = ToMap(_system.ToSystem(player.position));

        RefreshInfo();
    }

    // ── 선택 / 정보 / 워프 ─────────────────────────────

    private void Select(StarMapMarker marker)
    {
        if (_selected != null)
            _selected.SetSelected(false);

        _selected = marker;
        if (_selected != null)
            _selected.SetSelected(true);

        RefreshInfo();
    }

    private void RefreshInfo()
    {
        if (_selected == null)
        {
            _infoName.text = "";
            _infoKind.text = "";
            _infoDetail.text = "지도에서 장소를 선택하세요.";
            _warpButton.interactable = false;
            _warpButtonLabel.text = "워프";
            return;
        }

        StarSystemData data = _system.Data;
        StarSystem.Location location = _selected.Location;
        _infoName.text = _system.LocationName(location);

        if (location.Kind == StarSystem.LocationKind.Station)
        {
            StarSystemData.Station s = data.stations[location.Index];
            _infoKind.text = location.Index == data.homeStation ? "우주정거장 · 모항" : "우주정거장";
            _infoDetail.text = $"{data.orbits[s.orbit].name} 궤도\n정비와 무기 상점을 이용할 수 있습니다.";
        }
        else
        {
            StarSystemData.Beacon b = data.beacons[location.Index];
            string color = ColorUtility.ToHtmlStringRGB(StarSystemData.LevelColor(b.level));
            _infoKind.text = $"워프 비콘 · <color=#{color}>레벨 {b.level}</color>";
            _infoDetail.text = $"{data.orbits[b.orbit].name}\n레벨이 높을수록 귀한 광물이 많이 나오지만, 해적도 강하고 많습니다.";
        }

        Transform player = _system.Player;
        if (player != null && _system.IsInsideSystem(player.position))
        {
            float distance = Vector2.Distance(_system.ToSystem(player.position), _system.ToSystem(_system.LocationWorld(location)));
            _infoDetail.text += $"\n거리 {distance:N0}";
        }

        if (location == MissionTarget)
            _infoDetail.text += "\n<color=#FFD166>▶ 임무 목적지</color>";

        bool here = _system.CurrentArea == location && !StationDocking.IsDocked;
        bool storyBusy = StoryPlayer.IsPlaying || MissionDirector.BlocksTravel;
        bool docked = StationDocking.IsDocked;
        _warpButton.interactable = !here && !storyBusy && !docked && !StarSystemWarp.IsWarping;
        _warpButtonLabel.text = here ? "현재 위치" : storyBusy ? "임무 진행 중" : docked ? "출항 후 워프" : "워프";
    }

    private void WarpToSelected()
    {
        if (_selected == null || _system.CurrentArea == _selected.Location || !_warp.CanWarp(this))
            return;

        StarSystem.Location destination = _selected.Location;
        Close();
        _warp.WarpTo(destination);
    }

    private void HandleAreaChanged(StarSystem.Location area)
    {
        if (area.IsNone)
        {
            _areaLabel.text = "";
            return;
        }

        string name = _system.LocationName(area);
        if (area.Kind == StarSystem.LocationKind.Beacon)
        {
            int level = _system.Data.beacons[area.Index].level;
            string color = ColorUtility.ToHtmlStringRGB(StarSystemData.LevelColor(level));
            _areaLabel.text = $"{name} · <color=#{color}>레벨 {level}</color>    <size=80%>{_toggleKey}: 항성계 지도</size>";
        }
        else
        {
            _areaLabel.text = $"{name}    <size=80%>{_toggleKey}: 항성계 지도</size>";
        }
    }
}
