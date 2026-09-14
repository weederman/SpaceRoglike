using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteSpaceGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float gridSize = 2f;
    [SerializeField] private int gridCount = 20;
    [SerializeField] private float lineHeight = 0.01f;
    [SerializeField] private float lineWidth = 0.015f;

    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Grid Color")]
    [SerializeField] private Color gridColor = new Color(0.15f, 0.25f, 0.35f, 0.35f);

    private LineRenderer[] verticalLines;
    private LineRenderer[] horizontalLines;

    private float gridWorldSize;

    private void Start()
    {
        gridWorldSize = gridSize * gridCount;

        CreateGrid();
    }

    private void Update()
    {
        if (target == null)
            return;

        FollowTarget();
    }

    private void CreateGrid()
    {
        int lineCount = gridCount * 2 + 1;

        verticalLines = new LineRenderer[lineCount];
        horizontalLines = new LineRenderer[lineCount];

        for (int i = 0; i < lineCount; i++)
        {
            verticalLines[i] = CreateLine("VerticalLine_" + i);
            horizontalLines[i] = CreateLine("HorizontalLine_" + i);
        }

        UpdateGridPositions();
    }

    private LineRenderer CreateLine(string lineName)
    {
        GameObject lineObject = new GameObject(lineName);

        lineObject.transform.SetParent(transform);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();

        line.positionCount = 2;

        // 월드 공간 기준
        line.useWorldSpace = true;

        // 선 굵기
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        // 기본 Line Renderer 머티리얼
        line.material = new Material(Shader.Find("Sprites/Default"));

        // 그리드 색상
        line.startColor = gridColor;
        line.endColor = gridColor;

        // 그림자 사용하지 않음
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        line.receiveShadows = false;

        return line;
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = target.position;

        // Y축은 고정
        targetPosition.y = lineHeight;

        // 그리드가 항상 일정한 크기로 보이도록
        // 격자 간격 단위로 위치를 맞춘다.
        targetPosition.x =
            Mathf.Round(targetPosition.x / gridSize) * gridSize;

        targetPosition.z =
            Mathf.Round(targetPosition.z / gridSize) * gridSize;

        transform.position = targetPosition;

        UpdateGridPositions();
    }

    private void UpdateGridPositions()
    {
        if (verticalLines == null)
            return;

        int lineCount = gridCount * 2 + 1;

        float halfSize = gridWorldSize;

        for (int i = 0; i < lineCount; i++)
        {
            float offset =
                (i - gridCount) * gridSize;

            // X 방향으로 뻗는 선
            verticalLines[i].SetPosition(
                0,
                new Vector3(
                    transform.position.x + offset,
                    lineHeight,
                    transform.position.z - halfSize
                )
            );

            verticalLines[i].SetPosition(
                1,
                new Vector3(
                    transform.position.x + offset,
                    lineHeight,
                    transform.position.z + halfSize
                )
            );

            // Z 방향으로 뻗는 선
            horizontalLines[i].SetPosition(
                0,
                new Vector3(
                    transform.position.x - halfSize,
                    lineHeight,
                    transform.position.z + offset
                )
            );

            horizontalLines[i].SetPosition(
                1,
                new Vector3(
                    transform.position.x + halfSize,
                    lineHeight,
                    transform.position.z + offset
                )
            );
        }
    }
}