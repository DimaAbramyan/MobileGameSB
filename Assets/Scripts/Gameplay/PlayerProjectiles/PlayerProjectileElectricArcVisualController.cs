using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Managed visual bridge for Arc Nodes. ECS owns arc timing and damage; this
/// component only pools and renders the short-lived jagged LineRenderers.
/// </summary>
public sealed class PlayerProjectileElectricArcVisualController : MonoBehaviour
{
      private readonly List<ActiveArc> activeArcs = new();
      private readonly Stack<LineRenderer> availableLines = new();
      private Material lineMaterial;

      private void Awake()
      {
          Shader spriteShader = Shader.Find("Sprites/Default");
          if (spriteShader == null)
              return;

          lineMaterial = new Material(spriteShader)
          {
              name = "Player Projectile Electric Arc Material",
              enableInstancing = true
          };
      }

    public void Play(PlayerProjectileElectricArcVisualRequest request)
    {
        if (request.Duration <= 0f || request.Width <= 0f)
            return;

        LineRenderer line = GetLine();
        int pointCount = Mathf.Max(1, request.Segments) + 1;
        line.positionCount = pointCount;
        line.widthMultiplier = request.Width;
        Color color = new(
            request.Color.x,
            request.Color.y,
            request.Color.z,
            request.Color.w);
        line.startColor = color;
        line.endColor = color;

        Vector3 start = new(request.Start.x, request.Start.y, 0f);
        Vector3 end = new(request.End.x, request.End.y, 0f);
        Vector2 startPoint = new(request.Start.x, request.Start.y);
        Vector2 endPoint = new(request.End.x, request.End.y);
        Vector2 direction = endPoint - startPoint;
        Vector2 normal = direction.sqrMagnitude > 0.0001f
            ? new Vector2(-direction.y, direction.x).normalized
            : Vector2.up;
        line.SetPosition(0, start);
        for (int index = 1; index < pointCount - 1; index++)
        {
            float progress = (float)index / (pointCount - 1);
            float offset = UnityEngine.Random.Range(
                -request.Jitter,
                request.Jitter);
            Vector2 point = Vector2.Lerp(startPoint, endPoint, progress)
                + normal * offset;
            line.SetPosition(index, new Vector3(point.x, point.y, 0f));
        }
        line.SetPosition(pointCount - 1, end);
        line.enabled = true;
        activeArcs.Add(new ActiveArc(line, Time.time + request.Duration));
    }

    private void Update()
    {
        for (int index = activeArcs.Count - 1; index >= 0; index--)
        {
            ActiveArc arc = activeArcs[index];
            if (Time.time < arc.HideTime)
                continue;

            arc.Line.enabled = false;
            arc.Line.positionCount = 0;
            availableLines.Push(arc.Line);
            activeArcs.RemoveAt(index);
        }
    }

    private LineRenderer GetLine()
    {
        if (availableLines.Count > 0)
            return availableLines.Pop();

        var lineObject = new GameObject("Player Projectile Electric Arc");
        lineObject.transform.SetParent(transform, false);
          LineRenderer line = lineObject.AddComponent<LineRenderer>();
          line.sharedMaterial = lineMaterial;
          line.useWorldSpace = true;
          line.numCapVertices = 2;
          line.numCornerVertices = 2;
        line.sortingOrder = 25;
        line.enabled = false;
        return line;
    }

      private void OnDestroy()
      {
        for (int index = 0; index < activeArcs.Count; index++)
        {
            if (activeArcs[index].Line != null)
                Destroy(activeArcs[index].Line.gameObject);
        }

        while (availableLines.Count > 0)
        {
            LineRenderer line = availableLines.Pop();
            if (line != null)
                  Destroy(line.gameObject);
          }

          if (lineMaterial != null)
              Destroy(lineMaterial);
      }

    private readonly struct ActiveArc
    {
        public ActiveArc(LineRenderer line, float hideTime)
        {
            Line = line;
            HideTime = hideTime;
        }

        public LineRenderer Line { get; }
        public float HideTime { get; }
    }
}
